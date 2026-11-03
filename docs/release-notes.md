# v1.0.0

Первая версия.

## Что есть

- **7 станций** на частотах радио из игры, 88.1–107.9 МГц, шаг 0,1 МГц. Эфир
  идёт по часам с 1 января 2025 года по UTC.
- **Шум между станциями**, помехи на слабом сигнале и треск 0,35 секунды при
  переходе на соседнюю станцию. Станция ловится в пределах 0,4 МГц.
- **Кассета** — альбом по порядку, сторона A и сторона B, номер трека
  сохраняется.
- **Скан** — переход на следующую станцию каждые 6 секунд.
- **Сон** на 15, 30 или 60 минут с затиханием за 10 секунд.
- **Оверлей по Ctrl+Alt+O** — полоска поверх игры, фон непрозрачен на 0, 30,
  60 или 100 %, мини-режим 320 на 64 точки и клики сквозь окно.
- **Глобальные клавиши Ctrl+Alt** для паузы, станций, громкости, оверлея,
  мини-режима и окна.
- **Значок в трее** с меню, окно «настройки» в стиле настроек игры.
- **Саундтрек из Steam** находится сам, путь к Steam берётся из реестра. Свою
  папку можно указать кнопкой.
- **Шрифты и иконки игры** читаются из установленной игры, на тестовом
  компьютере за 0,2 секунды. Без игры работает свой пак.

## Установка

Скачайте `EdcRadio.exe` и запустите. Нужны Windows 10 или 11, 64 бита, среда
.NET уже внутри exe. Настройки хранятся в `%APPDATA%\EdcRadio`.

Windows SmartScreen может предупредить о неизвестном издателе, потому что
сборка не подписана сертификатом. «Подробнее» → «Выполнить в любом случае».

---

## English

First release.

- **7 stations** on the in-game radio frequencies, 88.1 to 107.9 MHz in
  0.1 MHz steps. The broadcast follows the clock from 1 January 2025 UTC.
- **Static between stations**, interference on a weak signal and a 0.35 second
  crackle when jumping to the next station. A station comes in within 0.4 MHz.
- **Tape** plays the album in order, side A and side B, and keeps the track
  number.
- **Scan** moves to the next station every 6 seconds.
- **Sleep** for 15, 30 or 60 minutes with a 10 second fade.
- **Overlay on Ctrl+Alt+O** is a strip over the game with a 0, 30, 60 or 100 %
  background, a 320 by 64 mini mode and clicks that pass through.
- **Global Ctrl+Alt keys** for pause, stations, volume, overlay, mini mode and
  the window.
- **A tray icon** with a menu, and a "settings" window laid out like the game's
  settings.
- **The Steam soundtrack** is found on its own, with the Steam path read from
  the registry. A folder of your own can be picked with a button.
- **The game's fonts and icons** are read from the installed game, in
  0.2 seconds on the test computer. Without the game the built-in pack is used.

Download `EdcRadio.exe` and run it. It needs Windows 10 or 11, 64-bit, and the
.NET runtime is inside the exe. Settings are kept in `%APPDATA%\EdcRadio`.

Windows SmartScreen may warn about an unknown publisher because the build is
not signed. Click "More info", then "Run anyway".
