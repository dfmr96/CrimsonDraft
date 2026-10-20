import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "re2tool"))
from gdbmem import GdbMem
g = GdbMem(); ram = g.read(0x80000000, 0x200000); g.close()
base = 0x8011E558 - 0x3EB4
hdr = ram[base - 0x80000000: base - 0x80000000 + 100]
print("RAM header bytes 0..8:", hdr[:8].hex(" "))
vals = struct.unpack_from("<23I", hdr, 8)
print("slot13 =", hex(vals[13]), " slot14 =", hex(vals[14]))
for label, target in (("JP table", base + 0x3E94), ("EN table", base + 0x3EB0)):
    hits = []
    p = ram.find(struct.pack("<I", target))
    while p != -1:
        hits.append(hex(0x80000000 + p)); p = ram.find(struct.pack("<I", target), p + 1)
    print(f"{label} @0x{target:X} referenced from: {hits}")
