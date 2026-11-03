using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace EdcRadio
{
    internal sealed class UnityBundle : IDisposable
    {
        private sealed class Block
        {
            public long Start;
            public int Size;
            public long Packed;
            public int PackedSize;
            public int Kind;
        }

        public sealed class Node
        {
            public string Name;
            public long Offset;
            public long Size;
        }

        private readonly FileStream _file;
        private readonly List<Block> _blocks = new List<Block>();
        private readonly Dictionary<int, byte[]> _cache = new Dictionary<int, byte[]>();
        private readonly Queue<int> _order = new Queue<int>();
        public readonly List<Node> Nodes = new List<Node>();

        private UnityBundle(FileStream file)
        {
            _file = file;
        }

        public static UnityBundle Open(string path)
        {
            var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
            var bundle = new UnityBundle(file);
            try
            {
                bundle.ReadHeader();
                return bundle;
            }
            catch
            {
                bundle.Dispose();
                throw;
            }
        }

        private void ReadHeader()
        {
            var r = new BinaryReader(_file);
            if (CString(r) != "UnityFS") throw new InvalidDataException("not a UnityFS bundle");
            uint version = Be32(r.ReadBytes(4), 0);
            CString(r);
            CString(r);
            long size = (long)Be64(r.ReadBytes(8), 0);
            byte[] head = r.ReadBytes(12);
            int packedInfo = (int)Be32(head, 0), infoSize = (int)Be32(head, 4);
            uint flags = Be32(head, 8);
            if (packedInfo <= 0 || infoSize <= 0 || packedInfo > _file.Length || infoSize > 64 << 20)
                throw new InvalidDataException("bad block info size");
            if (version >= 7) Align(16);
            byte[] raw;
            if ((flags & 0x80) != 0)
            {
                long back = _file.Position;
                _file.Position = size - packedInfo;
                raw = r.ReadBytes(packedInfo);
                _file.Position = back;
            }
            else
            {
                raw = r.ReadBytes(packedInfo);
            }
            byte[] info = Unpack(raw, packedInfo, infoSize, (int)(flags & 0x3F));
            if ((flags & 0x200) != 0) Align(16);
            long packed = _file.Position;
            int p = 16;
            int blocks = (int)Be32(info, p);
            p += 4;
            long start = 0;
            for (int i = 0; i < blocks; i++)
            {
                var b = new Block
                {
                    Start = start,
                    Size = (int)Be32(info, p),
                    PackedSize = (int)Be32(info, p + 4),
                    Kind = ((info[p + 8] << 8) | info[p + 9]) & 0x3F,
                    Packed = packed,
                };
                if (b.Size <= 0 || b.Size > 64 << 20 || b.PackedSize <= 0 || b.PackedSize > 64 << 20)
                    throw new InvalidDataException("bad block size");
                p += 10;
                _blocks.Add(b);
                start += b.Size;
                packed += b.PackedSize;
            }
            int nodes = (int)Be32(info, p);
            p += 4;
            for (int i = 0; i < nodes; i++)
            {
                var n = new Node { Offset = (long)Be64(info, p), Size = (long)Be64(info, p + 8) };
                p += 20;
                int end = Array.IndexOf(info, (byte)0, p);
                n.Name = Encoding.UTF8.GetString(info, p, end - p);
                p = end + 1;
                Nodes.Add(n);
            }
        }

        public Node Find(string name) =>
            Nodes.FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));

        public byte[] Read(long offset, int count)
        {
            if (count < 0 || count > 256 << 20 || offset < 0) throw new InvalidDataException("bad read");
            var result = new byte[count];
            int done = 0;
            int index = BlockAt(offset);
            while (done < count && index < _blocks.Count)
            {
                Block b = _blocks[index];
                byte[] data = Data(index);
                int from = (int)(offset + done - b.Start);
                int n = Math.Min(count - done, b.Size - from);
                Buffer.BlockCopy(data, from, result, done, n);
                done += n;
                index++;
            }
            if (done < count) throw new EndOfStreamException();
            return result;
        }

        private int BlockAt(long offset)
        {
            int lo = 0, hi = _blocks.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_blocks[mid].Start <= offset) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        private byte[] Data(int index)
        {
            if (_cache.TryGetValue(index, out byte[] hit)) return hit;
            Block b = _blocks[index];
            _file.Position = b.Packed;
            var raw = new byte[b.PackedSize];
            int read = 0;
            while (read < raw.Length)
            {
                int n = _file.Read(raw, read, raw.Length - read);
                if (n <= 0) throw new EndOfStreamException();
                read += n;
            }
            byte[] data = Unpack(raw, b.PackedSize, b.Size, b.Kind);
            _cache[index] = data;
            _order.Enqueue(index);
            if (_order.Count > 24) _cache.Remove(_order.Dequeue());
            return data;
        }

        private static byte[] Unpack(byte[] raw, int packed, int size, int kind)
        {
            if (kind == 0) return raw;
            if (kind != 2 && kind != 3) throw new NotSupportedException("bundle compression " + kind);
            var dst = new byte[size];
            int s = 0, d = 0;
            while (s < packed)
            {
                int token = raw[s++];
                int literal = token >> 4;
                if (literal == 15)
                {
                    int x;
                    do { x = raw[s++]; literal += x; } while (x == 255);
                }
                Buffer.BlockCopy(raw, s, dst, d, literal);
                s += literal;
                d += literal;
                if (s >= packed) break;
                int back = raw[s] | (raw[s + 1] << 8);
                s += 2;
                int length = token & 15;
                if (length == 15)
                {
                    int x;
                    do { x = raw[s++]; length += x; } while (x == 255);
                }
                length += 4;
                int from = d - back;
                if (from < 0) throw new InvalidDataException("broken LZ4 block");
                if (back >= length)
                {
                    Buffer.BlockCopy(dst, from, dst, d, length);
                    d += length;
                }
                else
                {
                    for (int i = 0; i < length; i++) dst[d++] = dst[from + i];
                }
            }
            return dst;
        }

        private void Align(int n)
        {
            long pad = (n - _file.Position % n) % n;
            _file.Position += pad;
        }

        private static string CString(BinaryReader r)
        {
            var bytes = new List<byte>();
            byte c;
            while ((c = r.ReadByte()) != 0 && bytes.Count < 256) bytes.Add(c);
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        public static uint Be32(byte[] b, int p) => (uint)((b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3]);

        public static ulong Be64(byte[] b, int p) => ((ulong)Be32(b, p) << 32) | Be32(b, p + 4);

        public void Dispose() => _file.Dispose();
    }

    internal sealed class UnityAsset
    {
        public sealed class Item
        {
            public long PathId;
            public long Start;
            public int Size;
            public int ClassId;
        }

        public const int Texture2D = 28;
        public const int TextAsset = 49;

        public readonly UnityBundle Bundle;
        public readonly UnityBundle.Node Node;
        public readonly List<Item> Items = new List<Item>();
        private long _data;

        private UnityAsset(UnityBundle bundle, UnityBundle.Node node)
        {
            Bundle = bundle;
            Node = node;
        }

        public static UnityAsset Read(UnityBundle bundle, UnityBundle.Node node)
        {
            var asset = new UnityAsset(bundle, node);
            return asset.Parse() ? asset : null;
        }

        public byte[] Bytes(Item item) => Bundle.Read(Node.Offset + _data + item.Start, item.Size);

        public byte[] Head(Item item, int count) => Bundle.Read(Node.Offset + _data + item.Start, Math.Min(count, item.Size));

        private bool Parse()
        {
            if (Node.Size < 48) return false;
            byte[] head = Bundle.Read(Node.Offset, 48);
            uint version = UnityBundle.Be32(head, 8);
            if (version < 17 || version > 30) return false;
            long metaSize, data;
            int start;
            if (version >= 22)
            {
                metaSize = UnityBundle.Be32(head, 20);
                data = (long)UnityBundle.Be64(head, 32);
                start = 48;
            }
            else
            {
                metaSize = UnityBundle.Be32(head, 0);
                data = UnityBundle.Be32(head, 12);
                start = 20;
            }
            if (head[16] != 0 || metaSize <= 0 || start + metaSize > Node.Size || metaSize > 64 << 20) return false;
            _data = data;
            byte[] b = Bundle.Read(Node.Offset, (int)(start + metaSize));
            int p = start;
            while (b[p] != 0) p++;
            p++;
            p += 4;
            bool tree = b[p++] != 0;
            int types = BitConverter.ToInt32(b, p);
            p += 4;
            if (types < 0 || types > 100000) return false;
            var classes = new int[types];
            for (int i = 0; i < types; i++)
            {
                int classId = BitConverter.ToInt32(b, p);
                p += 7;
                if (classId == 114) p += 16;
                p += 16;
                if (tree)
                {
                    int nodes = BitConverter.ToInt32(b, p);
                    int strings = BitConverter.ToInt32(b, p + 4);
                    p += 8 + nodes * (version >= 19 ? 32 : 24) + strings;
                    if (version >= 21)
                    {
                        int deps = BitConverter.ToInt32(b, p);
                        p += 4 + deps * 4;
                    }
                }
                classes[i] = classId;
            }
            int count = BitConverter.ToInt32(b, p);
            p += 4;
            if (count < 0 || count > 10000000) return false;
            for (int i = 0; i < count; i++)
            {
                p = (p + 3) & ~3;
                var item = new Item { PathId = BitConverter.ToInt64(b, p) };
                p += 8;
                if (version >= 22)
                {
                    item.Start = BitConverter.ToInt64(b, p);
                    p += 8;
                }
                else
                {
                    item.Start = BitConverter.ToUInt32(b, p);
                    p += 4;
                }
                uint size = BitConverter.ToUInt32(b, p);
                if (size > Node.Size) return false;
                item.Size = (int)size;
                int type = BitConverter.ToInt32(b, p + 4);
                p += 8;
                item.ClassId = type >= 0 && type < classes.Length ? classes[type] : -1;
                Items.Add(item);
            }
            return true;
        }

        public static string Name(byte[] b)
        {
            if (b.Length < 4) return "";
            int n = BitConverter.ToInt32(b, 0);
            return n > 0 && n < 256 && 4 + n <= b.Length ? Encoding.UTF8.GetString(b, 4, n) : "";
        }

        public static int AfterName(byte[] b) => (4 + BitConverter.ToInt32(b, 0) + 3) & ~3;

        public static string Script(byte[] b)
        {
            int p = AfterName(b);
            if (p + 4 > b.Length) return null;
            int n = BitConverter.ToInt32(b, p);
            return n > 0 && p + 4 + n <= b.Length ? Encoding.UTF8.GetString(b, p + 4, n) : null;
        }
    }

    internal sealed class UnityTexture
    {
        public string Name;
        public int Width;
        public int Height;
        public int Format;
        public int Complete;
        public byte[] Inline;
        public string Stream;
        public long StreamOffset;
        public int StreamSize;

        public static UnityTexture Parse(byte[] b)
        {
            var t = new UnityTexture { Name = UnityAsset.Name(b) };
            int p = UnityAsset.AfterName(b) + 4;
            if (p + 24 > b.Length) return null;
            t.Width = BitConverter.ToInt32(b, p);
            t.Height = BitConverter.ToInt32(b, p + 4);
            t.Complete = BitConverter.ToInt32(b, p + 8);
            t.Format = BitConverter.ToInt32(b, p + 16);
            if (t.Width <= 0 || t.Height <= 0 || t.Width > 8192 || t.Height > 8192 || t.Complete <= 0 || t.Complete > 256 << 20) return null;
            int dot = LastIndexOf(b, Encoding.ASCII.GetBytes(".res"));
            if (dot > 0)
            {
                for (int q = dot - 1; q >= Math.Max(24, dot - 256); q--)
                {
                    int n = BitConverter.ToInt32(b, q - 4);
                    if (n <= 0 || q + n > b.Length || q + n < dot + 5) continue;
                    if (BitConverter.ToInt32(b, q - 20) != 0) continue;
                    t.Stream = Encoding.UTF8.GetString(b, q, n);
                    t.StreamSize = BitConverter.ToInt32(b, q - 8);
                    t.StreamOffset = (long)BitConverter.ToUInt64(b, q - 16);
                    return t.StreamSize > 0 && t.StreamOffset >= 0 ? t : null;
                }
            }
            for (int k = 1; k <= 6; k++)
            {
                long size = (long)t.Complete * k;
                long at = b.Length - 16 - ((size + 3) & ~3L) - 4;
                if (at < p || BitConverter.ToInt32(b, (int)at) != size) continue;
                t.Inline = new byte[size];
                Buffer.BlockCopy(b, (int)at + 4, t.Inline, 0, (int)size);
                return t;
            }
            return null;
        }

        private static int LastIndexOf(byte[] b, byte[] pattern)
        {
            for (int i = b.Length - pattern.Length; i >= 0; i--)
            {
                int j = 0;
                while (j < pattern.Length && b[i + j] == pattern[j]) j++;
                if (j == pattern.Length) return i;
            }
            return -1;
        }

        private byte[] Pixels(UnityBundle bundle)
        {
            if (Inline != null) return Inline;
            UnityBundle.Node node = bundle.Find(Path.GetFileName(Stream.Replace('\\', '/')));
            if (node == null || StreamOffset < 0 || StreamOffset + StreamSize > node.Size) return null;
            return bundle.Read(node.Offset + StreamOffset, StreamSize);
        }

        public Bitmap Decode(UnityBundle bundle) => Decode(bundle, new Rectangle(0, 0, Width, Height));

        public Bitmap Decode(UnityBundle bundle, Rectangle crop)
        {
            crop.Intersect(new Rectangle(0, 0, Width, Height));
            if (crop.Width <= 0 || crop.Height <= 0) return null;
            byte[] d = Pixels(bundle);
            if (d == null) return null;
            var argb = new int[crop.Width * crop.Height];
            if (Format == 10 || Format == 12)
            {
                var full = new int[Width * Height];
                if (!Dxt(d, full, Format == 12)) return null;
                for (int y = 0; y < crop.Height; y++)
                    Array.Copy(full, (Height - 1 - (crop.Y + y)) * Width + crop.X, argb, y * crop.Width, crop.Width);
            }
            else
            {
                int bpp = Bytes(Format);
                if (bpp == 0 || d.Length < (long)Width * Height * bpp) return null;
                for (int y = 0; y < crop.Height; y++)
                {
                    int row = (Height - 1 - (crop.Y + y)) * Width;
                    for (int x = 0; x < crop.Width; x++)
                        argb[y * crop.Width + x] = Argb(d, (row + crop.X + x) * bpp);
                }
            }
            var bmp = new Bitmap(crop.Width, crop.Height, PixelFormat.Format32bppArgb);
            BitmapData lk = bmp.LockBits(new Rectangle(0, 0, crop.Width, crop.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < crop.Height; y++)
                Marshal.Copy(argb, y * crop.Width, lk.Scan0 + y * lk.Stride, crop.Width);
            bmp.UnlockBits(lk);
            return bmp;
        }

        private static int Bytes(int format)
        {
            switch (format)
            {
                case 1:
                case 63:
                    return 1;
                case 2:
                case 13:
                    return 2;
                case 3:
                    return 3;
                case 4:
                case 5:
                case 14:
                    return 4;
                default:
                    return 0;
            }
        }

        private int Argb(byte[] d, int i)
        {
            const int opaque = unchecked((int)0xFF000000);
            switch (Format)
            {
                case 1:
                    return (d[i] << 24) | 0xFFFFFF;
                case 63:
                    return opaque | (d[i] << 16) | (d[i] << 8) | d[i];
                case 3:
                    return opaque | (d[i] << 16) | (d[i + 1] << 8) | d[i + 2];
                case 4:
                    return (d[i + 3] << 24) | (d[i] << 16) | (d[i + 1] << 8) | d[i + 2];
                case 5:
                    return (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];
                case 14:
                    return (d[i + 3] << 24) | (d[i + 2] << 16) | (d[i + 1] << 8) | d[i];
                case 13:
                {
                    int v = d[i] | (d[i + 1] << 8);
                    return (Nib(v) << 24) | (Nib(v >> 12) << 16) | (Nib(v >> 8) << 8) | Nib(v >> 4);
                }
                case 2:
                {
                    int v = d[i] | (d[i + 1] << 8);
                    return (Nib(v >> 12) << 24) | (Nib(v >> 8) << 16) | (Nib(v >> 4) << 8) | Nib(v);
                }
                default:
                    return 0;
            }
        }

        private static int Nib(int v) => (v & 0xF) * 17;

        private bool Dxt(byte[] d, int[] o, bool alpha)
        {
            int bw = (Width + 3) / 4, bh = (Height + 3) / 4, size = alpha ? 16 : 8;
            if (d.Length < bw * bh * size) return false;
            var colors = new int[4];
            var alphas = new int[8];
            for (int by = 0; by < bh; by++)
                for (int bx = 0; bx < bw; bx++)
                {
                    int p = (by * bw + bx) * size;
                    ulong abits = 0;
                    if (alpha)
                    {
                        alphas[0] = d[p];
                        alphas[1] = d[p + 1];
                        for (int i = 2; i < 8; i++)
                            alphas[i] = alphas[0] > alphas[1]
                                ? ((8 - i) * alphas[0] + (i - 1) * alphas[1]) / 7
                                : i < 6 ? ((6 - i) * alphas[0] + (i - 1) * alphas[1]) / 5 : i == 6 ? 0 : 255;
                        for (int i = 0; i < 6; i++) abits |= (ulong)d[p + 2 + i] << (8 * i);
                        p += 8;
                    }
                    int c0 = d[p] | (d[p + 1] << 8), c1 = d[p + 2] | (d[p + 3] << 8);
                    colors[0] = Rgb565(c0);
                    colors[1] = Rgb565(c1);
                    if (c0 > c1 || alpha)
                    {
                        colors[2] = Mix(colors[0], colors[1], 2, 1, 3);
                        colors[3] = Mix(colors[0], colors[1], 1, 2, 3);
                    }
                    else
                    {
                        colors[2] = Mix(colors[0], colors[1], 1, 1, 2);
                        colors[3] = 0;
                    }
                    uint bits = BitConverter.ToUInt32(d, p + 4);
                    for (int i = 0; i < 16; i++)
                    {
                        int x = bx * 4 + i % 4, y = by * 4 + i / 4;
                        if (x >= Width || y >= Height) continue;
                        int c = colors[(bits >> (2 * i)) & 3];
                        if (alpha) c = (c & 0xFFFFFF) | (alphas[(int)((abits >> (3 * i)) & 7)] << 24);
                        o[y * Width + x] = c;
                    }
                }
            return true;
        }

        private static int Rgb565(int v)
        {
            int r = (v >> 11) & 31, g = (v >> 5) & 63, b = v & 31;
            return unchecked((int)0xFF000000) | ((r * 255 / 31) << 16) | ((g * 255 / 63) << 8) | (b * 255 / 31);
        }

        private static int Mix(int a, int b, int wa, int wb, int div)
        {
            int r = (((a >> 16) & 255) * wa + ((b >> 16) & 255) * wb) / div;
            int g = (((a >> 8) & 255) * wa + ((b >> 8) & 255) * wb) / div;
            int bl = ((a & 255) * wa + (b & 255) * wb) / div;
            return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | bl;
        }
    }
}
