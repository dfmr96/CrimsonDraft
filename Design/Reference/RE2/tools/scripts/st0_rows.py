import sys, os
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
from beta_menu_copies import rows
t = os.path.join(sys.argv[1], "psx", "beta2", "tim")
o, n = rows(open(os.path.join(t, "ST_MOJI.TIM"), "rb").read()), rows(open(os.path.join(t, "ST_MOJI_EN.TIM"), "rb").read())
st0 = Disc(sys.argv[2]).read(*Disc(sys.argv[2]).find("COMMON\\DATA\\ST0.TIM"))
base60 = 75904
for y in range(56, 72):
    a = base60 + (y - 60) * 128
    same = st0[a:a + 96] == o[y][:96]
    diff = [i for i in range(96) if st0[a + i] != o[y][i]]
    print(f"row {y}: match={same} changed_by_us={o[y][:96] != n[y][:96]} diff_bytes={diff[:8]}{'...' if len(diff) > 8 else ''}")
