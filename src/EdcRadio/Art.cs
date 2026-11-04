using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace EdcRadio
{
    internal sealed class Art : IDisposable
    {
        public static readonly Rectangle Tower = new Rectangle(16, 112, 16, 16);
        public static readonly Rectangle Truck = new Rectangle(0, 176, 16, 16);
        public static readonly Rectangle Arrow = new Rectangle(112, 0, 16, 16);
        public static readonly Rectangle Pointer = new Rectangle(128, 0, 16, 16);
        public static readonly Rectangle HandOpen = new Rectangle(144, 0, 16, 16);
        public static readonly Rectangle HandClosed = new Rectangle(160, 0, 16, 16);

        private static readonly string[] Wanted = { "tiny-puter-ui", "nesFont", "LanaPixel_0", "notes", "jumpCattspr" };

        public Bitmap Ui { get; private set; }
        public BitmapFont Text { get; private set; }
        public BitmapFont Mono { get; private set; }
        public Bitmap Notes { get; private set; }
        public Bitmap Cats { get; private set; }
        public bool FromGame { get; private set; }

        public static string GameData()
        {
            foreach (string root in Library.SteamLibraries())
            {
                string path = Path.Combine(root, "steamapps", "common", "Easy Delivery Co", "EasyDeliveryCo_Data", "data.unity3d");
                if (File.Exists(path)) return path;
            }
            return null;
        }

        public static Art Load()
        {
            var art = new Art();
            string data = GameData();
            if (data != null)
            {
                try
                {
                    art.ReadGame(data);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidDataException
                                          || e is NotSupportedException || e is ArgumentException || e is IndexOutOfRangeException
                                          || e is OverflowException || e is FormatException || e is ExternalException
                                          || e is OutOfMemoryException || e is InvalidOperationException)
                {
                }
            }
            if (art.FromGame) return art;
            art.Dispose();
            var own = new Art();
            own.UseOwn();
            return own;
        }

        private void ReadGame(string path)
        {
            using (UnityBundle bundle = UnityBundle.Open(path))
            {
                var textures = new Dictionary<string, UnityTexture>();
                string xml = null;
                IEnumerable<UnityBundle.Node> files = bundle.Nodes
                    .Where(n => n.Name.EndsWith(".assets", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(n => n.Name.StartsWith("sharedassets", StringComparison.OrdinalIgnoreCase) ? 0 : 1);
                foreach (UnityBundle.Node node in files)
                {
                    if (textures.Count == Wanted.Length && xml != null) break;
                    UnityAsset asset = UnityAsset.Read(bundle, node);
                    if (asset == null) continue;
                    foreach (UnityAsset.Item item in asset.Items)
                    {
                        if (item.ClassId != UnityAsset.Texture2D && item.ClassId != UnityAsset.TextAsset) continue;
                        string name = UnityAsset.Name(asset.Head(item, 80));
                        if (item.ClassId == UnityAsset.Texture2D)
                        {
                            if (!Wanted.Contains(name) || textures.ContainsKey(name)) continue;
                            UnityTexture t = UnityTexture.Parse(asset.Bytes(item));
                            if (t != null) textures[name] = t;
                        }
                        else if (xml == null && name == "LanaPixel")
                        {
                            xml = UnityAsset.Script(asset.Bytes(item));
                        }
                    }
                }
                if (xml == null || Wanted.Take(3).Any(n => !textures.ContainsKey(n))) return;
                Ui = textures["tiny-puter-ui"].Decode(bundle);
                Mono = BitmapFont.FromGrid(Ink(textures["nesFont"].Decode(bundle)));
                UnityTexture lana = textures["LanaPixel_0"];
                Text = BitmapFont.FromBmFont(xml, crop => Ink(lana.Decode(bundle, crop)));
                if (textures.TryGetValue("notes", out UnityTexture notes)) Notes = notes.Decode(bundle);
                if (textures.TryGetValue("jumpCattspr", out UnityTexture cats)) Cats = cats.Decode(bundle);
                FromGame = Ui != null && Mono != null && Text != null;
            }
        }

        private static Bitmap Ink(Bitmap source)
        {
            if (source == null) return null;
            var rect = new Rectangle(0, 0, source.Width, source.Height);
            BitmapData lk = source.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var px = new int[source.Width * source.Height];
            for (int y = 0; y < source.Height; y++) Marshal.Copy(lk.Scan0 + y * lk.Stride, px, y * source.Width, source.Width);
            for (int i = 0; i < px.Length; i++)
            {
                int c = px[i];
                int lum = (((c >> 16) & 255) * 3 + ((c >> 8) & 255) * 6 + (c & 255)) / 10;
                px[i] = lum > 127 ? -1 : unchecked((int)0xFF000000);
            }
            for (int y = 0; y < source.Height; y++) Marshal.Copy(px, y * source.Width, lk.Scan0 + y * lk.Stride, source.Width);
            source.UnlockBits(lk);
            return source;
        }

        private static readonly Dictionary<char, string[]> OwnMono = new Dictionary<char, string[]>
        {
            [' '] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." },
            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['3'] = new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." },
            ['.'] = new[] { ".....", ".....", ".....", ".....", ".....", ".##..", ".##.." },
            ['-'] = new[] { ".....", ".....", ".....", ".###.", ".....", ".....", "....." },
            [':'] = new[] { ".....", ".##..", ".##..", ".....", ".##..", ".##..", "....." },
            ['/'] = new[] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." },
            ['['] = new[] { "###..", "#....", "#....", "#....", "#....", "#....", "###.." },
            [']'] = new[] { "..###", "....#", "....#", "....#", "....#", "....#", "..###" },
            ['+'] = new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." },
            ['?'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." },
            ['<'] = new[] { "...#.", "..#..", ".#...", "#....", ".#...", "..#..", "...#." },
            ['>'] = new[] { ".#...", "..#..", "...#.", "....#", "...#.", "..#..", ".#..." },
            ['_'] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "#####" },
            ['x'] = new[] { ".....", ".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#" },
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
        };

        private void UseOwn()
        {
            Ui = OwnUi();
            Text = BitmapFont.FromSystem("Tahoma", 9f, 13, 1);
            Mono = BitmapFont.FromCells(OwnMono, 8, 6);
            Notes = null;
            Cats = null;
            FromGame = false;
        }

        private static Bitmap OwnUi()
        {
            var sheet = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(sheet))
            using (var black = new SolidBrush(Color.Black))
            using (var white = new SolidBrush(Color.White))
            {
                g.Clear(Color.Transparent);
                g.FillRectangle(black, 0, 0, 24, 24);
                g.FillRectangle(white, 5, 4, 14, 1);
                g.FillRectangle(white, 5, 19, 14, 1);
                g.FillRectangle(white, 4, 5, 1, 14);
                g.FillRectangle(white, 19, 5, 1, 14);
                g.FillRectangle(black, 24, 16, 24, 8);
                g.FillRectangle(white, 29, 19, 14, 1);
                g.FillRectangle(white, 28, 20, 16, 2);
                g.FillRectangle(white, 28, 22, 1, 2);
                g.FillRectangle(white, 43, 22, 1, 2);
            }
            return sheet;
        }

        public void Dispose()
        {
            Ui?.Dispose();
            Text?.Dispose();
            Mono?.Dispose();
            Notes?.Dispose();
            Cats?.Dispose();
        }
    }
}
