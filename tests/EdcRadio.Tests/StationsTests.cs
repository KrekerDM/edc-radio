using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EdcRadio.Tests
{
    public sealed class StationsTests
    {
        private static Track Song(string title, double seconds, int disc = 1, int number = 1) =>
            new Track { Path = title + ".flac", Title = title, Length = TimeSpan.FromSeconds(seconds), Disc = disc, Number = number };

        private static List<Track> Album() => new List<Track>
        {
            Song("It's No Problem", 250), Song("Rainless", 244), Song("Easily Delivered or Whatever", 200),
            Song("Are Auroras Real", 210), Song("Factory Settings", 190), Song("Snowdrifting", 226),
            Song("Chilling on Company Time", 205), Song("Cruising", 199), Song("Roadside Mud", 161), Song("Rest", 160),
            Song("Shut Down", 120), Song("Rainless (Hum)", 130), Song("Same Day Delivery", 187),
            Song("Track and Trace", 170), Song("Wrench in the Works", 195),
        };

        [Fact]
        public void BuildsSevenStationsInOrder()
        {
            var stations = Stations.Build(Album(), new List<Track>());
            Assert.Equal(new[] { 88.1f, 91.1f, 99.1f, 99.9f, 101.7f, 104.9f, 107.9f }, stations.Select(s => s.Frequency));
        }

        [Fact]
        public void MatchesPlaylistsByNormalizedTitles()
        {
            var stations = Stations.Build(Album(), new List<Track>());
            Station lofi = stations.Single(s => s.Id == "lofi");
            Assert.Equal(10, lofi.Tracks.Count);
            Assert.Equal(2, lofi.Tracks.Count(t => t.Title == "Easily Delivered or Whatever"));
            Assert.Contains(stations.Single(s => s.Id == "dnb").Tracks, t => t.Title == "Track and Trace");
            Assert.Equal(new[] { "Shut Down", "Rainless (Hum)" }, stations.Single(s => s.Id == "night").Tracks.Select(t => t.Title));
            Assert.False(stations.Single(s => s.Id == "news").OnAir);
        }

        [Fact]
        public void ZeroLengthTracksKeepAStationOffAir()
        {
            var stations = Stations.Build(new List<Track> { Song("Rainless", 0) }, new List<Track>());
            Assert.All(stations, s => Assert.False(s.OnAir));
        }

        [Fact]
        public void LiveAtStaysInsideTheSchedule()
        {
            Station lofi = Stations.Build(Album(), new List<Track>()).Single(s => s.Id == "lofi");
            var start = new DateTime(2019, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 2000; i++)
            {
                DateTime moment = start.AddSeconds(i * 98765.4321);
                var (track, offset, index) = Stations.LiveAt(lofi, moment);
                Assert.Same(lofi.Tracks[index], track);
                Assert.InRange(offset.TotalSeconds, 0, track.Length.TotalSeconds);
            }
        }

        [Fact]
        public void SameMomentGivesSameTrackOnEveryComputer()
        {
            Station lofi = Stations.Build(Album(), new List<Track>()).Single(s => s.Id == "lofi");
            var moment = new DateTime(2026, 11, 4, 1, 2, 3, DateTimeKind.Utc);
            Assert.Equal(Stations.LiveAt(lofi, moment), Stations.LiveAt(lofi, moment));
        }

        [Fact]
        public void PrefersTheFolderWithTheSoundtrack()
        {
            List<Track> album = Album();
            foreach (Track t in album) t.Path = System.IO.Path.Combine("ost", t.Path);
            album.Insert(0, new Track { Path = System.IO.Path.Combine("mine", "03 Rest.mp3"), Title = "Rest", Length = TimeSpan.FromSeconds(100), Disc = 1, Number = 3 });
            Station lofi = Stations.Build(album, new List<Track>()).Single(s => s.Id == "lofi");
            Track rest = lofi.Tracks.Single(t => t.Key == "rest");
            Assert.StartsWith("ost", rest.Path);
        }

        [Theory]
        [InlineData(99.9f, 1f)]
        [InlineData(100.1f, 0.5f)]
        [InlineData(100.3f, 0f)]
        [InlineData(100.5f, 0f)]
        public void SignalFadesOverPointFourMegahertz(float tuned, float expected)
        {
            var station = new Station { Frequency = 99.9f };
            Assert.Equal(expected, Stations.Signal(station, tuned), 2);
        }
    }
}
