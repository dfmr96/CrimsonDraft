"""Patch a COPY of the Beta 2 ISO: in the given rooms, swap the RDT header pointers of message
table 13 (Japanese, the one the game shows) and 14 (unused English). File sizes do not change.

Usage: python -I beta_swap_msg.py <iso copy> ROOM1030 [ROOM....]
Refuses to run on a file whose name does not contain "(EN" so the original is never touched.
"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from psx_extract import Disc  # noqa: E402


def main():
    iso = sys.argv[1]
    if "(EN" not in os.path.basename(iso):
        sys.exit("Por seguridad solo se parchean copias con '(EN' en el nombre.")
    disc = Disc(iso)
    with open(iso, "r+b") as f:
        for room in sys.argv[2:]:
            loc = disc.find(f"PL0\\RDT\\{room}.RDT")
            if not loc:
                print(f"{room}: not found")
                continue
            lba, _ = loc
            # The RDT header lives in the file's first sector, so one sector's user data covers it.
            base = lba * disc.secsize + disc.data_off
            f.seek(base + 8 + 13 * 4)
            t13, t14 = struct.unpack("<ii", f.read(8))
            if not t14:
                print(f"{room}: no English table, skipped")
                continue
            f.seek(base + 8 + 13 * 4)
            f.write(struct.pack("<ii", t14, t13))
            print(f"{room}: table13 0x{t13:X} <-> table14 0x{t14:X}  (file offset 0x{base + 0x3C:X})")


if __name__ == "__main__":
    main()
