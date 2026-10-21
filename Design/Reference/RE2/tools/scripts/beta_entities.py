import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "re2tool"))
from gdbmem import GdbMem
g = GdbMem()
def ent(p):
    b = g.read(p, 0x200)
    return dict(ptr=hex(p), r0=b[4], r1=b[5], id=b[8], type=struct.unpack_from("<H", b, 0x10E)[0] & 0x3F,
                pos=struct.unpack_from("<3i", b, 0x38), ydir=struct.unpack_from("<h", b, 0x76)[0],
                floor=b[0x106], hp=struct.unpack_from("<h", b, 0x156)[0], hpmax=struct.unpack_from("<h", b, 0x162)[0],
                dist=struct.unpack_from("<I", b, 0x1F0)[0])
pl = struct.unpack("<I", g.read(0x800C2744, 4))[0]
print("player ptr", hex(pl), ent(pl))
stage, room, cut = struct.unpack("<3H", g.read(0x800C6F08, 6)); print(f"room ROOM{stage+1:X}{room:02X}0 cut {cut}")
ptrs = struct.unpack("<16I", g.read(0x800C274C, 64))
for i, p in enumerate(ptrs):
    if 0x80000000 <= p < 0x80200000:
        print(f"enemy[{i}]", ent(p))
print("raw enemy ptrs:", [hex(p) for p in ptrs])
g.close()
