import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
EN = " .___()__\u201c\u201d_0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz_________"
d = Disc(sys.argv[2]); exe = d.read(*d.find("PSX.EXE")); base = 0x80010000 - 0x800
offs = struct.unpack_from("<136H", exe, 0x800940F6 - base); blob = exe[0x80093C20 - base:0x80093C20 - base + 1233]
name = lambda i: "".join(EN[c] for c in blob[offs[i]:blob.index(b"\xF7", offs[i])])
print([f"{i}:{name(i)}" for i in (0, 1, 19, 29, 37, 40, 49, 70, 88, 103, 109, 120)])
