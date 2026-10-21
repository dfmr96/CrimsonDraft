import sys, os, struct
from collections import Counter
tim = open(os.path.join(sys.argv[1], "psx", "beta2", "tim", "ST_MOJI.TIM"), "rb").read()
p = 8 + struct.unpack_from("<I", tim, 8)[0]
blen, ix, iy, w16, h = struct.unpack_from("<IHHHH", tim, p); pix = tim[p + 12:p + blen]
idx = lambda x, y: (pix[y * w16 * 2 + x // 2] >> (4 if x & 1 else 0)) & 15
for cell in range(5):
    c = Counter(idx(x, y) for y in range(56, 72) for x in range(cell * 48, cell * 48 + 48))
    print(f"cell {cell}: {c.most_common()}")
clut = struct.unpack_from("<16H", tim, 8 + 12)
print("CLUT0:", [hex(v) for v in clut])
