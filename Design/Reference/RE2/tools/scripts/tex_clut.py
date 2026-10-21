import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
d = Disc(sys.argv[2]); tim = d.read(*d.find("COMMON\\DATA\\TEX.TIM"))
flags = struct.unpack_from("<I", tim, 4)[0]
clen, cx, cy, cw, ch = struct.unpack_from("<IHHHH", tim, 8)
print(f"TEX.TIM mode {flags & 7}, CLUT at x={cx} y={cy}, {cw} x {ch}")
for r in range(ch):
    row = struct.unpack_from(f"<{cw}H", tim, 20 + 2 * cw * r)
    for seg in range(0, cw, 16):
        pal = row[seg:seg + 16]
        g = [((c >> 5) & 31) for c in pal]
        if sum(1 for c in pal if (c & 31) <= 1 and ((c >> 5) & 31) >= 8 and ((c >> 10) & 31) <= 5) >= 4:
            x = cx + seg
            clutid = (cy + r) << 6 | (x >> 4)
            print(f"  green palette at VRAM x={x} y={cy + r}  clut id 0x{clutid:04X}  -> F9 p = {((cy + r) - 480) / 2 if x == 256 else 'n/a (x != 256)'}")
            print("    ", [hex(c) for c in pal])
