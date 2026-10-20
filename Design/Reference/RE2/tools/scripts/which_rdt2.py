import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "re2tool"))
from gdbmem import GdbMem
g = GdbMem(); ram = g.read(0x80000000, 0x200000); g.close()
rdt = open(os.path.join(sys.argv[1], "psx", "beta2", "rdt", "ROOM1030.RDT"), "rb").read()
offs = struct.unpack("<23i", rdt[8:8 + 92])
p = ram.find(rdt[:8])
while p != -1:
    vals = struct.unpack_from("<23I", ram, p + 8)
    base = 0x80000000 + p
    rel = [(v - base) if v else 0 for v in vals]
    same = sum(1 for a, b in zip(rel, offs) if a == b)
    if same >= 15:
        print(f"RDT loaded at 0x{base:X}: {same}/23 offsets match as absolute pointers")
        print(f"  slot13 -> +0x{rel[13]:X}   slot14 -> +0x{rel[14]:X}   (file: 13=0x{offs[13]:X} JP, 14=0x{offs[14]:X} EN)")
        break
    p = ram.find(rdt[:8], p + 1)
else:
    print("RDT header not located")
# Also: is the English message text itself in RAM?
en = bytes.fromhex("252550 3a4f00504b4b00".replace(" ", ""))
q = ram.find(bytes.fromhex("25503a4f00504b4b0040"))
print("english text bytes in RAM at:", hex(0x80000000 + q) if q != -1 else "not found")
