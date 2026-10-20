"""For every door_aot_se, check which camera of the destination room covers the arrival point,
and compare against the door's byte fields to find the 'next camera' field.

Usage: python -I re2doorcam.py <disasm_dir> <rdt_dir>
"""
import os
import re
import struct
import sys
from collections import Counter

DOOR = re.compile(r"door_aot_se\((.*)\)")


def cam_areas(path):
    data = open(path, "rb").read()
    off = struct.unpack_from("<i", data, 8 + 8 * 4)[0]
    areas, seen, i = {}, set(), 0
    while True:
        rec = data[off + i * 20: off + i * 20 + 20]
        if len(rec) < 20 or struct.unpack_from("<I", rec)[0] == 0xFFFFFFFF:
            break
        frm = rec[2]
        if frm not in seen:  # first record of each group = camera's own area
            seen.add(frm)
            p = struct.unpack_from("<8h", rec, 4)
            areas[frm] = [(p[k], p[k + 1]) for k in range(0, 8, 2)]
        i += 1
    return areas


def inside(pt, quad):
    x, z = pt
    sign = None
    for k in range(4):
        (x1, z1), (x2, z2) = quad[k], quad[(k + 1) % 4]
        c = (x2 - x1) * (z - z1) - (z2 - z1) * (x - x1)
        if c != 0:
            s = c > 0
            if sign is None:
                sign = s
            elif s != sign:
                return False
    return True


def main():
    dis, rdt = sys.argv[1], sys.argv[2]
    stats = Counter()
    examples = []
    for name in sorted(os.listdir(dis)):
        if not name.startswith("ROOM"):
            continue
        for line in open(os.path.join(dis, name), encoding="utf-8"):
            m = DOOR.search(line)
            if not m:
                continue
            a = [x.strip() for x in m.group(1).split(",")]
            nx, ny, nz = int(a[9]), int(a[10]), int(a[11])
            b = [int(v) for v in a[13:21]]  # the 8 u-bytes after next_dir
            stage, room = b[0], b[1]
            for st in (stage + 1,):
                dest = os.path.join(rdt, f"ROOM{st:X}{room:02X}0.RDT")
                if os.path.exists(dest):
                    break
            else:
                stats["dest_missing"] += 1
                continue
            areas = cam_areas(dest)
            covering = sorted(c for c, q in areas.items() if inside((nx, nz), q))
            stats["doors"] += 1
            if b[2] in covering:
                stats["field2_matches_covering_cam"] += 1
            elif covering:
                stats["field2_not_covering"] += 1
                if len(examples) < 8:
                    examples.append((name, os.path.basename(dest), b, covering))
            else:
                stats["no_covering_area"] += 1
    print(dict(stats))
    for e in examples:
        print("mismatch:", e)


if __name__ == "__main__":
    main()
