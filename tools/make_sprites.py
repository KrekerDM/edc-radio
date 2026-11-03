import io
import struct
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "src" / "EdcRadio" / "Assets"
CODE = ROOT / "src" / "EdcRadio" / "Sprites.cs"
WIDTH = 128
ICON_SIZES = (16, 24, 32, 48, 64, 128, 256)

TRUCK = [
    "            #####   ",
    "   #####    #...##  ",
    "   #.#.#    #...#.# ",
    "   ##.##    #...#..#",
    "   #.#.#    #####..#",
    " ###########.......#",
    " #.........#.......#",
    " ###################",
    "  .##.        .##.  ",
    "  #{}#        #{}#  ",
    "  #{}#        #{}#  ",
    "   ##          ##   ",
]

SPRITES = [
    ("TruckA", True, [row.format("..", "..") if "{" in row else row for row in TRUCK[:9]]
     + ["  #..#        #..#  ", "  #.##        #.##  ", "   ##          ##   "]),
    ("TruckB", True, [row for row in TRUCK[:9]]
     + ["  ##.#        ##.#  ", "  #..#        #..#  ", "   ##          ##   "]),
    ("Tower", True, [
        "    #    ",
        "    #    ",
        "    #    ",
        "   ###   ",
        "   # #   ",
        "   ###   ",
        "   # #   ",
        "  #####  ",
        "  ## ##  ",
        "  # # #  ",
        "  ## ##  ",
        "  #####  ",
        " ##   ## ",
        " # # # # ",
        " #  #  # ",
        " # # # # ",
        " ##   ## ",
        " ####### ",
        "##     ##",
        "# #   # #",
        "#  # #  #",
        "#   #   #",
        "#  # #  #",
        "# #   # #",
        "##     ##",
        "#       #",
    ]),
    ("PineL", False, [
        "     #     ",
        "    # #    ",
        "     #     ",
        "   # # #   ",
        "    # #    ",
        "   # # #   ",
        "  # # # #  ",
        "   # # #   ",
        "  # # # #  ",
        " # # # # # ",
        "# # # # # #",
        "     #     ",
        "     #     ",
    ]),
    ("PineS", False, [
        "   #   ",
        "  # #  ",
        "   #   ",
        " # # # ",
        "  # #  ",
        " # # # ",
        "# # # #",
        "   #   ",
        "   #   ",
    ]),
    ("Drift", False, [
        "     # # #       ",
        "  # # # # # #    ",
        "# # # # # # # # #",
    ]),
    ("CatA", True, [
        "#   #     ",
        "## ##     ",
        "#####    #",
        "#.#.#    #",
        "#####   # ",
        " ###    # ",
        "#####  #  ",
        "###### #  ",
        "#######   ",
    ]),
    ("CatB", True, [
        "#   #     ",
        "## ##     ",
        "#####     ",
        "#.#.#     ",
        "#####     ",
        " ###      ",
        "#####     ",
        "######  ##",
        "########  ",
    ]),
    ("KitSleep", True, [
        "  #   #  ",
        "  ## ##  ",
        " ######  ",
        "######## ",
        "#########",
        " ########",
        "  #######",
    ]),
    ("Radio", False, [
        "          #   ",
        "         #    ",
        "        #     ",
        "##############",
        "#............#",
        "#.####...###.#",
        "#.#..#..#.#.##",
        "#.####...###.#",
        "##############",
    ]),
    ("Heart", False, [
        " ## ## ",
        "#######",
        "#######",
        " ##### ",
        "  ###  ",
        "   #   ",
    ]),
    ("NoteA", False, [
        "   #  ",
        "   ## ",
        "   # #",
        "   #  ",
        " ###  ",
        "####  ",
        " ##   ",
    ]),
    ("NoteB", False, [
        " #####",
        " #   #",
        " #   #",
        " #   #",
        "##  ##",
        "## ###",
        "    # ",
    ]),
    ("Moon", False, [
        "    #    ",
        "  ##     ",
        " ###     ",
        " ###     ",
        "####     ",
        "####     ",
        " ####    ",
        " #####   ",
        "  ###### ",
        "    ##   ",
    ]),
    ("Zz", False, [
        "####",
        "  # ",
        " #  ",
        "####",
    ]),
    ("Blink", False, [
        " # ",
        "###",
        " # ",
    ]),
    ("Flake", False, [
        " # ",
        "###",
        " # ",
    ]),
]


