"""Replace the Beta 2 Japanese item-name table (PSX.EXE) with Capcom's official English names
from the PC executable, fitting the original 1232-byte blob by sharing duplicate strings and
suffixes (the game indexes names through a u16 offset table) plus a few shortened file titles.

Usage: python -I beta_item_names.py <LeonU.exe> <beta PSX.EXE> [<iso copy with (EN> --apply]
"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

EN = " .___()__“”_0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz_________"
enc = lambda t: bytes(EN.index(c) for c in t)
dec = lambda b: "".join(EN[c] if c < len(EN) else "?" for c in b)

BLOB_VA, BLOB_SIZE, TABLE_VA, COUNT = 0x80093C20, 1232, 0x800940F6, 136
EXE_BASE = 0x80010000 - 0x800
SHORT = {  # file titles shortened to fit; meaning kept
    "Police memorandum": "Police memo", "Operation report 1": "Op. report 1", "Operation report 2": "Op. report 2",
    "Mail to the chief": "Mail to chief", "Secretary's diary A": "Sec. diary A", "Secretary's diary B": "Sec. diary B",
    "User registration": "User reg.", "Sewer manager diary": "Sewer diary", "Sewer manager fax": "Sewer fax",
    "Lab security manual": "Lab manual", "Vaccine synthesis": "Vaccine notes", "P-epsilon report": "P-Eps. report",
    "Watchman's diary": "Watchman log", "Patrol report": "Patrol log", "Memo to LEON": "Leon memo",
}


def pc_names(pc):
    start = pc.rfind(b"\xF7", 0, pc.find(enc("Knife") + b"\xF7"))
    out, pos = [], start
    while pc[pos] == 0xF7 and len(out) < COUNT:
        nxt = pc.find(b"\xF7", pos + 1)
        out.append(pc[pos + 1:nxt])
        pos = nxt
    return out


def build(names):
    uniq = sorted(set(names), key=len, reverse=True)
    kept = [u for u in uniq if not any(o != u and o.endswith(u) for o in uniq)]
    blob, where = bytearray(b"\xF7"), {}
    for k in kept:
        where[k] = len(blob)
        blob += k + b"\xF7"
    offsets = []
    for n in names:
        host = next(k for k in kept if k.endswith(n))
        offsets.append(where[host] + len(host) - len(n))
    return bytes(blob), offsets


def main():
    pc = open(sys.argv[1], "rb").read()
    beta = open(sys.argv[2], "rb").read()
    global BLOB_SIZE
    jp_offs = struct.unpack_from(f"<{COUNT}H", beta, TABLE_VA - EXE_BASE)
    region = beta[BLOB_VA - EXE_BASE:TABLE_VA - EXE_BASE]
    BLOB_SIZE = region.index(b"\xF7", jp_offs[-1]) + 1   # up to the separator closing the last name
    print(f"original blob: {BLOB_SIZE} bytes (table starts {TABLE_VA - BLOB_VA} bytes after blob start)")
    jp_blob = region[:BLOB_SIZE]
    jp = [jp_blob[o:jp_blob.index(b"\xF7", o)] for o in jp_offs]
    en = pc_names(pc)
    # Alignment check: equal names must sit at the same indices in both lists.
    def groups(lst):
        g = {}
        for i, n in enumerate(lst):
            g.setdefault(n, []).append(i)
        return sorted(tuple(v) for v in g.values() if len(v) > 1)
    print("JP duplicate groups:", groups(jp))
    print("EN duplicate groups:", groups(en))
    names = [enc(SHORT.get(dec(n), dec(n))) for n in en]
    blob, offsets = build(names)
    print(f"new blob {len(blob)} / {BLOB_SIZE} bytes")
    if len(blob) > BLOB_SIZE:
        sys.exit("does not fit")
    blob += b"\xF7" * (BLOB_SIZE - len(blob))
    if "--apply" in sys.argv:
        from psx_extract import Disc
        from beta_english import disc_write
        iso = sys.argv[3]
        if "(EN" not in os.path.basename(iso):
            sys.exit("Por seguridad solo se parchean copias con '(EN' en el nombre.")
        disc = Disc(iso)
        lba, _ = disc.find("PSX.EXE")
        with open(iso, "r+b") as f:
            disc_write(f, disc, lba, BLOB_VA - EXE_BASE, blob)
            disc_write(f, disc, lba, TABLE_VA - EXE_BASE, struct.pack(f"<{COUNT}H", *offsets))
        print("patched PSX.EXE item names in", os.path.basename(iso))


if __name__ == "__main__":
    main()
