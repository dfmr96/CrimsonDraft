import sys, os
EN = " .___()__\u201c\u201d_0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz_________"
enc = lambda s: bytes(EN.index(c) for c in s)
words = ["Ink Ribbon", "Green Herb", "Valve Handle", "Small Key", "Will you take", "First Aid", "Lighter", "Shotgun", "Hand Gun", "Combine", "Check"]
b2 = os.path.join(sys.argv[1], "psx", "beta2")
files = {"PSX.EXE": open(os.path.join(b2, "PSX.EXE"), "rb").read()}
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
d = Disc(sys.argv[2])
for n, l, s_, isdir in d.dir_entries(*d.find("COMMON\\BIN")):
    files["COMMON/BIN/" + n] = d.read(l, s_)
for n in ("ST_FILE.TS", "INIT_TBL.DAT", "ST_FILE.TM2"):
    loc = d.find("COMMON\\DATA\\" + n)
    if loc: files["COMMON/DATA/" + n] = d.read(*loc)
for fname, data in files.items():
    hits = [w for w in words if enc(w) in data]
    if hits: print(f"{fname:28s} EN text found: {hits}")
print("done; files scanned:", list(files))
