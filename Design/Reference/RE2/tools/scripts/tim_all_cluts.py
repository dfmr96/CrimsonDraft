import sys, os, struct
from PIL import Image
t = os.path.join(sys.argv[1], "psx", "beta2", "tim")
for name in ("ST0", "ST1"):
    tim = open(os.path.join(t, name + ".TIM"), "rb").read()
    clen, _, _, cw, ch = struct.unpack_from("<IHHHH", tim, 8)
    cluts = [struct.unpack_from(f"<{cw}H", tim, 20 + 2 * cw * i) for i in range(ch)]
    p = 8 + clen; blen, _, _, w16, h = struct.unpack_from("<IHHHH", tim, p); pix = tim[p + 12:p + blen]; W = w16 * 2
    rgb = lambda c: ((c & 31) * 8, ((c >> 5) & 31) * 8, ((c >> 10) & 31) * 8)
    sheet = Image.new("RGB", (W * len(cluts), h))
    for k, cl in enumerate(cluts):
        for y in range(h):
            for x in range(W):
                sheet.putpixel((k * W + x, y), rgb(cl[pix[y * W + x]]))
    sheet.save(os.path.join(t, f"{name}_cluts.png"))
    print(name, len(cluts), "cluts")
