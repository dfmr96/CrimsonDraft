import sys, os
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
d = Disc(sys.argv[2]); out = os.path.join(sys.argv[1], "psx", "beta2")
for name in ("MAIN.SYM", "MAIN.CPE", "FILECODE.H", "FILE_LOC.H"):
    loc = d.find(name)
    data = d.read(*loc)
    open(os.path.join(out, name), "wb").write(data)
    print(name, len(data), data[:16])
