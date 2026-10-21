"""Dump the SCA (collision) chunk of RE2 RDT files (offset table index 6).
Format (verified against Sca_ck_hit in the Beta 2 and matching PC data): 16-byte header
[s16 Cx, s16 Cz, u32 count (header included), 8 unknown bytes], then count-1 entries of 16 bytes:
[s16 x, s16 z, u16 w, u16 d, u16 type (low 4 bits = shape), u16 quadrants, u32 floor mask].
Usage: python -I re2sca.py <rdt> [more rdts]
"""
import struct, sys
SHAPES = ["box", "tri_a", "tri_b", "tri_c", "tri_d", "rhombus", "circle", "oval_x", "oval_z",
          "ledge_up", "ledge_down", "slope", "stairs", "curve", "?14", "?15"]
for path in sys.argv[1:]:
    d = open(path, "rb").read()
    s = struct.unpack_from("<23i", d, 8)[6]
    cx, cz, n = struct.unpack_from("<hhI", d, s)
    print(f"== {path.replace(chr(92), '/').split('/')[-1]}  SCA 0x{s:X}  center=({cx},{cz})  entries={n - 1}")
    for i in range(1, n):
        x, z, w, dd, t, q, fl = struct.unpack_from("<hhHHHHI", d, s + 16 * i)
        print(f"{i:3} {SHAPES[t & 15]:10} x={x:7} z={z:7} w={w:6} d={dd:6} type=0x{t:04X} quad=0x{q:04X} floors=0x{fl:X}")
