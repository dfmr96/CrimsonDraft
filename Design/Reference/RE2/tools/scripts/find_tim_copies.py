import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
tim = open(os.path.join(sys.argv[1], "psx", "beta2", "tim", "ST_MOJI.TIM"), "rb").read()
p = 8 + struct.unpack_from("<I", tim, 8)[0]; w16 = struct.unpack_from("<H", tim, p + 8)[0]; pix = p + 12
row_bytes = w16 * 2
needle = tim[pix + 60 * row_bytes: pix + 60 * row_bytes + 48]      # part of the command-word row (y=60)
d = Disc(sys.argv[2])
def walk(lba, size, path=""):
    for n, l, s_, isdir in d.dir_entries(lba, size):
        if isdir: yield from walk(l, s_, path + n + "/")
        else: yield path + n, l, s_
hits = []
for path, l, size in walk(*d.root()):
    if size > 40_000_000: continue
    data = d.read(l, size)
    if needle in data: hits.append(path)
print("files containing the original command-word pixels:", hits)
