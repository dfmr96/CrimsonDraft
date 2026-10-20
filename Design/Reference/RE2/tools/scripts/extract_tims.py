import sys, os
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
d = Disc(sys.argv[2]); out = os.path.join(sys.argv[1], "psx", "beta2", "tim"); os.makedirs(out, exist_ok=True)
for n in ("ST_MOJI.TIM", "ST0.TIM", "ST1.TIM", "TMOJI.TIM", "ST_FILE.TIM"):
    open(os.path.join(out, n), "wb").write(d.read(*d.find("COMMON\\DATA\\" + n)))
print("ok")
