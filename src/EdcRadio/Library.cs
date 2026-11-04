using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace EdcRadio
{
    internal sealed class Track
    {
        public string Path;
        public string Title;
        public string Artist;
        public int Disc;
        public int Number;
        public TimeSpan Length;

        public string Key => Library.Normalize(Title);
    }

    internal static class Library
    {
        public const int SoundtrackAppId = 3994280;
        public const string StoreUrl = "https://store.steampowered.com/app/3994280";
        public const int Limit = 2000;
        private const int Longest = 200;

        private static readonly string[] Extensions = { ".flac", ".wav", ".m4a", ".mp3", ".wma" };

        public static List<Track> Load(IEnumerable<string> folders, bool distinct = true)
        {
            var tracks = new List<Track>();
            foreach (string folder in folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                List<string> files;
                try
                {
                    var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System };
                    files = Directory.EnumerateFiles(folder, "*", options)
                        .Where(f => Extensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()))
                        .Take(Limit)
                        .ToList();
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    continue;
                }
                Shell shell = null;
                try
                {
                    foreach (string file in files)
                    {
                        try
                        {
                            Track t = Read(file);
                            if (t.Length <= TimeSpan.Zero) (shell ??= new Shell()).Fill(t);
                            t.Title = Clip(t.Title);
                            t.Artist = Clip(t.Artist);
                            tracks.Add(t);
                        }
                        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
                        {
                        }
                    }
                }
                finally
                {
                    shell?.Dispose();
                }
            }
            return (distinct ? Distinct(tracks) : tracks)
                .OrderBy(t => t.Disc).ThenBy(t => t.Number).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<Track> Distinct(List<Track> tracks) =>
            tracks.GroupBy(t => (t.Disc, t.Number, t.Key != "" ? t.Key : System.IO.Path.ChangeExtension(t.Path, null).ToLowerInvariant()))
                .SelectMany(group =>
                {
                    var kept = new List<Track>();
                    foreach (Track t in group.OrderBy(t => t.Length > TimeSpan.Zero ? 0 : 1).ThenBy(t => Rank(t.Path)))
                        if (!kept.Any(k => Same(k, t))) kept.Add(t);
                    return kept;
                });

        private static int Rank(string path) => Array.IndexOf(Extensions, System.IO.Path.GetExtension(path).ToLowerInvariant());

        private static bool Same(Track a, Track b) =>
            a.Length <= TimeSpan.Zero || b.Length <= TimeSpan.Zero || Math.Abs((a.Length - b.Length).TotalSeconds) <= 2.0;

        private static string Clip(string s) => s != null && s.Length > Longest ? s.Substring(0, Longest) : s;

        public static List<string> Sources(string chosen)
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(chosen) && Directory.Exists(chosen)) list.Add(chosen);
            string steam = SteamSoundtrack();
            if (steam != null) list.Add(steam);
            return list;
        }

        public static string SteamSoundtrack()
        {
            foreach (string root in SteamLibraries())
            {
                string music = System.IO.Path.Combine(root, "steamapps", "music");
                if (!Directory.Exists(music)) continue;
                try
                {
                    string album = Directory.GetDirectories(music)
                        .FirstOrDefault(d => System.IO.Path.GetFileName(d).IndexOf("Easy Delivery", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (album != null) return album;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                }
            }
            return null;
        }

        public static string SteamRoot()
        {
            foreach (string root in SteamCandidates())
                if (File.Exists(System.IO.Path.Combine(root, "steam.exe")) || File.Exists(System.IO.Path.Combine(root, "steamapps", "libraryfolders.vdf")))
                    return root;
            return null;
        }

        private static IEnumerable<string> SteamCandidates()
        {
            var list = new List<string>();
            try
            {
                if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string user && user != "")
                    list.Add(System.IO.Path.GetFullPath(user));
                if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) is string machine && machine != "")
                    list.Add(System.IO.Path.GetFullPath(machine));
            }
            catch (Exception e) when (e is System.Security.SecurityException || e is IOException || e is ArgumentException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
            }
            list.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            return list.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public static IEnumerable<string> SteamLibraries()
        {
            var roots = new List<string>();
            string main = SteamRoot() ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            roots.Add(main);
            string vdf = System.IO.Path.Combine(main, "steamapps", "libraryfolders.vdf");
            try
            {
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
                        roots.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
            }
            return roots.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public static string GameCustomRadio()
        {
            foreach (string root in SteamLibraries())
            {
                string dir = System.IO.Path.Combine(root, "steamapps", "common", "Easy Delivery Co", "EasyDeliveryCo_Data", "StreamingAssets", "CustomRadio");
                if (Directory.Exists(dir)) return dir;
            }
            return null;
        }

        public static string Normalize(string s) =>
            new string((s ?? "").ToLowerInvariant().Replace("&", "and").Where(char.IsLetterOrDigit).ToArray());

        private static Track Read(string file)
        {
            var track = new Track { Path = file, Disc = 1 };
            Match name = Regex.Match(System.IO.Path.GetFileNameWithoutExtension(file), @"^(?:([0-9]{1,2})-)?([0-9]{1,3})\s*[.\-_]?\s+(.+)$");
            if (name.Success)
            {
                if (name.Groups[1].Success && int.TryParse(name.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int disc)) track.Disc = disc;
                if (int.TryParse(name.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int number)) track.Number = number;
                track.Title = name.Groups[3].Value.Trim();
            }
            else
            {
                track.Title = System.IO.Path.GetFileNameWithoutExtension(file);
            }
            if (System.IO.Path.GetExtension(file).Equals(".flac", StringComparison.OrdinalIgnoreCase))
                ReadFlac(file, track);
            return track;
        }

        private static void ReadFlac(string file, Track track)
        {
            using (var f = File.OpenRead(file))
            using (var r = new BinaryReader(f))
            {
                if (f.Length < 8 || Encoding.ASCII.GetString(r.ReadBytes(4)) != "fLaC") return;
                bool last = false;
                while (!last && f.Position + 4 <= f.Length)
                {
                    byte head = r.ReadByte();
                    last = (head & 0x80) != 0;
                    int type = head & 0x7F;
                    byte[] len = r.ReadBytes(3);
                    int size = (len[0] << 16) | (len[1] << 8) | len[2];
                    if (type == 0 || type == 4)
                    {
                        byte[] block = r.ReadBytes(size);
                        if (type == 0 && block.Length >= 18)
                        {
                            int rate = (block[10] << 12) | (block[11] << 4) | (block[12] >> 4);
                            long samples = ((long)(block[13] & 0x0F) << 32) | ((long)block[14] << 24) | ((long)block[15] << 16) | ((long)block[16] << 8) | block[17];
                            if (rate > 0) track.Length = TimeSpan.FromSeconds((double)samples / rate);
                        }
                        else if (type == 4)
                        {
                            ReadVorbisComments(block, track);
                        }
                    }
                    else
                    {
                        f.Position += size;
                    }
                }
            }
        }

        private static void ReadVorbisComments(byte[] b, Track track)
        {
            int p = 0;
            bool Fits(int n) => n >= 0 && n <= b.Length - p;
            int Int()
            {
                int v = BitConverter.ToInt32(b, p);
                p += 4;
                return v;
            }
            if (!Fits(4)) return;
            int vendor = Int();
            if (!Fits(vendor)) return;
            p += vendor;
            if (!Fits(4)) return;
            int count = Int();
            for (int i = 0; i < count && Fits(4); i++)
            {
                int n = Int();
                if (!Fits(n)) break;
                string c = Encoding.UTF8.GetString(b, p, n);
                p += n;
                int eq = c.IndexOf('=');
                if (eq <= 0) continue;
                string key = c.Substring(0, eq).ToUpperInvariant(), value = c.Substring(eq + 1).Trim();
                if (key == "TITLE" && value != "") track.Title = value;
                else if (key == "ARTIST" && value != "") track.Artist = value;
                else if (key == "TRACKNUMBER" && int.TryParse(value.Split('/')[0], out int num)) track.Number = num;
                else if (key == "DISCNUMBER" && int.TryParse(value.Split('/')[0], out int disc)) track.Disc = disc;
            }
        }

        private sealed class Shell : IDisposable
        {
            private readonly dynamic _app;
            private readonly Dictionary<string, dynamic> _folders = new Dictionary<string, dynamic>(StringComparer.OrdinalIgnoreCase);

            public Shell()
            {
                try
                {
                    Type type = Type.GetTypeFromProgID("Shell.Application");
                    if (type != null) _app = Activator.CreateInstance(type);
                }
                catch (Exception e) when (Broken(e))
                {
                    _app = null;
                }
            }

            public void Fill(Track track)
            {
                if (_app == null) return;
                dynamic item = null;
                try
                {
                    string dir = System.IO.Path.GetDirectoryName(track.Path);
                    if (!_folders.TryGetValue(dir, out dynamic folder)) _folders[dir] = folder = _app.NameSpace(dir);
                    if (folder == null) return;
                    item = folder.ParseName(System.IO.Path.GetFileName(track.Path));
                    if (item == null) return;
                    object duration = item.ExtendedProperty("System.Media.Duration");
                    if (duration != null) track.Length = TimeSpan.FromTicks(Convert.ToInt64(duration));
                    if (item.ExtendedProperty("System.Title") is string title && title.Trim() != "") track.Title = title.Trim();
                    object artist = item.ExtendedProperty("System.Music.Artist");
                    if (artist is string a && a != "") track.Artist = a;
                    else if (artist is string[] list && list.Length > 0) track.Artist = string.Join(", ", list);
                    object number = item.ExtendedProperty("System.Music.TrackNumber");
                    if (number != null && Convert.ToInt32(number) > 0) track.Number = Convert.ToInt32(number);
                    if (item.ExtendedProperty("System.Music.PartOfSet") is string set && int.TryParse(set.Split('/')[0], out int disc)) track.Disc = disc;
                }
                catch (Exception e) when (Broken(e))
                {
                }
                finally
                {
                    if (item is object com) Marshal.ReleaseComObject(com);
                }
            }

            private static bool Broken(Exception e) =>
                e is ExternalException || e is InvalidComObjectException || e is InvalidCastException || e is FormatException
                || e is OverflowException || e is ArgumentException || e is NotSupportedException || e is NotImplementedException
                || e is NullReferenceException || e is UnauthorizedAccessException || e is IOException || e is InvalidOperationException
                || e is OutOfMemoryException || e is TargetInvocationException || e is MemberAccessException
                || e is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException;

            public void Dispose()
            {
                foreach (object folder in _folders.Values)
                    if (folder != null) Marshal.FinalReleaseComObject(folder);
                _folders.Clear();
                if (_app is object app) Marshal.FinalReleaseComObject(app);
            }
        }
    }
}