def framed(rows, outline):
    width = max(len(r) for r in rows)
    grid = [list(r.ljust(width)) for r in rows]
    if not outline:
        return grid
    height = len(grid)
    grid = [[" "] * (width + 2)] + [[" "] + r + [" "] for r in grid] + [[" "] * (width + 2)]
    out = [r[:] for r in grid]
    for y in range(height + 2):
        for x in range(width + 2):
            if grid[y][x] != " ":
                continue
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    yy, xx = y + dy, x + dx
                    if 0 <= yy < height + 2 and 0 <= xx < width + 2 and grid[yy][xx] == "#":
                        out[y][x] = "."
    return out


def icon(grid, size):
    w, h = len(grid[0]), len(grid)
    sprite = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y, line in enumerate(grid):
        for x, c in enumerate(line):
            if c == "#":
                sprite.putpixel((x, y), (255, 255, 255, 255))
    margin = max(1, size // 8)
    scale = max(1, (size - 2 * margin) // w)
    sprite = sprite.resize((w * scale, h * scale), Image.NEAREST)
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    corner = max(1, size // 16)
    image.paste((0, 0, 0, 255), (corner, 0, size - corner, size))
    image.paste((0, 0, 0, 255), (0, corner, size, size - corner))
    image.alpha_composite(sprite, ((size - sprite.width) // 2, (size - sprite.height) // 2))
    return image


def write_icon(grid):
    blobs = []
    for size in ICON_SIZES:
        buffer = io.BytesIO()
        icon(grid, size).save(buffer, format="PNG")
        blobs.append(buffer.getvalue())
    header = struct.pack("<HHH", 0, 1, len(ICON_SIZES))
    offset = 6 + 16 * len(ICON_SIZES)
    entries = b""
    for size, blob in zip(ICON_SIZES, blobs):
        side = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", side, side, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
    (ASSETS / "app.ico").write_bytes(header + entries + b"".join(blobs))


def main():
    grids = [(name, framed(rows, outline)) for name, outline, rows in SPRITES]
    places = []
    x = y = row = 0
    for name, grid in grids:
        w, h = len(grid[0]), len(grid)
        if x + w > WIDTH:
            x, y, row = 0, y + row + 1, 0
        places.append((name, x, y, w, h, grid))
        x += w + 1
        row = max(row, h)
    height = (y + row + 7) // 8 * 8
    sheet = Image.new("RGBA", (WIDTH, height), (0, 0, 0, 0))
    for name, px, py, w, h, grid in places:
        for gy, line in enumerate(grid):
            for gx, c in enumerate(line):
                if c == "#":
                    sheet.putpixel((px + gx, py + gy), (255, 255, 255, 255))
                elif c == ".":
                    sheet.putpixel((px + gx, py + gy), (0, 0, 0, 255))
    sheet.save(ASSETS / "sprites.png")
    write_icon(dict((name, grid) for name, grid in grids)["Radio"])
    fields = "\n".join(
        f"        public static readonly Rectangle {name} = new Rectangle({px}, {py}, {w}, {h});"
        for name, px, py, w, h, _ in places
    )
    CODE.write_text(
        "using System.Drawing;\n"
        "using System.IO;\n"
        "using System.Reflection;\n"
        "\n"
        "namespace EdcRadio\n"
        "{\n"
        "    internal static class Sprites\n"
        "    {\n"
        f"{fields}\n"
        "\n"
        "        private static Bitmap _pack;\n"
        "\n"
        "        public static Bitmap Pack\n"
        "        {\n"
        "            get\n"
        "            {\n"
        "                if (_pack != null) return _pack;\n"
        "                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(\"EdcRadio.Assets.sprites.png\"))\n"
        "                    _pack = new Bitmap(s);\n"
        "                return _pack;\n"
        "            }\n"
        "        }\n"
        "    }\n"
        "}\n",
        encoding="utf-8",
        newline="\r\n",
    )


if __name__ == "__main__":
    main()
