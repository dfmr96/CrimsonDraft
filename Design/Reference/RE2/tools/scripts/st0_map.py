import sys, os
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
from beta_menu_copies import rows
t = os.path.join(sys.argv[1], "psx", "beta2", "tim")
o_rows = rows(open(os.path.join(t, "ST_MOJI.TIM"), "rb").read())
st0 = Disc(sys.argv[2]).read(*Disc(sys.argv[2]).find("COMMON\\DATA\\ST0.TIM"))
for y in range(54, 73):
    for c in range(5):
        seg = o_rows[y][c * 24:(c + 1) * 24]
        if len(set(seg)) < 3: continue
        p = st0.find(seg)
        hits = []
        while p != -1 and len(hits) < 3:
            hits.append(p); p = st0.find(seg, p + 1)
        print(f"row {y} cell {c}: {hits}")
