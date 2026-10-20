import sys, os
sys.path.insert(0, os.path.join(sys.argv[1], "re2tool"))
from gdbmem import GdbMem
g = GdbMem()
live = g.read(0x80010000, 64)
disk = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read()[0x800:0x800 + 64]
print("code at 0x80010000 matches Beta 2 PSX.EXE:", live == disk)
print("live :", live[:16].hex()); print("disk :", disk[:16].hex())
print("Game state byte 0x800D1878 =", g.read(0x800D1878, 1)[0])
g.close()
