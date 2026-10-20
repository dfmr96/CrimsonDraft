"""Read-only snapshot of PS1 RAM (2 MB) from DuckStation's GDB server.
Usage: python -I ramsnap.py <out.bin>
"""
import os, sys, time
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "re2tool"))
from gdbmem import GdbMem
g = GdbMem()
t = time.time(); out = bytearray()
for a in range(0x80000000, 0x80200000, 0x800):
    out += g.read(a, 0x800)
open(sys.argv[1], "wb").write(out)
print(len(out), "bytes in", round(time.time() - t, 1), "s")
