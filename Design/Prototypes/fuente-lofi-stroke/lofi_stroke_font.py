"""Prototype of the Lo-Fi Stroke font effect: render text with a pixel TTF (Silver).
Layers: fill shades 1-3 (stable noise), corner smoothing 4-6, 8-connected 1px border (8), background 0.
Indices 1-8 are one light-to-dark ramp; the text colour swaps the whole ramp, border included.
Usage: python -I lofi_stroke_font.py <font.ttf> <out.png> [scale] [bold]  (bold: thicken strokes 1px right)
"""
import sys, zlib
from PIL import Image, ImageDraw, ImageFont

FONT, OUT = sys.argv[1], sys.argv[2]
SCALE = int(sys.argv[3]) if len(sys.argv) > 3 else 4
BOLD = len(sys.argv) > 4 and sys.argv[4] == "bold"
PX = 19  # Silver: 1900 units/em, 100 units per pixel -> 19 px is native

# palettes: index -> RGB (0 = transparent). 8-step light-to-dark ramps; the last step is the border.
PALETTES = {
    "white": {1: (216, 216, 200), 2: (200, 200, 184), 3: (168, 168, 152), 4: (136, 136, 128),
              5: (112, 112, 104), 6: (80, 80, 80), 7: (56, 56, 56), 8: (56, 48, 72)},
    "green": {1: (0, 184, 40), 2: (0, 160, 32), 3: (0, 136, 24), 4: (0, 120, 16),
              5: (0, 96, 16), 6: (0, 80, 8), 7: (0, 56, 0), 8: (0, 40, 0)},
    "red":   {1: (152, 0, 72), 2: (144, 0, 64), 3: (120, 0, 40), 4: (96, 0, 24),
              5: (80, 0, 0), 6: (72, 0, 0), 7: (56, 0, 0), 8: (16, 0, 0)},
    "blue":  {1: (32, 80, 232), 2: (24, 72, 224), 3: (24, 64, 200), 4: (24, 56, 168),
              5: (16, 48, 120), 6: (16, 32, 104), 7: (16, 16, 80), 8: (8, 8, 64)},
}

def raster(text):
    f = ImageFont.truetype(FONT, PX)
    l, t, r, b = f.getbbox(text)
    img = Image.new("1", (r + 4, PX + 6), 0)
    ImageDraw.Draw(img).text((2, 2), text, font=f, fill=1)
    if BOLD:
        from PIL import ImageChops
        img = ImageChops.logical_or(img, ImageChops.offset(img, 1, 0))
    return img

def shade(x, y):
    hsh = zlib.crc32(bytes((x & 255, x >> 8, y & 255, y >> 8))) % 100
    return 1 if hsh < 28 else (2 if hsh < 75 else 3)   # ~28/47/25 light/mid/dark

def indexed(mask):
    W, H = mask.size
    m = lambda x, y: 0 <= x < W and 0 <= y < H and mask.getpixel((x, y)) != 0
    out = [[0] * W for _ in range(H)]
    for y in range(H):
        for x in range(W):
            if m(x, y):
                out[y][x] = shade(x, y)
    # corner smoothing: empty pixel with fill on two orthogonal sides (concave corner)
    for y in range(H):
        for x in range(W):
            if out[y][x] == 0 and not m(x, y):
                h = m(x - 1, y) or m(x + 1, y)
                v = m(x, y - 1) or m(x, y + 1)
                if h and v:
                    out[y][x] = 5
    # 8-connected border around fill and smoothing
    solid = [[out[y][x] != 0 for x in range(W)] for y in range(H)]
    for y in range(H):
        for x in range(W):
            if not solid[y][x] and any(0 <= x + dx < W and 0 <= y + dy < H and solid[y + dy][x + dx]
                                       for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                out[y][x] = 8
    return out

def colour(idx, pal, bg=(0, 0, 0)):
    H, W = len(idx), len(idx[0])
    img = Image.new("RGB", (W, H), bg)
    for y in range(H):
        for x in range(W):
            v = idx[y][x]
            if v:
                img.putpixel((x, y), pal[v])
    return img

lines = [("white", "Will you use the Ink Ribbon?"), ("green", "F. Aid Spray  Handgun Ammo"),
         ("red", "Danger"), ("blue", "Crimson Draft  0123456789"), ("white", "インクリボンを使いますか？")]
rows = [colour(indexed(raster(t)), PALETTES[p]) for p, t in lines]
W = max(r.width for r in rows); H = sum(r.height for r in rows)
sheet = Image.new("RGB", (W, H), (0, 0, 0)); y = 0
for r in rows:
    sheet.paste(r, (0, y)); y += r.height
sheet.resize((W * SCALE, H * SCALE), Image.NEAREST).save(OUT)
# plain 1x reference without effects for comparison
plain = Image.new("RGB", (W, H), (0, 0, 0)); y = 0
for p, t in lines:
    m = raster(t); plain.paste(Image.merge("RGB", [m.convert("L")] * 3), (0, y)); y += m.height
plain.resize((W * SCALE, H * SCALE), Image.NEAREST).save(OUT.replace(".png", "_plain.png"))
print("ok", W, H)
