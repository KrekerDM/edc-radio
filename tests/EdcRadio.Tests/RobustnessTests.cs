using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace EdcRadio.Tests
{
    public sealed class RobustnessTests : IDisposable
    {
        private static readonly Type[] Allowed =
        {
            typeof(IOException), typeof(UnauthorizedAccessException), typeof(InvalidDataException), typeof(NotSupportedException),
            typeof(ArgumentException), typeof(IndexOutOfRangeException), typeof(OverflowException),
            typeof(System.Runtime.InteropServices.ExternalException), typeof(OutOfMemoryException), typeof(InvalidOperationException),
        };

        private readonly string _file = Path.Combine(Path.GetTempPath(), "edc-radio-fuzz-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (File.Exists(_file)) File.Delete(_file);
        }

        [Fact]
        public void BundleReaderOnlyThrowsHandledExceptions()
        {
            var rnd = new Random(42);
            var header = Encoding.ASCII.GetBytes("UnityFS\0").Concat(new byte[] { 0, 0, 0, 8 }).Concat(Encoding.ASCII.GetBytes("5.x.x\06000.0.58f2\0")).ToArray();
            for (int i = 0; i < 300; i++)
            {
                byte[] data;
                if (i % 3 == 0)
                {
                    data = new byte[rnd.Next(0, 5000)];
                    rnd.NextBytes(data);
                }
                else
                {
                    var tail = new byte[rnd.Next(0, 4000)];
                    rnd.NextBytes(tail);
                    data = header.Concat(tail).ToArray();
                }
                File.WriteAllBytes(_file, data);
                try
                {
                    using (UnityBundle bundle = UnityBundle.Open(_file))
                        foreach (UnityBundle.Node node in bundle.Nodes.Take(5))
                            UnityAsset.Read(bundle, node);
                }
                catch (Exception e)
                {
                    Assert.True(Allowed.Any(t => t.IsInstanceOfType(e)), "case " + i + " escaped with " + e.GetType().Name + ": " + e.Message);
                }
            }
        }

        [Theory]
        [InlineData("{\"Volume\": \"loud\"}")]
        [InlineData("{\"Scale\": 1e30}")]
        [InlineData("{")]
        [InlineData("[]")]
        public void BrokenSettingsThrowJsonException(string text)
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Settings>(text));
        }

        [Fact]
        public void SettingsAcceptHandEdits()
        {
            Settings settings = Settings.Parse("{\n  // quieter at night\n  \"Volume\": \"0.25\",\n  \"Overlay\": true,\n}");
            Assert.NotNull(settings);
            Assert.Equal(0.25f, settings.Volume);
            Assert.True(settings.Overlay);
        }

        [Theory]
        [InlineData("{")]
        [InlineData("[]")]
        [InlineData("{\"Volume\": \"loud\"}")]
        public void BrokenSettingsParseToNull(string text)
        {
            Assert.Null(Settings.Parse(text));
        }

        [Fact]
        public void SettingsRoundTrip()
        {
            var settings = new Settings { Folder = "C:\\Музыка", Frequency = 101.7f, Overlay = true, OverlayBackground = 60, TapeTrack = 12 };
            var back = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings));
            Assert.Equal(settings.Folder, back.Folder);
            Assert.Equal(settings.Frequency, back.Frequency);
            Assert.True(back.Overlay);
            Assert.Equal(60, back.OverlayBackground);
            Assert.Equal(12, back.TapeTrack);
        }
    }
}
