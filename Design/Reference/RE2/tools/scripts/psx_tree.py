import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
d = Disc(sys.argv[2])
def walk(lba, size, path=""):
    for name, l, s, isdir in d.dir_entries(lba, size):
        if isdir:
            yield from walk(l, s, path + name + "/")
        else:
            yield path + name, s
files = list(walk(*d.root()))
print("total files:", len(files))
for p, s in files:
    if not p.upper().startswith(("PL0/RDT", "PL0/EMD", "COMMON/BSS", "ZMOVIE")) :
        print(f"{s:10d} {p}")
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read()
t_addr, t_size = struct.unpack_from("<II", exe, 0x18)
print(f"PSX.EXE text: load 0x{t_addr:X} size 0x{t_size:X} -> ends 0x{t_addr + t_size:X}")
