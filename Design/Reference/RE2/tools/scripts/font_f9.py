import sys, os, struct
from PIL import Image, ImageDraw
t = os.path.join(sys.argv[1], "psx", "beta2", "tim")
font = open(os.path.join(t, "ST1_FONT.TIM"), "rb").read(); moji = open(os.path.join(t, "ST_MOJI.TIM"), "rb").read()
def cluts(tim):
    _, _, cy, cw, ch = struct.unpack_from("<IHHHH", tim, 8)
    return {cy + i: struct.unpack_from(f"<{cw}H", tim, 20 + 2 * cw * i) for i in range(ch)}
vram = cluts(moji); vram.update(cluts(font))          # font uploaded last -> owns rows 480-481
p = 8 + struct.unpack_from("<I", font, 8)[0]; w16 = struct.unpack_from("<H", font, p + 8)[0]; pix = font[p + 12:]
idx = lambda x, y: (pix[y * w16 * 2 + x // 2] >> (4 if x & 1 else 0)) & 15
rgb = lambda c: ((c & 31) * 255 // 31, ((c >> 5) & 31) * 255 // 31, ((c >> 10) & 31) * 255 // 31)
Z, N = 5, 9
img = Image.new("RGB", (N * (48 * Z + 10) + 10, 16 * Z + 40), (40, 40, 48)); d = ImageDraw.Draw(img)
for k in range(N):                                    # F9 p -> row 480 + 2p
    row = 480 + 2 * k; cl = vram.get(row); x0 = 10 + k * (48 * Z + 10)
    d.text((x0, 4), f"F9 {k:02X} -> fila {row}", fill=(230, 230, 230))
    if not cl: continue
    for y in range(16):
        for x in range(48):
            v = idx(x, y)
            if v: d.rectangle([x0 + x * Z, 22 + y * Z, x0 + x * Z + Z - 1, 22 + y * Z + Z - 1], fill=rgb(cl[v]))
img.save(os.path.join(t, "font_f9_colors.png"))
