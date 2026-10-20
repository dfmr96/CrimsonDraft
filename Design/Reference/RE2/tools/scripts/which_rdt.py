import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "re2tool"))
from gdbmem import GdbMem
g = GdbMem()
stage, room, cut = struct.unpack("<3H", g.read(0x800C6F08, 6)); print(f"loaded room: ROOM{stage+1:X}{room:02X}0")
ram = g.read(0x80000000, 0x200000); g.close()
rdt = open(os.path.join(sys.argv[1], "psx", "beta2", "rdt", "ROOM1030.RDT"), "rb").read()
orig_tbl = rdt[8:8 + 23 * 4]
t = list(struct.unpack("<23i", orig_tbl)); t[13], t[14] = t[14], t[13]
patched_tbl = struct.pack("<23i", *t)
for label, pat in (("ORIGINAL (japanese)", orig_tbl), ("PATCHED (english)", patched_tbl)):
    p = ram.find(pat)
    print(f"{label:22s} header in RAM: {hex(0x80000000 + p) if p != -1 else 'not found'}")
