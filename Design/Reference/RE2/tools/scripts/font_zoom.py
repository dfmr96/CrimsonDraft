import sys, os, struct
from collections import Counter
from PIL import Image
t = os.path.join(sys.argv[1], "psx", "beta2", "tim")
tim = open(os.path.join(t, "ST1_FONT.TIM"), "rb").read()
clen, _, _, cw, ch = struct.unpack_from("<IHHHH", tim, 8)
cluts = [struct.unpack_from(f"<{cw}H", tim, 20 + 2 * cw * i) for i in range(ch)]
p = 8 + clen; blen, _, _, w16, h = struct.unpack_from("<IHHHH", tim, p); pix = tim[p + 12:p + blen]; W = w16 * 4
idx = lambda x, y: (pix[y * w16 * 2 + x // 2] >> (4 if x & 1 else 0)) & 15
hist = Counter(idx(x, y) for y in range(h) for x in range(W))
print("pixel index usage:", sorted(hist.items()))
rgb = lambda c: ((c & 31) * 255 // 31, ((c >> 5) & 31) * 255 // 31, ((c >> 10) & 31) * 255 // 31)
print("CLUT0:", [rgb(c) for c in cluts[0][:8]])
crop = Image.new("RGB", (64, 16))
for y in range(16):
    for x in range(64):
        crop.putpixel((x, y), rgb(cluts[0][idx(x, y)]))
crop.resize((64 * 12, 16 * 12), Image.NEAREST).save(os.path.join(t, "font_zoom.png"))
