import sys, os
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
d = Disc(sys.argv[2]); out = os.path.join(sys.argv[1], "psx", "beta2", "tim"); os.makedirs(out, exist_ok=True)
for n, l, size, isdir in d.dir_entries(*d.find("COMMON\\DATA")):
    if n.upper().endswith(".TIM") and size < 200000:
        open(os.path.join(out, n), "wb").write(d.read(l, size))
print(sorted(os.listdir(out)))
