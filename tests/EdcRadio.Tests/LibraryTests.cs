using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace EdcRadio.Tests
{
    public sealed class LibraryTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "edc-radio-tests-" + Guid.NewGuid().ToString("N"));

        public LibraryTests() => Directory.CreateDirectory(_dir);

        public void Dispose() => Directory.Delete(_dir, true);

        [Fact]
        public void ReadsFlacLengthAndTags()
        {
            Flac.Write(Path.Combine(_dir, "1-05. File Name.flac"), 44100, 44100L * 125, "TITLE=Factory Settings", "ARTIST=Sohaoying", "TRACKNUMBER=5/22", "DISCNUMBER=1/2");
            var track = Library.Load(new[] { _dir }).Single();
            Assert.Equal("Factory Settings", track.Title);
            Assert.Equal("Sohaoying", track.Artist);
            Assert.Equal(5, track.Number);
            Assert.Equal(1, track.Disc);
            Assert.Equal(125.0, track.Length.TotalSeconds, 3);
        }

        [Fact]
        public void TakesNumbersAndTitleFromTheFileName()
        {
            Flac.Write(Path.Combine(_dir, "2-03. Evening Tonight.flac"), 48000, 48000L * 60);
            var track = Library.Load(new[] { _dir }).Single();
            Assert.Equal(2, track.Disc);
            Assert.Equal(3, track.Number);
            Assert.Equal("Evening Tonight", track.Title);
        }

        [Fact]
        public void SurvivesBrokenFiles()
        {
            File.WriteAllBytes(Path.Combine(_dir, "1-01. Empty.flac"), new byte[0]);
            File.WriteAllText(Path.Combine(_dir, "1-02. Text.flac"), "not audio");
            File.WriteAllBytes(Path.Combine(_dir, "1-03. Cut.flac"), Encoding.ASCII.GetBytes("fLaC\u0000"));
            File.WriteAllBytes(Path.Combine(_dir, "\uFF11-\uFF10\uFF14. Wide digits.flac"), new byte[8]);
            File.WriteAllText(Path.Combine(_dir, "notes.txt"), "x");
            var tracks = Library.Load(new[] { _dir });
            Assert.Equal(4, tracks.Count);
            Assert.All(tracks, t => Assert.Equal(TimeSpan.Zero, t.Length));
        }

        [Fact]
        public void PrefersFlacOverOtherFormats()
        {
            Flac.Write(Path.Combine(_dir, "1-10. Rest.flac"), 44100, 44100L * 30);
            File.WriteAllBytes(Path.Combine(_dir, "1-10. Rest.mp3"), new byte[32]);
            var tracks = Library.Load(new[] { _dir });
            Assert.Single(tracks);
            Assert.EndsWith(".flac", tracks[0].Path);
        }

        [Fact]
        public void KeepsDifferentSongsThatShareANumber()
        {
            Directory.CreateDirectory(Path.Combine(_dir, "a"));
            Directory.CreateDirectory(Path.Combine(_dir, "b"));
            Flac.Write(Path.Combine(_dir, "a", "01 Intro.flac"), 44100, 44100L * 30);
            Flac.Write(Path.Combine(_dir, "b", "01 Intro.flac"), 44100, 44100L * 95);
            Assert.Equal(2, Library.Load(new[] { _dir }).Count);
        }

        [Fact]
        public void KeepsTitlesWithoutLetters()
        {
            Flac.Write(Path.Combine(_dir, "01 !!!.flac"), 44100, 44100L * 30);
            Flac.Write(Path.Combine(_dir, "01 ###.flac"), 44100, 44100L * 30);
            Assert.Equal(2, Library.Load(new[] { _dir }).Count);
        }

        [Fact]
        public void CustomRadioKeepsEveryFile()
        {
            Flac.Write(Path.Combine(_dir, "1-10. Rest.flac"), 44100, 44100L * 30);
            File.WriteAllBytes(Path.Combine(_dir, "1-10. Rest.mp3"), new byte[32]);
            Assert.Equal(2, Library.Load(new[] { _dir }, false).Count);
        }

        [Fact]
        public void CutsVeryLongTags()
        {
            Flac.Write(Path.Combine(_dir, "01 Long.flac"), 44100, 44100L * 30, "TITLE=" + new string('x', 5000), "ARTIST=" + new string('y', 5000));
            Track track = Library.Load(new[] { _dir }).Single();
            Assert.Equal(200, track.Title.Length);
            Assert.Equal(200, track.Artist.Length);
        }

        [Fact]
        public void EveryFolderGetsItsOwnLimit()
        {
            string big = Path.Combine(_dir, "big"), small = Path.Combine(_dir, "small");
            Directory.CreateDirectory(big);
            Directory.CreateDirectory(small);
            for (int i = 0; i <= Library.Limit; i++) Flac.Write(Path.Combine(big, "song " + i + ".flac"), 44100, 44100L * 10);
            Flac.Write(Path.Combine(small, "1-02. Rainless.flac"), 44100, 44100L * 244);
            var tracks = Library.Load(new[] { big, small });
            Assert.Equal(Library.Limit + 1, tracks.Count);
            Assert.Contains(tracks, t => t.Title == "Rainless");
        }

        [Fact]
        public void IgnoresMissingFolders()
        {
            var tracks = Library.Load(new[] { Path.Combine(_dir, "missing"), "", "Z:\\" + new string('x', 300) });
            Assert.Empty(tracks);
        }

        [Fact]
        public void NormalizeMatchesTitlesAcrossPunctuation()
        {
            Assert.Equal(Library.Normalize("Track & Trace"), Library.Normalize("track and trace"));
            Assert.Equal(Library.Normalize("It's No Problem"), Library.Normalize("its no problem"));
            Assert.Equal("", Library.Normalize(null));
        }
    }
}
