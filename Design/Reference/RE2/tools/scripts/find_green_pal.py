import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
want_g = {20, 17, 15, 12, 10, 5}
def is_green(c): return (c & 31) <= 1 and ((c >> 5) & 31) >= 4 and ((c >> 10) & 31) <= 5
def scan(name, data, base=0):
    for off in range(0, len(data) - 32, 2):
        pal = struct.unpack_from("<16H", data, off)
        greens = [p for p in pal if is_green(p)]
        gs = {(p >> 5) & 31 for p in greens}
        if len(greens) >= 5 and len(gs & want_g) >= 4:
            print(f"{name} @ 0x{base + off:X}: G values {sorted(gs, reverse=True)}  pal={[hex(p) for p in pal]}")
            return True
    return False
d = Disc(sys.argv[2])
scan("PSX.EXE", d.read(*d.find("PSX.EXE"))[0x800:], 0x80010000)
def walk(lba, size, path=""):
    for n, l, s_, isdir in d.dir_entries(lba, size):
        if isdir: yield from walk(l, s_, path + n + "/")
        else: yield path + n, l, s_
for path, l, size in walk(*d.root()):
    if size < 2_000_000 and not path.startswith(("PL0/RDT", "ZMOVIE", "PL0/VOICE")):
        scan(path, d.read(l, size))
print("scan done")
