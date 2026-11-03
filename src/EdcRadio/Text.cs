using System.Collections.Generic;
using System.Globalization;

namespace EdcRadio
{
    internal static class Text
    {
        public static bool Russian = IsRussianCulture();

        private static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>
        {
            ["no music"] = new[] { "музыки пока нет", "no music yet" },
            ["no music 1"] = new[] { "Радио играет саундтрек Easy Delivery Co.", "The radio plays the Easy Delivery Co. soundtrack." },
            ["no music 2"] = new[] { "Укажите папку с ним или купите его в Steam.", "Pick a folder with it or buy it on Steam." },
            ["no music 3"] = new[] { "Установленный саундтрек из Steam плеер найдёт сам.", "An installed Steam soundtrack is found on its own." },
            ["no music status"] = new[] { "нет музыки", "no music" },
            ["pick folder"] = new[] { "указать папку", "pick a folder" },
            ["steam"] = new[] { "саундтрек в Steam", "soundtrack on Steam" },
            ["folder dialog"] = new[] { "Папка с саундтреком Easy Delivery Co.", "Folder with the Easy Delivery Co. soundtrack" },
            ["pause"] = new[] { "пауза", "pause" },
            ["play"] = new[] { "играть", "play" },
            ["tape"] = new[] { "кассета", "tape" },
            ["scan"] = new[] { "скан", "scan" },
            ["scanning"] = new[] { "скан станций", "scanning" },
            ["sleep"] = new[] { "сон", "sleep" },
            ["off"] = new[] { "выкл", "off" },
            ["min"] = new[] { "мин", "min" },
            ["folder"] = new[] { "папка", "folder" },
            ["settings"] = new[] { "настройки", "settings" },
            ["on air"] = new[] { "в эфире", "on air" },
            ["paused"] = new[] { "пауза", "paused" },
            ["static"] = new[] { "шум эфира", "static" },
            ["side"] = new[] { "сторона {0}, трек {1}", "side {0}, track {1}" },
            ["sleep in"] = new[] { "сон через {0} мин", "sleep in {0} min" },
            ["sleep off"] = new[] { "сон выключен", "sleep is off" },
            ["volume"] = new[] { "громкость {0} %", "volume {0} %" },
            ["glow on"] = new[] { "свечение экрана включено", "screen glow on" },
            ["glow off"] = new[] { "свечение экрана выключено", "screen glow off" },
            ["overlay on"] = new[] { "оверлей, Ctrl+Alt+O вернёт окно", "overlay, Ctrl+Alt+O brings the window back" },
            ["overlay off"] = new[] { "оверлей выключен", "overlay off" },
            ["hotkeys busy"] = new[] { "Ctrl+Alt + {0} заняты другой программой", "Ctrl+Alt + {0} are taken by another program" },
            ["custom"] = new[] { "своё радио", "your radio" },
            ["easyco"] = new[] { "радио ЛегКо", "EasyCo radio" },
            ["news"] = new[] { "EZ News", "EZ News" },
            ["lofi"] = new[] { "лоу-фай", "lo-fi" },
            ["dnb"] = new[] { "драм-н-бейс", "drum and bass" },
            ["rally"] = new[] { "ралли", "rally" },
            ["night"] = new[] { "после смены", "after hours" },
            ["between"] = new[] { "между станциями", "between stations" },
            ["news line"] = new[] { "EZ News выходит только в кабине. Лёгкого Вам дня!", "EZ News airs in the cab only. Have an Easy day." },
            ["custom line"] = new[] { "Здесь играют файлы из папки CustomRadio игры. Пока их там нет.", "This plays files from the game's CustomRadio folder. It has none yet." },
            ["missing line"] = new[] { "Треков этой станции нет в выбранной папке.", "This station's tracks are not in the chosen folder." },
            ["foreign line"] = new[] { "В папке нет треков саундтрека. Кассета сыграет файлы папки по порядку.", "The folder has no soundtrack tracks. The tape plays its files in order." },
            ["help"] = new[] { "клавиши", "keys" },
            ["help window"] = new[] { "в окне", "in the window" },
            ["help anywhere"] = new[] { "везде, даже в игре", "anywhere, even in a game" },
            ["help tune"] = new[] { "частота", "frequency" },
            ["help seek"] = new[] { "соседняя станция", "previous or next station" },
            ["help volume"] = new[] { "громкость", "volume" },
            ["help pause"] = new[] { "пауза", "pause" },
            ["help presets"] = new[] { "станции по порядку", "stations in order" },
            ["help tape"] = new[] { "кассета", "tape" },
            ["help scan"] = new[] { "скан станций", "station scan" },
            ["help sleep"] = new[] { "сон: 15, 30, 60 мин", "sleep: 15, 30, 60 min" },
            ["help glow"] = new[] { "свечение экрана", "screen glow" },
            ["help help"] = new[] { "эти клавиши", "these keys" },
            ["help overlay"] = new[] { "оверлей", "overlay" },
            ["help mini"] = new[] { "мини-режим", "mini mode" },
            ["help show"] = new[] { "показать или скрыть", "show or hide" },
            ["help wheel"] = new[] { "колесо", "wheel" },
            ["help wheel do"] = new[] { "частота или громкость", "frequency or volume" },
            ["key space"] = new[] { "пробел", "space" },
            ["set overlay"] = new[] { "оверлей", "overlay" },
            ["set background"] = new[] { "фон в оверлее", "overlay background" },
            ["set overlay mini"] = new[] { "мини в оверлее", "mini in overlay" },
            ["set click"] = new[] { "клики сквозь оверлей", "clicks pass through" },
            ["set topmost"] = new[] { "поверх окон", "always on top" },
            ["set mini"] = new[] { "мини-режим", "mini mode" },
            ["set glow"] = new[] { "свечение экрана", "screen glow" },
            ["set tray"] = new[] { "значок в трее", "tray icon" },
            ["set language"] = new[] { "язык", "language" },
            ["set scale"] = new[] { "масштаб", "scale" },
            ["language name"] = new[] { "русский", "English" },
            ["tray show"] = new[] { "показать окно", "show window" },
            ["tray hide"] = new[] { "скрыть окно", "hide window" },
            ["tray next"] = new[] { "следующая станция", "next station" },
            ["tray quit"] = new[] { "выход", "quit" },
        };

        private static bool IsRussianCulture()
        {
            string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return lang == "ru" || lang == "uk" || lang == "be" || lang == "kk";
        }

        public static string T(string key) =>
            Lines.TryGetValue(key, out string[] v) ? v[Russian ? 0 : 1] : key;
    }
}
