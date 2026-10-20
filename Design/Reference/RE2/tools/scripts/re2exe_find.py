"""Search an RE2 executable for the per-item attribute table by stride.

Known magazine sizes for item ids 2..11 (HandgunLeon .. GL Acid) are used as a signature.
Usage: python -I re2exe_find.py <exe>
"""
import sys

# item id -> expected byte. Magnum/C.Magnum 8, Shotgun 5, C.Shotgun 7, Sparkshot/SMG/Flamethrower 100.
SIGNATURE = {5: 8, 6: 8, 7: 5, 8: 7, 14: 100, 15: 100, 16: 100}


def main():
    data = open(sys.argv[1], "rb").read()
    hits = []
    first_id = min(SIGNATURE)
    for stride in range(1, 33):
        span = stride * (max(SIGNATURE) - first_id)
        for pos in range(0, len(data) - span):
            if data[pos] != SIGNATURE[first_id]:
                continue
            if all(data[pos + (i - first_id) * stride] == v for i, v in SIGNATURE.items()):
                table_start = pos - first_id * stride
                hits.append((stride, pos, table_start))
    for stride, pos, start in hits:
        print(f"stride={stride:2d} field_at=0x{pos:X} table_start(id0)=0x{start:X}")
    if not hits:
        print("no match")


if __name__ == "__main__":
    main()
