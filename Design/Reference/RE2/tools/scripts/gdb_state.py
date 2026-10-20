import sys, os, struct, time
sys.path.insert(0, os.path.join(sys.argv[1], "re2tool"))
from gdbmem import GdbMem
g = GdbMem()
for _ in range(3):
    stage, room, cut = struct.unpack("<3H", g.read(0x800C6F08, 6))
    x, y, z = struct.unpack("<3i", g.read(0x800C252C + 0x38, 12))
    print(f"stage={stage} room={room} (ROOM{stage + 1:X}{room:02X}0) cut={cut}  player x={x} y={y} z={z}")
    time.sleep(0.5)
g.close()
