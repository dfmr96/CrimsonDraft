"""Redraw the inventory command words in ST_MOJI.TIM (Beta 2) in English.

Cells are 48x16 at y=56 (from the sprite table at 0x8009DD40). Background index 14, text uses the
original anti-aliasing ramp (6 = full text ... 12 = faint edge), so it matches the Japanese style.

Usage: venv python -I beta_menu_tim.py <ST_MOJI.TIM in> <ST_MOJI.TIM out> <preview.png> [<iso copy (EN> --apply]
"""
import os
import struct
import sys

from PIL import Image, ImageDraw, ImageFont

WORDS = ["EQUIP", "USE", "CHECK", "COMBINE", "PARTNER"]
CELL_W, CELL_H, ROW_Y = 48, 16, 56
BG = 14
RAMP = [12, 11, 10, 9, 8, 7, 6]          # faint edge -> solid text
FONTS = ["arialnb.ttf", "tahomabd.ttf", "arialbd.ttf"]


def pick_font(word):
    for name in FONTS:
        path = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", name)
        if not os.path.exists(path):
            continue
        for size in range(14, 6, -1):
            f = ImageFont.truetype(path, size)
            l, t, r, b = f.getbbox(word)
            if r - l <= CELL_W - 2 and b - t <= CELL_H - 3:
                return f
    raise RuntimeError("no font fits")


def main():
    src, dst, preview = sys.argv[1], sys.argv[2], sys.argv[3]
    tim = bytearray(open(src, "rb").read())
    clut_len = struct.unpack_from("<I", tim, 8)[0]
    clut = struct.unpack_from("<16H", tim, 8 + 12)
    p = 8 + clut_len
    blen, _, _, w16, h = struct.unpack_from("<IHHHH", tim, p)
    pix_off = p + 12

    def set_idx(x, y, v):
        o = pix_off + y * w16 * 2 + x // 2
        tim[o] = (tim[o] & 0x0F) | (v << 4) if x & 1 else (tim[o] & 0xF0) | v

    # One size for every word: the one that fits the longest.
    font = pick_font(max(WORDS, key=len))
    for cell, word in enumerate(WORDS):
        img = Image.new("L", (CELL_W, CELL_H), 0)
        d = ImageDraw.Draw(img)
        l, t, r, b = font.getbbox(word)
        d.text(((CELL_W - (r - l)) // 2 - l, (CELL_H - (b - t)) // 2 - t), word, fill=255, font=font)
        for y in range(CELL_H):
            for x in range(CELL_W):
                a = img.getpixel((x, y))
                v = BG if a < 40 else RAMP[min(len(RAMP) - 1, (a - 40) * len(RAMP) // 216)]
                set_idx(cell * CELL_W + x, ROW_Y + y, v)
    open(dst, "wb").write(tim)

    # Preview with CLUT 0, enlarged 4x.
    W = w16 * 4
    rgb = lambda c: ((c & 31) * 255 // 31, ((c >> 5) & 31) * 255 // 31, ((c >> 10) & 31) * 255 // 31)
    out = Image.new("RGB", (W, 32))
    for y in range(40, 72):
        for x in range(W):
            o = pix_off + y * w16 * 2 + x // 2
            v = (tim[o] >> 4) if x & 1 else (tim[o] & 15)
            out.putpixel((x, y - 40), rgb(clut[v]))
    out.resize((W * 4, 128), Image.NEAREST).save(preview)
    print("written", dst, "and preview", preview)

    if "--apply" in sys.argv:
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        from psx_extract import Disc
        from beta_english import disc_write
        iso = sys.argv[4]
        if "(EN" not in os.path.basename(iso):
            sys.exit("Por seguridad solo se parchean copias con '(EN' en el nombre.")
        disc = Disc(iso)
        lba, size = disc.find("COMMON\\DATA\\ST_MOJI.TIM")
        assert size == len(tim)
        with open(iso, "r+b") as f:
            disc_write(f, disc, lba, 0, bytes(tim))
        print("patched ST_MOJI.TIM in", os.path.basename(iso))


if __name__ == "__main__":
    main()
