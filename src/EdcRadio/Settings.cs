using System;
using System.IO;
using System.Text.Json;

namespace EdcRadio
{
    internal sealed class Settings
    {
        public static readonly int[] Backgrounds = { 0, 30, 60, 100 };

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
        public int X { get; set; } = int.MinValue;
        public int Y { get; set; } = int.MinValue;

        private static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EdcRadio", "settings.json");

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            }
            catch (Exception e) when (e is JsonException || e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
            }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
            }
        }
    }
}
