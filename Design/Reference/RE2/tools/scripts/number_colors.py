import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
from PIL import Image, ImageDraw
d = Disc(sys.argv[2]); tex = d.read(*d.find("COMMON\\DATA\\TEX.TIM"))
moji = open(os.path.join(sys.argv[1], "psx", "beta2", "tim", "ST_MOJI.TIM"), "rb").read()
def rows(tim):
    _, cx, cy, cw, ch = struct.unpack_from("<IHHHH", tim, 8)
    return {cy + r: struct.unpack_from("<16H", tim, 20 + 2 * cw * r) for r in range(ch)}
src = {"TEX.TIM": rows(tex), "ST_MOJI.TIM": rows(moji)}
p = 8 + struct.unpack_from("<I", moji, 8)[0]; w16 = struct.unpack_from("<H", moji, p + 8)[0]; pix = moji[p + 12:]
idx = lambda x, y: (pix[y * w16 * 2 + x // 2] >> (4 if x & 1 else 0)) & 15
rgb = lambda c: ((c & 31) * 255 // 31, ((c >> 5) & 31) * 255 // 31, ((c >> 10) & 31) * 255 // 31)
Z, DW, DY0, DH = 4, 108, 24, 13            # digit row "0123456789" in ST_MOJI (y ~ 9..19)
img = Image.new("RGB", (2 * (DW * Z + 20) + 120, 4 * (DH * Z + 26) + 30), (60, 60, 70)); dr = ImageDraw.Draw(img)
for col, (name, pal) in enumerate(src.items()):
    x0 = 120 + col * (DW * Z + 20); dr.text((x0, 6), name, fill=(240, 240, 240))
    for c in range(4):
        row = 490 + c; y0 = 26 + c * (DH * Z + 26)
        if col == 0: dr.text((6, y0 + 10), f"color {c}\n(fila {row})", fill=(220, 220, 220))
        for y in range(DH):
            for x in range(DW):
                v = idx(x, DY0 + y)
                if v: dr.rectangle([x0 + x * Z, y0 + y * Z, x0 + x * Z + Z - 1, y0 + y * Z + Z - 1], fill=rgb(pal[row][v]))
img.save(os.path.join(sys.argv[1], "psx", "beta2", "tim", "number_colors.png"))
print("ok")
