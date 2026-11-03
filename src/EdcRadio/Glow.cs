using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace EdcRadio
{
    internal sealed unsafe class Glow
    {
        private const int W = Canvas.W;
        private const int H = Canvas.H;

        private readonly int[] _source = new int[W * H];
        private readonly float[] _light = new float[W * H];
        private readonly float[] _spare = new float[W * H];
        private readonly float[] _row = new float[W + 1];
        private readonly int[] _shade = new int[256];
        private readonly int[] _solid = new int[256];
        private readonly bool[] _edge = new bool[W * H];
        private float _background = -1f;
        private float _dim = -1f;
        private int _scale;
        private float[] _mix;

        public void Compose(Bitmap canvas, int scale, int rows, bool glow, float background, float dim, IntPtr target, int stride)
        {
            rows = Math.Max(1, Math.Min(H, rows));
            Prepare(scale, background, dim);
            BitmapData lk = canvas.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            for (int y = 0; y < rows; y++) Marshal.Copy(lk.Scan0 + y * lk.Stride, _source, y * W, W);
            canvas.UnlockBits(lk);
            bool edges = background < 0.999f;
            if (edges)
            {
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < W; x++)
                    {
                        bool near = false;
                        for (int dy = -1; dy <= 1 && !near; dy++)
                        {
                            int yy = y + dy;
                            if (yy < 0 || yy >= rows) continue;
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx;
                                if (xx >= 0 && xx < W && (_source[yy * W + xx] & 0xFF) > 127)
                                {
                                    near = true;
                                    break;
                                }
                            }
                        }
                        _edge[y * W + x] = near;
                    }
            }
            if (glow)
            {
                for (int i = 0; i < rows * W; i++) _light[i] = (_source[i] & 0xFF) > 127 ? 1f : 0f;
                for (int pass = 0; pass < 2; pass++)
                {
                    Horizontal(_light, _spare, rows, 2);
                    Vertical(_spare, _light, rows, 2);
                }
            }
            int bg = _shade[0];
            int solid = _solid[0];
            int oh = rows * scale;
            fixed (int* src = _source)
            fixed (float* row = _row)
            fixed (float* mix = _mix)
            fixed (int* shadeBg = _shade)
            fixed (int* shadeSolid = _solid)
            fixed (float* light = _light)
            fixed (bool* edge = _edge)
            {
                for (int oy = 0; oy < oh; oy++)
                {
                    int* dst = (int*)((byte*)target + (long)oy * stride);
                    int* line = src + oy / scale * W;
                    bool* near = edge + oy / scale * W;
                    if (!glow)
                    {
                        for (int cx = 0; cx < W; cx++)
                        {
                            int c = (line[cx] & 0xFF) > 127 ? -1 : edges && near[cx] ? solid : bg;
                            for (int k = 0; k < scale; k++) *dst++ = c;
                        }
                        continue;
                    }
                    float v = (oy + 0.5f) / scale - 0.5f;
                    int top = (int)Math.Floor(v);
                    float fy = v - top;
                    float* a = light + Math.Max(0, Math.Min(rows - 1, top)) * W;
                    float* b = light + Math.Max(0, Math.Min(rows - 1, top + 1)) * W;
                    for (int x = 0; x < W; x++) row[x] = a[x] + (b[x] - a[x]) * fy;
                    row[W] = row[W - 1];
                    for (int cx = 0; cx < W; cx++)
                    {
                        if ((line[cx] & 0xFF) > 127)
                        {
                            for (int k = 0; k < scale; k++) *dst++ = -1;
                            continue;
                        }
                        int* shade = edges && near[cx] ? shadeSolid : shadeBg;
                        for (int k = 0; k < scale; k++)
                        {
                            float m = mix[k];
                            float g = m < 0f
                                ? (cx == 0 ? row[0] : row[cx - 1] + (row[cx] - row[cx - 1]) * (1f + m))
                                : row[cx] + (row[cx + 1] - row[cx]) * m;
                            int i = (int)(g * 255f);
                            *dst++ = shade[i < 0 ? 0 : i > 255 ? 255 : i];
                        }
                    }
                }
            }
        }

        private void Prepare(int scale, float background, float dim)
        {
            if (scale != _scale || _mix == null)
            {
                _scale = scale;
                _mix = new float[scale];
                for (int k = 0; k < scale; k++) _mix[k] = (k + 0.5f) / scale - 0.5f;
            }
            if (Math.Abs(background - _background) < 0.001f && Math.Abs(dim - _dim) < 0.001f) return;
            _background = background;
            _dim = dim;
            Fill(_shade, background * dim, dim);
            Fill(_solid, 1f, dim);
        }

        private static void Fill(int[] table, float background, float dim)
        {
            for (int i = 0; i < 256; i++)
            {
                float g = Math.Min(0.6f, i / 255f * 1.7f) * dim;
                float alpha = g + background * (1f - g);
                int a = (int)Math.Round(alpha * 255f), c = (int)Math.Round(g * 255f);
                table[i] = (a << 24) | (c << 16) | (c << 8) | c;
            }
        }

        private static void Horizontal(float[] src, float[] dst, int rows, int r)
        {
            float inv = 1f / (2 * r + 1);
            for (int y = 0; y < rows; y++)
            {
                int row = y * W;
                float acc = 0f;
                for (int x = -r; x <= r; x++) acc += src[row + Math.Max(0, Math.Min(W - 1, x))];
                for (int x = 0; x < W; x++)
                {
                    dst[row + x] = acc * inv;
                    acc += src[row + Math.Min(W - 1, x + r + 1)] - src[row + Math.Max(0, x - r)];
                }
            }
        }

        private static void Vertical(float[] src, float[] dst, int rows, int r)
        {
            float inv = 1f / (2 * r + 1);
            for (int x = 0; x < W; x++)
            {
                float acc = 0f;
                for (int y = -r; y <= r; y++) acc += src[Math.Max(0, Math.Min(rows - 1, y)) * W + x];
                for (int y = 0; y < rows; y++)
                {
                    dst[y * W + x] = acc * inv;
                    acc += src[Math.Min(rows - 1, y + r + 1) * W + x] - src[Math.Max(0, y - r) * W + x];
                }
            }
        }
    }
}
