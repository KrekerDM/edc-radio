using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace EdcRadio
{
    internal enum Align { Left, Center, Right }

    internal sealed class Canvas : IDisposable
    {
        public const int W = 320;
        public const int H = 208;

        public readonly Bitmap Bitmap = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
        public readonly Graphics G;
        public readonly Art Art;
        private readonly SolidBrush _white = new SolidBrush(Color.White);
        private readonly SolidBrush _black = new SolidBrush(Color.Black);
        private readonly ImageAttributes _clear = new ImageAttributes();

        public Canvas(Art art)
        {
            Art = art;
            _clear.SetColorKey(Color.Black, Color.Black);
            G = Graphics.FromImage(Bitmap);
            G.InterpolationMode = InterpolationMode.NearestNeighbor;
            G.PixelOffsetMode = PixelOffsetMode.Half;
            G.SmoothingMode = SmoothingMode.None;
            G.CompositingQuality = CompositingQuality.HighSpeed;
        }

        public void Clear() => G.Clear(Color.Black);

        public void Fill(int x, int y, int w, int h, bool white = true)
        {
            if (w > 0 && h > 0) G.FillRectangle(white ? _white : _black, x, y, w, h);
        }

        public void Dot(int x, int y, bool white = true) => G.FillRectangle(white ? _white : _black, x, y, 1, 1);

        public void HLine(int x0, int x1, int y) => Fill(Math.Min(x0, x1), y, Math.Abs(x1 - x0) + 1, 1);

        public void VLine(int x, int y0, int y1) => Fill(x, Math.Min(y0, y1), 1, Math.Abs(y1 - y0) + 1);

        public void Dotted(int x0, int x1, int y)
        {
            for (int x = x0; x <= x1; x += 2) Dot(x, y);
        }

        public void Sprite(Bitmap sheet, Rectangle source, int x, int y)
        {
            if (sheet != null) G.DrawImage(sheet, new Rectangle(x, y, source.Width, source.Height), source, GraphicsUnit.Pixel);
        }

        public void Tile(int sx, int sy, int x, int y) => Sprite(Art.Ui, new Rectangle(sx, sy, 8, 8), x, y);

        public void Box(int x, int y, int w, int h, bool top = false)
        {
            int tx = top ? 24 : 0, ty = top ? 16 : 0;
            for (int i = 1; i < w; i++)
            {
                Tile(8 + tx, ty, x + i * 8, y);
                Tile(8, 16, x + i * 8, y + h * 8);
            }
            for (int j = 1; j < h; j++)
            {
                Tile(0, 8, x, y + j * 8);
                Tile(16, 8, x + w * 8, y + j * 8);
            }
            Tile(0, 16, x, y + h * 8);
            Tile(16, 16, x + w * 8, y + h * 8);
            Tile(tx, ty, x, y);
            Tile(16 + tx, ty, x + w * 8, y);
        }

        public void Panel(int x, int y, int w, int h, bool top = false)
        {
            Fill(x, y, (w + 1) * 8, (h + 1) * 8, white: false);
            Box(x, y, w, h, top);
        }

        public int Measure(string text) => Art.Text.Measure(text);

        public int MeasureMono(string text, int scale = 1) => Art.Mono.Measure(text) * scale;

        public int Text(string text, int x, int y, Align align = Align.Left, bool clear = false) => Put(Art.Text, text, x, y, align, 1, clear);

        public int Mono(string text, int x, int y, Align align = Align.Left, int scale = 1, bool clear = false) => Put(Art.Mono, text, x, y, align, scale, clear);

        public string Fit(string text, int width)
        {
            if (string.IsNullOrEmpty(text) || Measure(text) <= width) return text ?? "";
            string cut = text;
            while (cut.Length > 1 && Measure(cut.TrimEnd() + "…") > width) cut = cut.Substring(0, cut.Length - 1);
            return cut.TrimEnd() + "…";
        }

        private int Put(BitmapFont font, string text, int x, int y, Align align, int scale, bool clear)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            Bitmap b = font.Render(text);
            int w = font.Measure(text) * scale;
            int left = align == Align.Left ? x : align == Align.Center ? x - w / 2 : x - w;
            var dest = new Rectangle(left, y, b.Width * scale, b.Height * scale);
            if (clear) G.DrawImage(b, dest, 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, _clear);
            else G.DrawImage(b, dest, 0, 0, b.Width, b.Height, GraphicsUnit.Pixel);
            return w;
        }

        public static Bitmap Dither(Image source, int w, int h)
        {
            int[,] bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
            var small = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(small))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float scale = Math.Max((float)w / source.Width, (float)h / source.Height);
                float sw = source.Width * scale, sh = source.Height * scale;
                g.DrawImage(source, (w - sw) / 2f, (h - sh) / 2f, sw, sh);
            }
            float lo = 1f, hi = 0f;
            var lum = new float[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = small.GetPixel(x, y);
                    float l = (0.3f * c.R + 0.59f * c.G + 0.11f * c.B) / 255f;
                    lum[x, y] = l;
                    lo = Math.Min(lo, l);
                    hi = Math.Max(hi, l);
                }
            float span = Math.Max(0.05f, hi - lo);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float l = (lum[x, y] - lo) / span;
                    small.SetPixel(x, y, l * 16f > bayer[y % 4, x % 4] + 0.5f ? Color.White : Color.Black);
                }
            return small;
        }

        public void Dispose()
        {
            G.Dispose();
            Bitmap.Dispose();
            _white.Dispose();
            _black.Dispose();
            _clear.Dispose();
        }
    }
}
