using System;
using System.Collections.Generic;
using System.Linq;

namespace EdcRadio
{
    internal sealed class Station
    {
        public float Frequency;
        public string Id;
        public string[] Playlist;
        public float Tempo;
        public List<Track> Tracks = new List<Track>();

        public TimeSpan Total => TimeSpan.FromSeconds(Tracks.Sum(t => t.Length.TotalSeconds));
        public bool OnAir => Tracks.Count > 0 && Tracks.All(t => t.Length > TimeSpan.Zero) && Total.TotalSeconds > 1;
    }

    internal static class Stations
    {
        public const float Min = 88.1f;
        public const float Max = 107.9f;
        public const float Width = 0.4f;

        public static List<Station> Build(List<Track> album, List<Track> custom)
        {
            var stations = new List<Station>
            {
                new Station { Frequency = 88.1f, Id = "custom", Tempo = 18f },
                new Station { Frequency = 91.1f, Id = "easyco", Tempo = 16f, Playlist = new[] { "rainless" } },
                new Station { Frequency = 99.1f, Id = "news", Tempo = 16f },
                new Station
                {
                    Frequency = 99.9f, Id = "lofi", Tempo = 14f,
                    Playlist = new[]
                    {
                        "easily delivered or whatever", "are auroras real", "factory settings", "snowdrifting",
                        "chilling on company time", "easily delivered or whatever", "cruising", "roadside mud", "rest",
                        "it's no problem",
                    },
                },
                new Station
                {
                    Frequency = 101.7f, Id = "dnb", Tempo = 34f,
                    Playlist = new[]
                    {
                        "same day delivery", "parcel dreams", "track & trace", "sunset logistics", "final dispatch",
                        "subzero shipping", "sender unknown", "freight across the galaxy", "lost goods", "kei thx bye",
                    },
                },
                new Station
                {
                    Frequency = 104.9f, Id = "rally", Tempo = 26f,
                    Playlist = new[] { "wrench in the works", "winding roads", "evening tonight", "exhaust scavenging", "drifting off", "another day" },
                },
                new Station { Frequency = 107.9f, Id = "night", Tempo = 10f, Playlist = new[] { "shut down", "rainless (hum)" } },
            };
            string[] wanted = stations.Where(s => s.Playlist != null).SelectMany(s => s.Playlist).Select(Library.Normalize).Distinct().ToArray();
            List<Track> playable = album.Where(t => t.Length > TimeSpan.Zero).ToList();
            Dictionary<string, int> hits = playable
                .GroupBy(Folder, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(t => t.Key).Distinct().Count(k => wanted.Any(w => Fits(k, w))), StringComparer.OrdinalIgnoreCase);
            var byKey = new Dictionary<string, Track>();
            foreach (Track t in playable.OrderByDescending(t => hits[Folder(t)]))
                if (!byKey.ContainsKey(t.Key)) byKey[t.Key] = t;
            foreach (Station s in stations.Where(s => s.Playlist != null))
                foreach (string title in s.Playlist)
                {
                    Track t = Match(byKey, Library.Normalize(title));
                    if (t != null) s.Tracks.Add(t);
                }
            stations[0].Tracks.AddRange(custom.Where(t => t.Length > TimeSpan.Zero));
            return stations;
        }

        private static Track Match(Dictionary<string, Track> byKey, string key)
        {
            if (byKey.TryGetValue(key, out Track exact)) return exact;
            if (key.Length < 6) return null;
            return byKey.Where(p => Fits(p.Key, key))
                .OrderBy(p => p.Key.Length)
                .Select(p => p.Value)
                .FirstOrDefault();
        }

        private static bool Fits(string key, string wanted) =>
            key == wanted || (wanted.Length >= 6 && key.StartsWith(wanted, StringComparison.Ordinal));

        private static string Folder(Track t) => System.IO.Path.GetDirectoryName(t.Path) ?? "";

        public static float Signal(Station s, float frequency) =>
            s == null ? 0f : Math.Max(0f, 1f - Math.Abs(frequency - s.Frequency) / Width);

        public static Station Nearest(List<Station> stations, float frequency) =>
            stations.OrderBy(s => Math.Abs(s.Frequency - frequency)).FirstOrDefault();

        public static (Track track, TimeSpan offset, int index) LiveAt(Station s, DateTime utc)
        {
            double total = s.Total.TotalSeconds;
            double t = ((utc - new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds % total + total) % total;
            for (int i = 0; i < s.Tracks.Count; i++)
            {
                Track track = s.Tracks[i];
                if (t < track.Length.TotalSeconds) return (track, TimeSpan.FromSeconds(t), i);
                t -= track.Length.TotalSeconds;
            }
            return (s.Tracks[0], TimeSpan.Zero, 0);
        }
    }
}
