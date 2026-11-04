using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdcRadio
{
    internal sealed class BitmapFont : IDisposable
    {
        private sealed class Glyph
        {
            public Rectangle Source;
            public int OffsetX;
            public int OffsetY;
            public int Advance;
        }

        private readonly Dictionary<int, Glyph> _glyphs = new Dictionary<int, Glyph>();
        private readonly Dictionary<string, Bitmap> _cache = new Dictionary<string, Bitmap>();
        private Bitmap _sheet;

        public int Height { get; private set; }

        public static bool Wanted(int c) =>
            (c >= 32 && c < 127) || (c >= 160 && c < 384) || (c >= 0x400 && c < 0x500)
            || (c >= 0x2010 && c < 0x2070) || c == 0x2116 || (c >= 0x2190 && c < 0x2196) || c == 0x20AC;

        public static BitmapFont FromGrid(Bitmap sheet, int cell = 8, int first = 32)
        {
            if (sheet == null) return null;
            int columns = sheet.Width / cell, rows = sheet.Height / cell;
            if (columns * rows < 127 - first)
            {
                sheet.Dispose();
                return null;
            }
            var font = new BitmapFont { _sheet = sheet, Height = cell };
            for (int i = 0; i < columns * rows; i++)
                font._glyphs[first + i] = new Glyph
                {
                    Source = new Rectangle(i % columns * cell, i / columns * cell, cell, cell),
                    Advance = cell,
                };
            return font;
        }

        public static BitmapFont FromBmFont(string xml, Func<Rectangle, Bitmap> crop)
        {
            Match common = Regex.Match(xml, "<common[^>]*\\blineHeight=\"([0-9]+)\"");
            var raw = new List<(int id, Rectangle r, int ox, int oy, int adv)>();
            foreach (Match m in Regex.Matches(xml, "<char\\s+id=\"([0-9]+)\"\\s+x=\"([0-9]+)\"\\s+y=\"([0-9]+)\"\\s+width=\"([0-9]+)\"\\s+height=\"([0-9]+)\"\\s+xoffset=\"(-?[0-9]+)\"\\s+yoffset=\"(-?[0-9]+)\"\\s+xadvance=\"(-?[0-9]+)\""))
            {
                int id = Int(m, 1);
                if (!Wanted(id)) continue;
                raw.Add((id, new Rectangle(Int(m, 2), Int(m, 3), Int(m, 4), Int(m, 5)), Int(m, 6), Int(m, 7), Int(m, 8)));
            }
            if (!raw.Any(g => g.id == '?') || !raw.Any(g => g.r.Width > 0 && g.r.Height > 0)) return null;
            Rectangle box = raw.Where(g => g.r.Width > 0 && g.r.Height > 0).Select(g => g.r).Aggregate(Rectangle.Union);
            int line = common.Success ? Math.Max(6, Math.Min(64, Int(common, 1))) : 13;
            const int width = 512;
            int rowHeight = Math.Max(line, raw.Max(g => g.r.Height));
            int px = 0, py = 0;
            var places = new Point[raw.Count];
            for (int i = 0; i < raw.Count; i++)
            {
                if (px + raw[i].r.Width > width)
                {
                    px = 0;
                    py += rowHeight;
                }
                places[i] = new Point(px, py);
                px += raw[i].r.Width;
            }
            var font = new BitmapFont { Height = line };
            using (Bitmap source = crop(box))
            {
                if (source == null) return null;
                font._sheet = new Bitmap(width, py + rowHeight, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(font._sheet))
                {
                    g.Clear(Color.Black);
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    for (int i = 0; i < raw.Count; i++)
                    {
                        Rectangle r = raw[i].r;
                        if (r.Width > 0 && r.Height > 0)
                            g.DrawImage(source, new Rectangle(places[i], r.Size), new Rectangle(r.X - box.X, r.Y - box.Y, r.Width, r.Height), GraphicsUnit.Pixel);
                    }
                }
            }
            for (int i = 0; i < raw.Count; i++)
                font._glyphs[raw[i].id] = new Glyph
                {
                    Source = new Rectangle(places[i], raw[i].r.Size),
                    OffsetX = raw[i].ox,
                    OffsetY = raw[i].oy,
                    Advance = raw[i].adv,
                };
            return font;
        }

        public static BitmapFont FromSystem(string family, float size, int height, int top)
        {
            int[] chars = Enumerable.Range(32, 95).Concat(Enumerable.Range(0x401, 0x5F))
                .Concat(new[] { 0xAB, 0xBB, 0xB7, 0xD7, 0xB0, 0x2013, 0x2014, 0x2026, 0x2116, 0x2190, 0x2191, 0x2192, 0x2193 })
                .ToArray();
            using (var font = new Font(family, size, GraphicsUnit.Pixel))
            using (var format = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap })
            using (var probe = new Bitmap(1, 1))
            using (Graphics pg = Graphics.FromImage(probe))
            {
                pg.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                var advances = chars.Select(c => Math.Max(2, (int)Math.Round(pg.MeasureString(char.ConvertFromUtf32(c), font, PointF.Empty, format).Width))).ToArray();
                int cellW = advances.Max() + 2;
                const int columns = 32;
                int rows = (chars.Length + columns - 1) / columns;
                var sheet = new Bitmap(columns * cellW, rows * height, PixelFormat.Format32bppArgb);
                var result = new BitmapFont { _sheet = sheet, Height = height };
                using (Graphics g = Graphics.FromImage(sheet))
                using (var white = new SolidBrush(Color.White))
                {
                    g.Clear(Color.Black);
                    g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                    for (int i = 0; i < chars.Length; i++)
                    {
                        int x = i % columns * cellW, y = i / columns * height;
                        g.SetClip(new Rectangle(x, y, cellW, height));
                        g.DrawString(char.ConvertFromUtf32(chars[i]), font, white, x, y + top, format);
                        result._glyphs[chars[i]] = new Glyph { Source = new Rectangle(x, y, advances[i] + 1, height), Advance = advances[i] };
                    }
                }
                return result;
            }
        }

        public static BitmapFont FromCells(IReadOnlyDictionary<char, string[]> cells, int cell, int advance)
        {
            const int columns = 16;
            int rows = (cells.Count + columns - 1) / columns;
            var sheet = new Bitmap(columns * cell, rows * cell, PixelFormat.Format32bppArgb);
            var font = new BitmapFont { _sheet = sheet, Height = cell };
            using (Graphics g = Graphics.FromImage(sheet))
                g.Clear(Color.Black);
            int i = 0;
            foreach (KeyValuePair<char, string[]> pair in cells)
            {
                int ox = i % columns * cell, oy = i / columns * cell;
                for (int y = 0; y < pair.Value.Length && y < cell; y++)
                    for (int x = 0; x < pair.Value[y].Length && x < cell; x++)
                        if (pair.Value[y][x] == '#') sheet.SetPixel(ox + x, oy + y, Color.White);
                font._glyphs[pair.Key] = new Glyph { Source = new Rectangle(ox, oy, advance, cell), Advance = advance };
                i++;
            }
            return font;
        }

        private static int Int(Match m, int group) => int.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);

        private Glyph Find(char c)
        {
            if (_glyphs.TryGetValue(c, out Glyph g)) return g;
            if (_glyphs.TryGetValue(char.ToLowerInvariant(c), out g)) return g;
            _glyphs.TryGetValue('?', out g);
            return g;
        }

        public bool Has(char c) => _glyphs.ContainsKey(c);

        public int Measure(string text)
        {
            int w = 0;
            foreach (char c in text ?? "")
            {
                Glyph g = Find(c);
                if (g != null) w += g.Advance;
            }
            return w;
        }

        public int Fits(string text, int width)
        {
            int w = 0;
            for (int i = 0; i < text.Length; i++)
            {
                Glyph g = Find(text[i]);
                if (g == null) continue;
                if (w + g.Advance > width) return i;
                w += g.Advance;
            }
            return text.Length;
        }

        public Bitmap Render(string text)
        {
            if (_cache.TryGetValue(text, out Bitmap hit)) return hit;
            if (_cache.Count > 400)
            {
                foreach (Bitmap b in _cache.Values) b.Dispose();
                _cache.Clear();
            }
            int width = 1;
            int x = 0;
            foreach (char c in text)
            {
                Glyph g = Find(c);
                if (g == null) continue;
                width = Math.Max(width, x + g.OffsetX + g.Source.Width);
                x += g.Advance;
            }
            width = Math.Max(width, x);
            var bmp = new Bitmap(width, Height, PixelFormat.Format32bppPArgb);
            using (Graphics gr = Graphics.FromImage(bmp))
            {
                gr.Clear(Color.Black);
                gr.InterpolationMode = InterpolationMode.NearestNeighbor;
                gr.PixelOffsetMode = PixelOffsetMode.Half;
                gr.CompositingMode = CompositingMode.SourceCopy;
                x = 0;
                foreach (char c in text)
                {
                    Glyph g = Find(c);
                    if (g == null) continue;
                    gr.DrawImage(_sheet, new Rectangle(x + g.OffsetX, g.OffsetY, g.Source.Width, g.Source.Height), g.Source, GraphicsUnit.Pixel);
                    x += g.Advance;
                }
            }
            _cache[text] = bmp;
            return bmp;
        }

        public void Dispose()
        {
            foreach (Bitmap b in _cache.Values) b.Dispose();
            _cache.Clear();
            _sheet?.Dispose();
        }
    }
}
