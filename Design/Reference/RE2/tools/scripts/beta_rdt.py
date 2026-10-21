import sys, os
sys.path.insert(0, os.path.join(sys.argv[1], "scripts")); sys.path.insert(0, os.path.join(sys.argv[1], "re2tool"))
from psx_extract import Disc
from re2data import parse_room
d = Disc(sys.argv[2])
def walk(lba, size, path=""):
    for name, l, s, isdir in d.dir_entries(lba, size):
        if isdir: yield from walk(l, s, path + name + "/")
        else: yield path + name, l, s
rdts = [f for f in walk(*d.root()) if f[0].upper().endswith(".RDT")]
print(len(rdts), "RDT files, e.g.", [r[0] for r in rdts[:4]])
out = os.path.join(sys.argv[1], "psx", "beta2", "rdt"); os.makedirs(out, exist_ok=True)
for p, l, s in rdts:
    open(os.path.join(out, os.path.basename(p)), "wb").write(d.read(l, s))
target = [r for r in rdts if "11A0" in r[0].upper()]
if target:
    room = parse_room(open(os.path.join(out, os.path.basename(target[0][0])), "rb").read())
    print("ROOM11A0:", len(room["cameras"]), "cameras,", len(room["zones"]), "zones,", len(room["aots"]), "aots")
    for c in room["cameras"]: print("  cam", c["id"], "fov", c["fov_v"], "from", c["from"], "to", c["to"])
    print("  sample zones:", [(z["from"], z["to"], z["area"]) for z in room["zones"][:8]])
