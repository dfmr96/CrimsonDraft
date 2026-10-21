"""Dump RE2 per-enemy damage tables: ptr table at 0x543C48 indexed by enemy type,
each entry -> records of 20 bytes (5 dwords); dword0 packs three 10-bit damage values.

Usage: python -I re2dmg_dump.py <exe> <enemy_type_hex>[,<enemy_type_hex>...] [records]
"""
import struct
import sys

IMAGE_BASE = 0x400000
SECS = [(0x1000, 0x11C1E8, 0x600), (0x11E000, 0x3AD4, 0x11C800), (0x122000, 0x2B600, 0x120400)]


def off(va):
    rva = va - IMAGE_BASE
    for s_rva, size, raw in SECS:
        if s_rva <= rva < s_rva + size:
            return rva - s_rva + raw
    return None


def main():
    data = open(sys.argv[1], "rb").read()
    types = [int(t, 16) for t in sys.argv[2].split(",")]
    n = int(sys.argv[3]) if len(sys.argv) > 3 else 20
    for t in types:
        ptr = struct.unpack_from("<I", data, off(0x543C48 + t * 4))[0]
        print(f"== enemy type 0x{t:02X}: table VA 0x{ptr:X}")
        o = off(ptr)
        if o is None:
            print("   (pointer outside file data)")
            continue
        for i in range(n):
            dws = struct.unpack_from("<5I", data, o + i * 20)
            d0 = dws[0]
            vals = [(d0 >> (10 * k)) & 0x3FF for k in range(3)]
            print(f"   attack {i + 1:2d}: dmg={vals}  raw=" + " ".join(f"{d:08X}" for d in dws))


if __name__ == "__main__":
    main()
