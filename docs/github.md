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
Радио из кабины Easy Delivery Co. для Windows. Семь станций, шесть из них на частотах из игры, эфир по часам, шум между станциями, кассета, сон и оверлей поверх игры по Ctrl+Alt+O. Музыку берёт из Steam или из Вашей папки, шрифты и иконки читает из установленной игры. Русский и английский интерфейс, готовый exe.
```

Английский:

```
The Easy Delivery Co. truck radio for Windows. Seven stations, six of them on the in-game frequencies, a clock-driven broadcast, static between stations, tape, sleep and an overlay on top of the game with Ctrl+Alt+O. Music comes from Steam or your folder, fonts and icons from the installed game. Russian and English UI, ready-made exe.
```

## Website

```
https://edc-radio-site.vercel.app/
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

Сайт работает по адресу https://edc-radio-site.vercel.app/, английская
версия — https://edc-radio-site.vercel.app/en/. Страница лежит в `site/`:
HTML, CSS, шрифты и картинки, сборка не нужна. Проект в Vercel настроен так:

- Root Directory — `site`;
- Framework Preset — Other;
- Build Command и Output Directory — пустые.

Каждый push в `main` выкладывает сайт заново. `site/vercel.json` убирает
`.html` из адресов и задаёт кэш для шрифтов и картинок, `site/404.html`
открывается на неизвестных адресах. Если адрес сайта поменяется, его нужно
заменить в `canonical`, `hreflang` и `og:` обеих страниц.

Шрифты Vercel отдаёт с кэшем на год, картинки на неделю. Поэтому изменённый
шрифт или лист спрайтов кладётся под новым именем, например
`ibm-plex-mono-latin-2.woff2`, а ссылки в `style.css`, `scene.js` и `<head>`
меняются в том же коммите. Таблица спрайтов в `scene.js` повторяет
`src/EdcRadio/Sprites.cs`, и после `tools/make_sprites.py` её нужно сверить.

Адрес сайта и картинки `site/img/rpc/radio.png`, `tower.png` и `tape.png`
вшиты в плеер: их показывает статус в Discord у всех, кто поставил релиз.
Поэтому эти файлы не переносятся и не переименовываются. Новая картинка
кладётся под новым именем вместе с новым релизом, а старая остаётся на месте.

## Настройки репозитория

- Лицензия MIT, файл `LICENSE` уже в репозитории.
- Issues включены, туда приходят отзывы о станциях и папках с музыкой.
- Скриншоты в `docs/` сняты без игры, своим паком. Шрифты и иконки игры
  принадлежат её автору, поэтому на скриншоты они не попадают.
- Social preview не задан. Без него GitHub показывает свою карточку с
  названием, описанием и счётчиками.
