"""ST0.TIM and ST1.TIM embed the same command-word pixels as ST_MOJI.TIM (they are uploaded to
overlapping VRAM later, overwriting it). Replace each modified pixel row there too.

Usage: python -I beta_menu_copies.py <ST_MOJI orig> <ST_MOJI_EN> <iso copy (EN>
"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from psx_extract import Disc  # noqa: E402
from beta_english import disc_write  # noqa: E402


def rows(tim):
    p = 8 + struct.unpack_from("<I", tim, 8)[0]
    w16, h = struct.unpack_from("<HH", tim, p + 8)
    rb = w16 * 2
    return [tim[p + 12 + y * rb: p + 12 + (y + 1) * rb] for y in range(h)]


def main():
    orig, new, iso = sys.argv[1], sys.argv[2], sys.argv[3]
    if "(EN" not in os.path.basename(iso):
        sys.exit("Por seguridad solo se parchean copias con '(EN' en el nombre.")
    o_rows, n_rows = rows(open(orig, "rb").read()), rows(open(new, "rb").read())
    changed = [(y, o, n) for y, (o, n) in enumerate(zip(o_rows, n_rows)) if o != n]
    disc = Disc(iso)
    with open(iso, "r+b") as f:
        for name in ("ST0.TIM", "ST1.TIM"):
            lba, size = disc.find(f"COMMON\\DATA\\{name}")
            data = bytearray(disc.read(lba, size))
            done = 0
            for y, o, n in changed:
                pos = data.find(o)
                if pos == -1:
                    continue
                if data.find(o, pos + 1) != -1:
                    print(f"{name}: row {y} ambiguous, skipped")
                    continue
                data[pos:pos + len(n)] = n
                done += 1
            disc_write(f, disc, lba, 0, bytes(data))
            print(f"{name}: {done}/{len(changed)} rows replaced")


if __name__ == "__main__":
    main()
