import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
from beta_english import disc_write
from beta_menu_copies import rows
t = os.path.join(sys.argv[1], "psx", "beta2", "tim")
o_rows, n_rows = rows(open(os.path.join(t, "ST_MOJI.TIM"), "rb").read()), rows(open(os.path.join(t, "ST_MOJI_EN.TIM"), "rb").read())
iso = sys.argv[2]; disc = Disc(iso)
lba, size = disc.find("COMMON\\DATA\\ST0.TIM")
data = bytearray(disc.read(lba, size))
orig_st0 = bytearray(Disc(sys.argv[3]).read(*Disc(sys.argv[3]).find("COMMON\\DATA\\ST0.TIM")))
CELLS = slice(0, 96)                                  # 5 cells x 24 bytes (4bpp, 48 px each)
needle = o_rows[60][CELLS]
pos = orig_st0.find(needle)
for stride in (128,):
    ok = all(orig_st0[pos + (y - 60) * stride: pos + (y - 60) * stride + 96] == o_rows[y][CELLS] for y in range(56, 72))
    print(f"stride {stride}: rows 56-71 match = {ok}")
    if ok:
        for y in range(56, 72):
            a = pos + (y - 60) * stride
            data[a:a + 96] = n_rows[y][CELLS]
        with open(iso, "r+b") as f:
            disc_write(f, disc, lba, 0, bytes(data))
        print("ST0.TIM patched with stride", stride)
        break
