# v1.1.0

- **Статус в Discord.** Если Discord запущен, в профиле видно «Слушает EDC
  Radio»: трек, исполнитель, станция с частотой или сторона кассеты и полоска
  времени. Картинки статуса — значок плеера, радиовышка и кассета из своего
  пака. На паузе, между станциями и без музыки статус пропадает.
- **Новая строка в настройках**, «статус в Discord». Статус включён сразу, и
  при первом показе плеер пишет об этом в строке состояния.
- Плеер говорит с Discord через его локальный канал в отдельном потоке. Если
  Discord не запущен, не отвечает или перезапустился, окно не ждёт, а плеер
  пробует снова раз в 20 секунд.

---

## English

- **Discord status.** When Discord runs, your profile shows "Listening to EDC
  Radio" with the track, the artist, the station and frequency or the tape
  side, and a time bar. The status images are the player icon, the radio tower
  and the cassette from the built-in pack. The status goes away on pause,
  between stations and without music.
- **A new settings row**, "Discord status". It is on by default, and the
  first time the status shows, the player says so in the status line.
- The player talks to Discord over its local channel on a separate thread. When
  Discord is not running, does not answer or was restarted, the window does not
  wait, and the player tries again every 20 seconds.

# v1.0.1

Исправления после проверки на ошибки.

- **Окно открывается сразу**, треки читаются в фоне. Раньше большая папка или
  спящий диск подвешивали окно при запуске, при выборе папки и при каждом
  переключении на плеер без музыки.
- **Саундтрек из Steam не теряется**, если в выбранной папке больше 2000
  файлов: предел теперь у каждой папки свой.
- **Свои песни не занимают места треков саундтрека.** Если названия совпали,
  станция берёт трек из папки, где треков саундтрека больше всего.
- **Разные песни с одинаковым номером и названием** больше не склеиваются в
  одну, а из CustomRadio играют все файлы.
- **Файл, который не открылся**, станция пропускает и включает следующий трек,
  имя файла видно в строке состояния. Раньше станция молчала.
- **Сон срабатывает вовремя**, даже если компьютер уходил в спящий режим или
  был загружен.
- **Кассета помнит трек по файлу**, а не по номеру в списке, и не выключается,
  если однажды при запуске музыка не нашлась.
- **Клики сквозь оверлей** включаются при запуске без значка в трее и после
  переключения значка. Если Ctrl+Alt+O занято, в заголовке написано
  «меню в трее».
- **Окно не меняет размер** при переходе на монитор с другим масштабом
  Windows и уменьшает свой масштаб, если не помещается на экране.
- **Повторный запуск exe** выводит окно на передний план.
- **При падении** плеер сохраняет настройки и пишет отчёт в
  `%APPDATA%\EdcRadio\crash.log`.
- **settings.json** с комментариями и лишними запятыми читается, испорченный
  файл остаётся рядом как `settings.json.bad`, запись идёт через временный
  файл.
- **Сценка не замирает**, если плеер работает несколько дней подряд.
- **Длинные теги, огромные обложки и странные таблицы шрифтов** больше не
  тормозят и не роняют плеер.

---

## English

Fixes after a bug hunt.

- **The window opens at once** and the tracks are read in the background.
  A big folder or a sleeping drive used to freeze the window at startup, after
  picking a folder and every time you switched to the player without music.
- **The Steam soundtrack is no longer lost** when the chosen folder has more
  than 2000 files: every folder now gets its own limit.
- **Your own songs no longer take the place of soundtrack tracks.** When names
  match, a station takes the track from the folder that holds the most
  soundtrack tracks.
- **Different songs with the same number and name** are no longer merged into
  one, and every file in CustomRadio plays.
- **A file that does not open** is skipped, the station plays the next track
  and the status line shows the file name. The station used to go silent.
- **Sleep fires on time**, even after the computer slept or was busy.
- **Tape remembers its track by file**, not by its place in the list, and no
  longer turns off after a launch where the music was not found.
- **Clicks pass through the overlay** at startup without the tray icon and
  after the tray icon is toggled. If Ctrl+Alt+O is taken, the title bar says
  "tray menu".
- **The window keeps its size** on a monitor with a different Windows scale
  and lowers its own scale when it does not fit the screen.
- **Starting the exe again** brings the window to the front.
- **On a crash** the player saves its settings and writes a report to
  `%APPDATA%\EdcRadio\crash.log`.
- **settings.json** with comments and trailing commas is read, a damaged file
  is kept next to it as `settings.json.bad`, and saving goes through a
  temporary file.
- **The scene keeps moving** when the player runs for days.
- **Long tags, huge covers and odd font tables** no longer slow down or crash
  the player.

# v1.0.0

Первая версия.

## Что есть

- **7 станций**, 88.1–107.9 МГц, шаг 0,1 МГц: шесть на частотах радио из игры
  и своя 107.9. Эфир идёт по часам с 1 января 2025 года по UTC.
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

- **7 stations**, 88.1 to 107.9 MHz in 0.1 MHz steps: six on the in-game
  radio frequencies and the player's own 107.9. The broadcast follows the clock
  from 1 January 2025 UTC.
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
