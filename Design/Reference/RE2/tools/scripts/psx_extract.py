"""Minimal read-only ISO9660 reader for PS1 disc images (raw 2352-byte sectors or 2048 ISO).
Lists the root/subdirs and extracts the boot executable named in SYSTEM.CNF.

Usage: python -I psx_extract.py <image> <out_dir>
"""
import os
import re
import struct
import sys


class Disc:
    def __init__(self, path):
        self.f = open(path, "rb")
        size = os.path.getsize(path)
        self.raw = size % 2352 == 0 and not path.lower().endswith(".iso")
        if not self.raw and size % 2048 != 0 and size % 2352 == 0:
            self.raw = True
        self.secsize = 2352 if self.raw else 2048
        # Mode 1 data starts at byte 16, Mode 2 (form 1) at byte 24.
        self.data_off = 0
        if self.raw:
            self.f.seek(16 * 2352 + 15)
            mode = self.f.read(1)[0]
            self.data_off = 16 if mode == 1 else 24

    def sector(self, lba):
        self.f.seek(lba * self.secsize + self.data_off)
        return self.f.read(2048)

    def read(self, lba, length):
        out = bytearray()
        while len(out) < length:
            out += self.sector(lba)
            lba += 1
        return bytes(out[:length])

    def dir_entries(self, lba, length):
        data = self.read(lba, length)
        pos = 0
        while pos < len(data):
            n = data[pos]
            if n == 0:
                pos = (pos // 2048 + 1) * 2048
                continue
            ext_lba = struct.unpack_from("<I", data, pos + 2)[0]
            size = struct.unpack_from("<I", data, pos + 10)[0]
            flags = data[pos + 25]
            nlen = data[pos + 32]
            name = data[pos + 33:pos + 33 + nlen].decode("ascii", "replace")
            if name not in ("\x00", "\x01"):
                yield name.split(";")[0], ext_lba, size, bool(flags & 2)
            pos += n

    def root(self):
        pvd = self.sector(16)
        assert pvd[1:6] == b"CD001", "not an ISO9660 image"
        rec = pvd[156:156 + 34]
        return struct.unpack_from("<I", rec, 2)[0], struct.unpack_from("<I", rec, 10)[0]

    def find(self, path):
        lba, size = self.root()
        for part in path.upper().strip("\\/").split("\\"):
            for name, l, s, is_dir in self.dir_entries(lba, size):
                if name.upper() == part:
                    lba, size = l, s
                    break
            else:
                return None
        return lba, size


def main():
    disc = Disc(sys.argv[1])
    out = sys.argv[2]
    os.makedirs(out, exist_ok=True)
    lba, size = disc.root()
    print("root:", [n + ("/" if d else "") for n, _, _, d in disc.dir_entries(lba, size)])
    cnf = disc.find("SYSTEM.CNF")
    boot = "PSX.EXE"
    if cnf:
        text = disc.read(*cnf).decode("ascii", "replace")
        print("SYSTEM.CNF:", text.strip().replace("\r\n", " | "))
        m = re.search(r"BOOT\s*=\s*cdrom:\\?([^;\s]+)", text, re.I)
        if m:
            boot = m.group(1)
    loc = disc.find(boot)
    if not loc:
        print("boot exe not found:", boot)
        return
    data = disc.read(*loc)
    dst = os.path.join(out, os.path.basename(boot.replace("\\", "/")))
    open(dst, "wb").write(data)
    print(f"extracted {boot} ({len(data)} bytes, header {data[:8]!r}) -> {dst}")


if __name__ == "__main__":
    main()
