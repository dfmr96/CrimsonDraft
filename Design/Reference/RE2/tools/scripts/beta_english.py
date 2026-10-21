"""Patch a COPY of the Beta 2 ISO so rooms that ship an (unused) English message table show it:
  1. swap RDT header pointers of message table 13 (JP, displayed) and 14 (EN),
  2. re-wrap every English message to MAX_COLS visible chars per line for the wide Japanese font,
     only by turning separator bytes into space (0x00) or line break (0xFC): sizes never change.
Works on raw 2352-byte-sector images (user data split across sectors is handled).

Usage: python -I beta_english.py "<iso copy with (EN in its name>" [max_cols]
"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from psx_extract import Disc  # noqa: E402

SPACE, BREAK, PAGE, END = 0x00, 0xFC, 0xFD, 0xFE
TWO_BYTE = {0xF9, 0xFA, 0xFB, PAGE}  # control code + 1 parameter byte


def rewrap(msg, max_cols):
    """Return msg with separators re-chosen so no line exceeds max_cols visible chars."""
    out = bytearray(msg)
    i, col, last_sep, line_start = 0, 0, None, 0
    while i < len(out):
        b = out[i]
        if b == END:
            break
        if b in TWO_BYTE:
            if b == PAGE:
                col, last_sep = 0, None
            i += 2
            continue
        if b in (SPACE, BREAK):
            out[i] = SPACE
            if col == 0:              # separator at start of a line: keep as-is, invisible after a break
                last_sep = None
            else:
                last_sep = i
            col += 1
            i += 1
            continue
        col += 1                      # visible character
        if col > max_cols and last_sep is not None:
            out[last_sep] = BREAK
            col = sum(1 for k in range(last_sep + 1, i + 1) if out[k] < 0xF0)
            last_sep = None
        i += 1
    return bytes(out)


def is_english(msg):
    """True if every byte is an English-charset glyph (< 0x60) or a known control code."""
    i = 0
    while i < len(msg):
        b = msg[i]
        if b == END:
            return True
        if b in TWO_BYTE:
            i += 2
            continue
        if b != BREAK and b >= 0x60:
            return False
        i += 1
    return True


def disc_write(f, disc, lba, file_off, data):
    """Write bytes at a file-relative offset of a file starting at lba (user data only)."""
    while data:
        sec, inner = divmod(file_off, 2048)
        n = min(len(data), 2048 - inner)
        f.seek((lba + sec) * disc.secsize + disc.data_off + inner)
        f.write(data[:n])
        data, file_off = data[n:], file_off + n


def main():
    iso = sys.argv[1]
    max_cols = int(sys.argv[2]) if len(sys.argv) > 2 else 18
    if "(EN" not in os.path.basename(iso):
        sys.exit("Por seguridad solo se parchean copias con '(EN' en el nombre.")
    disc = Disc(iso)
    lba_root, size_root = disc.root()
    rdt_dir = disc.find("PL0\\RDT")
    rooms = [(n, l, s) for n, l, s, d in disc.dir_entries(*rdt_dir) if n.upper().endswith(".RDT")]
    done = 0
    with open(iso, "r+b") as f:
        for name, lba, size in rooms:
            data = disc.read(lba, size)
            t = list(struct.unpack_from("<23i", data, 8))
            t13, t14 = t[13], t[14]
            if not t13 or not t14:
                continue
            en_off = t14 if t13 < t14 else t13  # EN table follows JP in the file; slot depends on swap
            ends = sorted(o for o in t if o > en_off) + [size]
            en_end = ends[0]
            table = bytearray(data[en_off:en_end])
            n = struct.unpack_from("<H", table, 0)[0] // 2
            offs = list(struct.unpack_from(f"<{n}H", table, 0)) + [len(table)]
            if not all(is_english(bytes(table[offs[k]:offs[k + 1]])) for k in range(n)):
                print(f"{name}: table 14 is not (fully) English, left in Japanese")
                continue
            for k in range(n):
                a, b = offs[k], offs[k + 1]
                table[a:b] = rewrap(bytes(table[a:b]), max_cols)
            disc_write(f, disc, lba, en_off, bytes(table))
            if t13 < t14:  # not swapped yet
                disc_write(f, disc, lba, 8 + 13 * 4, struct.pack("<ii", t14, t13))
            done += 1
            print(f"{name}: {n} English messages re-wrapped to {max_cols} cols, table 13 -> English")
    print(f"{done} rooms patched")


if __name__ == "__main__":
    main()
