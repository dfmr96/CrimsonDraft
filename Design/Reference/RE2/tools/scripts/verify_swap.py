import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "scripts")); sys.path.insert(0, os.path.join(sys.argv[1], "re2tool"))
from psx_extract import Disc
from re2data import Rdt, decode_msg
for label, path in (("original", sys.argv[2]), ("copy", sys.argv[3])):
    d = Disc(path); data = d.read(*d.find("PL0\\RDT\\ROOM1030.RDT")); r = Rdt(data)
    off = r.offsets[13]; first = struct.unpack_from("<H", data, off)[0]
    print(f"{label:8s} table 13 msg 0 ->", decode_msg(data[off + first: off + first + 60]) or "(japanese bytes)")
