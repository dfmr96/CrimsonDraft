import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "scripts")); sys.path.insert(0, os.path.join(sys.argv[1], "re2tool"))
from psx_extract import Disc
orig, copy = Disc(sys.argv[2]), Disc(sys.argv[3])
def tbl13(d, room, k):
    data = d.read(*d.find(f"PL0\\RDT\\{room}.RDT")); t = struct.unpack_from("<23i", data, 8); off = t[13]
    n = struct.unpack_from("<H", data, off)[0] // 2; o = struct.unpack_from(f"<{n}H", data, off)
    raw = data[off + o[k]: off + (o[k + 1] if k + 1 < n else o[k] + 80)]
    EN = " .___()__\u201c\u201d_0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz_________"
    s, i = "", 0
    while i < len(raw) and raw[i] != 0xFE:
        c = raw[i]
        if c == 0xFC: s += " | "
        elif c in (0xF9, 0xFA, 0xFB, 0xFD): i += 1
        elif c < len(EN): s += EN[c]
        else: s += "?"
        i += 1
    return s.strip()
print("copy ROOM1010 msg14:", tbl13(copy, "ROOM1010", 14))
print("copy ROOM21B0 msg0 :", tbl13(copy, "ROOM21B0", 0))
same = all(orig.read(*orig.find(f"PL0\\RDT\\{r}.RDT")) == copy.read(*copy.find(f"PL0\\RDT\\{r}.RDT")) for r in ("ROOM1050", "ROOM2000", "ROOM1000"))
print("unpatched rooms identical to original:", same)
