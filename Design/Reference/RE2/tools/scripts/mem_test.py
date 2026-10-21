import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "re2tool"))
from memviewer import load_symbols, ENTITY_FIELDS
from overlay import BetaSource
syms = load_symbols(); print("data symbols:", len(syms), "e.g.", syms[:3], [s for s in syms if s[0] in ("G", "Camera", "Cutinfo")])
src = BetaSource()
pl = struct.unpack("<I", src.read(0x800C2744, 4))[0]
data = src.read(pl, 0x200)
for name, off, size in ENTITY_FIELDS[:4] + [f for f in ENTITY_FIELDS if f[0] in ("M_X_POS", "VIDA", "VIDA MÁX.", "M_FLOOR")]:
    print(f"  {name:28s}", struct.unpack_from({1: '<B', 2: '<h', 4: '<i'}[size], data, off)[0])
