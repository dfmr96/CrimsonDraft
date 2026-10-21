import sys, os, struct
from PIL import Image
moji = open(os.path.join(sys.argv[1], "psx", "beta2", "tim", "ST_MOJI.TIM"), "rb").read()
p = 8 + struct.unpack_from("<I", moji, 8)[0]; w16 = struct.unpack_from("<H", moji, p + 8)[0]; pix = moji[p + 12:]
idx = lambda x, y: (pix[y * w16 * 2 + x // 2] >> (4 if x & 1 else 0)) & 15
img = Image.new("L", (128, 40))
for y in range(40):
    for x in range(128):
        img.putpixel((x, y), 0 if idx(x, y) == 0 else 60 + idx(x, y) * 12)
big = img.resize((128 * 6, 40 * 6), Image.NEAREST).convert("RGB")
from PIL import ImageDraw
dr = ImageDraw.Draw(big)
for y in range(0, 40, 4):
    dr.line([(0, y * 6), (8, y * 6)], fill=(255, 0, 0)); dr.text((10, y * 6 - 5), str(y), fill=(255, 80, 80))
big.save(os.path.join(sys.argv[1], "psx", "beta2", "tim", "moji_top.png"))
