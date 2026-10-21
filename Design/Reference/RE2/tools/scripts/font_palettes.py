import sys, os, struct
from PIL import Image, ImageDraw
t = os.path.join(sys.argv[1], "psx", "beta2", "tim")
tim = open(os.path.join(t, "ST1_FONT.TIM"), "rb").read()
clen, _, _, cw, ch = struct.unpack_from("<IHHHH", tim, 8)
cluts = [struct.unpack_from(f"<{cw}H", tim, 20 + 2 * cw * i) for i in range(ch)]
p = 8 + clen; blen, _, _, w16, h = struct.unpack_from("<IHHHH", tim, p); pix = tim[p + 12:p + blen]
idx = lambda x, y: (pix[y * w16 * 2 + x // 2] >> (4 if x & 1 else 0)) & 15
rgb = lambda c: ((c & 31) * 255 // 31, ((c >> 5) & 31) * 255 // 31, ((c >> 10) & 31) * 255 // 31)
Z, GW, GH, SW = 8, 96, 32, 28
img = Image.new("RGB", (40 + 16 * SW + 20 + GW * Z, 20 + len(cluts) * (GH * Z + 30)), (30, 30, 36))
d = ImageDraw.Draw(img)
for k, cl in enumerate(cluts):
    y0 = 20 + k * (GH * Z + 30)
    d.text((10, y0 - 15), f"Paleta {k}", fill=(230, 230, 230))
    for i in range(16):                                   # swatches (index 0 = transparent)
        x0 = 10 + i * SW
        d.rectangle([x0, y0, x0 + SW - 4, y0 + 40], fill=rgb(cl[i]), outline=(90, 90, 90))
        d.text((x0 + 2, y0 + 44), str(i), fill=(200, 200, 200))
    gx = 40 + 16 * SW
    for y in range(GH):                                   # first glyphs rendered with this palette
        for x in range(GW):
            v = idx(x, y)
            c = (30, 30, 36) if v == 0 else rgb(cl[v])
            d.rectangle([gx + x * Z, y0 + y * Z, gx + x * Z + Z - 1, y0 + y * Z + Z - 1], fill=c)
img.save(os.path.join(t, "font_palettes.png"))
print([[hex(c) for c in cl] for cl in cluts])
