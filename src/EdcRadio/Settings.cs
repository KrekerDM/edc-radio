using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EdcRadio
{
    internal sealed class Settings
    {
        public static readonly int[] Backgrounds = { 0, 30, 60, 100 };

        private static readonly JsonSerializerOptions Reading = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };

        private static readonly JsonSerializerOptions Writing = new JsonSerializerOptions { WriteIndented = true };

        public string Folder { get; set; } = "";
        public float Frequency { get; set; } = 99.9f;
        public float Volume { get; set; } = 0.8f;
        public string Language { get; set; } = "";
        public int Scale { get; set; }
        public bool Glow { get; set; } = true;
        public bool Mini { get; set; }
        public bool TopMost { get; set; }
        public bool Overlay { get; set; }
        public int OverlayBackground { get; set; } = 30;
        public bool OverlayMini { get; set; } = true;
        public bool OverlayClickThrough { get; set; } = true;
        public bool Tray { get; set; } = true;
        public bool Tape { get; set; }
        public int TapeTrack { get; set; }
        public string TapePath { get; set; } = "";
        public int X { get; set; } = int.MinValue;
        public int Y { get; set; } = int.MinValue;

        private static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EdcRadio", "settings.json");

        public static Settings Load()
        {
            string text;
            try
            {
                if (!File.Exists(FilePath)) return new Settings();
                text = File.ReadAllText(FilePath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                return new Settings();
            }
            Settings settings = Parse(text);
            if (settings != null) return settings;
            Keep(FilePath, FilePath + ".bad");
            return new Settings();
        }

        public static Settings Parse(string text)
        {
            try
            {
                return JsonSerializer.Deserialize<Settings>(text, Reading) ?? new Settings();
            }
            catch (Exception e) when (e is JsonException || e is NotSupportedException)
            {
                return null;
            }
        }

        public void Save()
        {
            string temp = FilePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(temp, JsonSerializer.Serialize(this, Writing));
                File.Move(temp, FilePath, true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
            }
        }

        private static void Keep(string file, string copy)
        {
            try
            {
                File.Copy(file, copy, true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
            }
        }
    }
}
