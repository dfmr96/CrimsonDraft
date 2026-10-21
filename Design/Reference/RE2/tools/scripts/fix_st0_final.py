import sys, os
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
from beta_english import disc_write
from beta_menu_copies import rows
t = os.path.join(sys.argv[1], "psx", "beta2", "tim")
o, n = rows(open(os.path.join(t, "ST_MOJI.TIM"), "rb").read()), rows(open(os.path.join(t, "ST_MOJI_EN.TIM"), "rb").read())
orig = Disc(sys.argv[3]); st0 = bytearray(orig.read(*orig.find("COMMON\\DATA\\ST0.TIM")))   # clean original
copy = Disc(sys.argv[2]); lba, size = copy.find("COMMON\\DATA\\ST0.TIM")
base60 = 75904
for y in range(56, 72):
    a = base60 + (y - 60) * 128
    for i in range(96):
        if o[y][i] != n[y][i]:            # only bytes we changed; keeps ST0's own odd byte in row 68
            st0[a + i] = n[y][i]
with open(sys.argv[2], "r+b") as f:
    disc_write(f, copy, lba, 0, bytes(st0))
check = Disc(sys.argv[2]).read(lba, size)
print("ST0 restored from original + 4 command cells patched:", check == bytes(st0))
