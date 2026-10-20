import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
from PIL import Image, ImageDraw
d = Disc(sys.argv[2]); tex = d.read(*d.find("COMMON\\DATA\\TEX.TIM"))
_, cx, cy, cw, ch = struct.unpack_from("<IHHHH", tex, 8)
rows = {cy + r: struct.unpack_from("<16H", tex, 20 + 2 * cw * r) for r in range(ch)}   # x = 256 column
font = open(os.path.join(sys.argv[1], "psx", "beta2", "tim", "ST1_FONT.TIM"), "rb").read()
p = 8 + struct.unpack_from("<I", font, 8)[0]; w16 = struct.unpack_from("<H", font, p + 8)[0]; pix = font[p + 12:]
idx = lambda x, y: (pix[y * w16 * 2 + x // 2] >> (4 if x & 1 else 0)) & 15
rgb = lambda c: ((c & 31) * 255 // 31, ((c >> 5) & 31) * 255 // 31, ((c >> 10) & 31) * 255 // 31)
Z, N = 6, 9
img = Image.new("RGB", (N * (32 * Z + 12) + 10, 16 * Z + 34), (60, 60, 70)); dr = ImageDraw.Draw(img)
for k in range(N):
    row = 480 + 2 * k; pal = rows.get(row); x0 = 10 + k * (32 * Z + 12)
    dr.text((x0, 4), f"F9 {k:02X}  (fila {row})", fill=(235, 235, 235))
    if not pal: continue
    for y in range(16):
        for x in range(32):
            v = idx(x, y)
            if v: dr.rectangle([x0 + x * Z, 22 + y * Z, x0 + x * Z + Z - 1, 22 + y * Z + Z - 1], fill=rgb(pal[v]))
img.save(os.path.join(sys.argv[1], "psx", "beta2", "tim", "text_colors.png"))
print("ok")
