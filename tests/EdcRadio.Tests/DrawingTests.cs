using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace EdcRadio.Tests
{
    public sealed class DrawingTests
    {
        private static Art OwnArt()
        {
            var art = (Art)Activator.CreateInstance(typeof(Art), true);
            typeof(Art).GetMethod("UseOwn", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(art, null);
            return art;
        }

        [Fact]
        public void FitKeepsTextInsideTheWidth()
        {
            using (Art art = OwnArt())
            using (var canvas = new Canvas(art))
            {
                foreach (string text in new[] { "", null, "short", new string('ж', 400), "Freight Across The Galaxy" })
                {
                    string fit = canvas.Fit(text, 120);
                    Assert.NotNull(fit);
                    Assert.True(canvas.Measure(fit) <= 120 || fit.Length <= 1);
                }
            }
        }

        [Fact]
        public void FitIsFastOnHugeText()
        {
            using (Art art = OwnArt())
            using (var canvas = new Canvas(art))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                string fit = canvas.Fit(new string('a', 200000), 100);
                Assert.True(watch.ElapsedMilliseconds < 1000);
                Assert.True(canvas.Measure(fit) <= 100);
                Assert.EndsWith("…", fit);
            }
        }

        [Fact]
        public void GridFontFollowsTheSheetWidth()
        {
            var sheet = new Bitmap(128, 48);
            sheet.SetPixel(1 * 8, 2 * 8, Color.White);
            using (BitmapFont font = BitmapFont.FromGrid(sheet))
            {
                Assert.NotNull(font);
                Assert.Equal(Color.White.ToArgb(), font.Render("A").GetPixel(0, 0).ToArgb());
            }
        }

        [Fact]
        public void GridFontRejectsTooSmallSheets()
        {
            Assert.Null(BitmapFont.FromGrid(new Bitmap(16, 8)));
        }

        [Fact]
        public void BmFontClampsLineHeightAndSkipsWideDigits()
        {
            const string xml = "<font><common lineHeight=\"0\" base=\"0\"/>"
                + "<char id=\"63\" x=\"0\" y=\"0\" width=\"4\" height=\"6\" xoffset=\"0\" yoffset=\"0\" xadvance=\"5\"/>"
                + "<char id=\"６５\" x=\"0\" y=\"0\" width=\"4\" height=\"6\" xoffset=\"0\" yoffset=\"0\" xadvance=\"5\"/></font>";
            using (BitmapFont font = BitmapFont.FromBmFont(xml, r => new Bitmap(r.Width, r.Height)))
            {
                Assert.NotNull(font);
                Assert.Equal(6, font.Height);
                Assert.Equal(5, font.Measure("?"));
            }
        }

        [Fact]
        public void OwnDigitsAreSixPixelsWide()
        {
            using (Art art = OwnArt())
            {
                Assert.Equal(6, art.Mono.Measure("0"));
                Assert.Equal(30, art.Mono.Measure("99.9F"));
            }
        }

        [Theory]
        [InlineData(1, 208)]
        [InlineData(2, 64)]
        [InlineData(3, 208)]
        [InlineData(5, 64)]
        [InlineData(6, 208)]
        public void GlowWritesExactlyTheTargetArea(int scale, int rows)
        {
            using (Art art = OwnArt())
            using (var canvas = new Canvas(art))
            {
                canvas.Clear();
                canvas.Box(0, 0, 39, rows == 64 ? 7 : 24, top: true);
                canvas.Text("radio.exe", 16, 0);
                int width = Canvas.W * scale, height = rows * scale, stride = width * 4;
                const int guard = 4096;
                IntPtr memory = Marshal.AllocHGlobal(stride * height + guard);
                try
                {
                    var sentinel = new byte[guard];
                    for (int i = 0; i < guard; i++) sentinel[i] = 0xA5;
                    Marshal.Copy(sentinel, 0, memory + stride * height, guard);
                    new Glow().Compose(canvas.Bitmap, scale, rows, true, 0.3f, 0.8f, memory, stride);
                    var after = new byte[guard];
                    Marshal.Copy(memory + stride * height, after, 0, guard);
                    Assert.All(after, b => Assert.Equal(0xA5, b));
                    int corner = Marshal.ReadInt32(memory);
                    Assert.True(((corner >> 24) & 0xFF) >= (corner & 0xFF));
                }
                finally
                {
                    Marshal.FreeHGlobal(memory);
                }
            }
        }
    }
}
