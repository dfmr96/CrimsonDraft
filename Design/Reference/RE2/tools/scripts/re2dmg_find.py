"""Heuristic search for a per-weapon damage table (indexed by item id 0..19) in the exe data.

Constraints: handgun (2) small-ish, magnum (5) >= 2.5x handgun, custom magnum (6) >= magnum,
rocket (17) >= magnum, knife (1) <= handgun, shotgun (7) > handgun.
Usage: python -I re2dmg_find.py <exe>
"""
import struct
import sys

DATA_RAW, DATA_RVA, IMAGE_BASE = 0x120400, 0x122000, 0x400000
DATA_SIZE = 0x2B600


def check(v):
    hg, knife, mag, cmag, sg, rocket = v[2], v[1], v[5], v[6], v[7], v[17]
    return (3 <= hg <= 60 and knife <= hg and mag >= 2.5 * hg and cmag >= mag
            and sg > hg and rocket >= mag and v[4] >= hg and v[3] > 0)


def main():
    data = open(sys.argv[1], "rb").read()
    region = data[DATA_RAW:DATA_RAW + DATA_SIZE]
    for size, fmt in ((2, "<H"), (1, "<B")):
        for stride in range(size, 33, size):
            for start in range(0, len(region) - stride * 20):
                v = [struct.unpack_from(fmt, region, start + i * stride)[0] for i in range(20)]
                if check(v):
                    va = IMAGE_BASE + DATA_RVA + start
                    print(f"u{size*8} stride={stride:2d} VA=0x{va:X} values={v}")


if __name__ == "__main__":
    main()
