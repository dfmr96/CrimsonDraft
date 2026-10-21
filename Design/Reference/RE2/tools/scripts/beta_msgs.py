import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "re2tool"))
from re2data import Rdt, decode_msg
d = os.path.join(sys.argv[1], "psx", "beta2", "rdt")
stats = {"rooms": 0, "t13": 0, "t14": 0}
for name in sorted(os.listdir(d)):
    data = open(os.path.join(d, name), "rb").read(); r = Rdt(data); stats["rooms"] += 1
    if r.offsets[13]: stats["t13"] += 1
    if r.offsets[14]: stats["t14"] += 1
print(stats)
r = Rdt(open(os.path.join(d, "ROOM1030.RDT"), "rb").read())
for idx in (13, 14):
    off = r.offsets[idx]
    if not off: print(f"table {idx}: none"); continue
    first = struct.unpack_from("<H", data := r.data, off)[0]
    raw = data[off + first: off + first + 40]
    print(f"table {idx}: count {first // 2}, first msg raw: {raw.hex(' ')}")
    print("   decoded with EN table:", decode_msg(raw))
