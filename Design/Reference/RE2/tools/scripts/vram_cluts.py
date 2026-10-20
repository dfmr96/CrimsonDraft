import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from PIL import Image
from psx_extract import Disc
im = Image.open(sys.argv[2]).convert("RGB")
S = im.width / 1024
def vram(x, y):
    r, g, b = im.getpixel((int(x * S + S / 2), int(y * S + S / 2)))
    return (round(r * 31 / 255), round(g * 31 / 255), round(b * 31 / 255))
def to5(c): return ((c & 31), (c >> 5) & 31, (c >> 10) & 31)
d = Disc(sys.argv[3])
def pals(tim):
    _, cx, cy, cw, ch = struct.unpack_from("<IHHHH", tim, 8)
    return {(f"r{r}", seg): struct.unpack_from("<16H", tim, 20 + 2 * cw * r + 2 * seg) for r in range(ch) for seg in range(0, cw, 16)}, cy
moji, my = pals(open(os.path.join(sys.argv[1], "psx", "beta2", "tim", "ST_MOJI.TIM"), "rb").read())
tex, ty = pals(d.read(*d.find("COMMON\\DATA\\TEX.TIM")))
def match(live, table, base_y, label):
    best = None
    for (r, seg), pal in table.items():
        if seg: continue
        err = sum(abs(a - b) for i in range(1, 16) for a, b in zip(to5(pal[i]), live[i]))
        if best is None or err < best[0]: best = (err, int(r[1:]), label)
    return best
for y in range(480, 498):
    live = [vram(256 + i, y) for i in range(16)]
    m1, m2 = match(live, moji, my, "ST_MOJI"), match(live, tex, ty, "TEX")
    best = min(m1, m2)
    print(f"VRAM fila {y}: mejor = {best[2]} paleta {best[1]} (error {best[0]})   [ST_MOJI p{m1[1]} err {m1[0]} | TEX p{m2[1]} err {m2[0]}]")
