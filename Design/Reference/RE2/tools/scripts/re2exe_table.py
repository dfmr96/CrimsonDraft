import sys, os
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from re2scd import ITEMS
data = open(sys.argv[2], "rb").read()
base = 0x1462A8
for i in range(100):
    rec = data[base + i*8: base + i*8 + 8]
    name = ITEMS[i] if i < len(ITEMS) else f"ITEM_{i}"
    print(f"{i:3d} {name:18s} " + " ".join(f"{b:02X}" for b in rec) + "   dec: " + " ".join(f"{b:3d}" for b in rec))
