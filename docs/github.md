# Тексты для GitHub

Поля страницы репозитория и релиза. Поле About держит до 350 знаков, оба
описания ниже в него помещаются. Тексты релиза лежат в `docs/release-notes.md`.

## Имя репозитория

```
edc-radio
```

## Короткое описание

Русский:

```
Радио для Windows с саундтреком Easy Delivery Co. Семь станций на частотах из игры, эфир по часам, шум между станциями, кассета, сон и оверлей поверх игры по Ctrl+Alt+O. Музыку берёт из Steam или из Вашей папки, шрифты и иконки читает из установленной игры. Русский и английский интерфейс, готовый exe.
```

Английский:

```
A Windows radio for the Easy Delivery Co. soundtrack. Seven stations on the in-game frequencies, a clock-driven broadcast, static between stations, tape, sleep and an overlay on top of the game with Ctrl+Alt+O. Music comes from Steam or your folder, fonts and icons from the installed game. Russian and English UI, ready-made exe.
```

## Website

```
https://github.com/KrekerDM/edc-radio/releases/latest
```

## Topics

```
easy-delivery-co
music-player
radio
soundtrack
pixel-art
1-bit
winforms
dotnet
csharp
windows
desktop-app
russian
```

## Сайт

Страница проекта лежит в `site/`: HTML, CSS, шрифты и картинки, сборка не
нужна. Проект в Vercel настраивается так:

- Root Directory — `site`;
- Framework Preset — Other;
- Build Command и Output Directory — пустые.

`site/vercel.json` убирает `.html` из адресов и задаёт кэш для шрифтов и
картинок. Когда сайт заработает, его адрес ставится в поле Website вместо
ссылки на релизы.

## Настройки репозитория

- Лицензия MIT, файл `LICENSE` уже в репозитории.
- Issues включены, туда приходят отзывы о станциях и папках с музыкой.
- Скриншоты в `docs/` сняты без игры, своим паком. Шрифты и иконки игры
  принадлежат её автору, поэтому на скриншоты они не попадают.
- Social preview не задан. Без него GitHub показывает свою карточку с
  названием, описанием и счётчиками.
