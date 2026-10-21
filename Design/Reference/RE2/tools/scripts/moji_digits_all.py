import sys, os, struct
from PIL import Image, ImageDraw
moji = open(os.path.join(sys.argv[1], "psx", "beta2", "tim", "ST_MOJI.TIM"), "rb").read()
_, cx, cy, cw, ch = struct.unpack_from("<IHHHH", moji, 8)
pals = [struct.unpack_from("<16H", moji, 20 + 2 * cw * r) for r in range(ch)]
p = 8 + struct.unpack_from("<I", moji, 8)[0]; w16 = struct.unpack_from("<H", moji, p + 8)[0]; pix = moji[p + 12:]
idx = lambda x, y: (pix[y * w16 * 2 + x // 2] >> (4 if x & 1 else 0)) & 15
rgb = lambda c: ((c & 31) * 255 // 31, ((c >> 5) & 31) * 255 // 31, ((c >> 10) & 31) * 255 // 31)
Z = 3; DW, DY0, DH = 60, 24, 13
cols = 3
img = Image.new("RGB", (cols * (DW * Z + 130), (ch // cols + 1) * (DH * Z + 10) + 10), (60, 60, 70)); dr = ImageDraw.Draw(img)
for k, pal in enumerate(pals):
    x0 = (k % cols) * (DW * Z + 130) + 110; y0 = (k // cols) * (DH * Z + 10) + 10
    dr.text((x0 - 105, y0 + 12), f"paleta {k:2d} (fila {cy + k})", fill=(230, 230, 230))
    for y in range(DH):
        for x in range(DW):
            v = idx(x, DY0 + y)
            if v: dr.rectangle([x0 + x * Z, y0 + y * Z, x0 + x * Z + Z - 1, y0 + y * Z + Z - 1], fill=rgb(pal[v]))
img.save(os.path.join(sys.argv[1], "psx", "beta2", "tim", "moji_digits_all.png"))
