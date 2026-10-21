"""Dump RE2 RID camera records (32 bytes each) from every RDT in a folder and summarise fields.

Record: u16 unknown0, u16 const0, i32 from[3], i32 to[3], u32 masks_offset.
Count = header byte 1 (nCut).
Usage: python -I re2cams.py <rdt_dir>
"""
import math
import os
import struct
import sys
from collections import Counter


def main():
    src = sys.argv[1]
    u0, c0 = Counter(), Counter()
    samples = []
    for name in sorted(os.listdir(src)):
        if not name.upper().endswith(".RDT"):
            continue
        data = open(os.path.join(src, name), "rb").read()
        ncut = data[1]
        rid = struct.unpack_from("<i", data, 8 + 7 * 4)[0]
        for i in range(ncut):
            rec = struct.unpack_from("<HH6iI", data, rid + i * 32)
            u0[rec[0]] += 1
            c0[rec[1]] += 1
            fx, fy, fz, tx, ty, tz = rec[2:8]
            dist = math.dist((fx, fy, fz), (tx, ty, tz))
            samples.append((name[4:8], i, rec[0], rec[1], dist))
    print("unknown0 values:", u0.most_common(10))
    print("const0 values:", c0.most_common(20))
    print("total cameras:", len(samples))
    for s in samples[:12]:
        print("room %s cam %d  u0=%d c0=%d  from->to dist=%.0f" % s)


if __name__ == "__main__":
    main()
