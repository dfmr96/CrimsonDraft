import sys, os, struct
pc = open(sys.argv[2], "rb").read()
EN = " .___()__\u201c\u201d_0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz_________"
enc = lambda t: bytes(EN.index(c) for c in t)
p = pc.find(enc("Knife") + b"\xF7")
start = pc.rfind(b"\xF7", 0, p)          # separator before "Knife"
# Walk back over the PC blob to find its first name (blob starts right after its offset table).
first = start
while pc[first - 1] != 0xF7 and False: pass
names, pos = [], start
while pc[pos] == 0xF7 and len(names) < 200:
    nxt = pc.find(b"\xF7", pos + 1); seg = pc[pos + 1:nxt]
    if nxt - pos > 40: break
    names.append(seg); pos = nxt
dec = lambda b: "".join(EN[c] if c < len(EN) else f"<{c:02X}>" for c in b)
print("PC names from Knife:", len(names), "bytes:", sum(len(n) + 1 for n in names))
print([dec(n) for n in names[:6]], "...", [dec(n) for n in names[-4:]])
# Beta: first JP entries before the knife-equivalent?
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read(); base = 0x80010000 - 0x800
b = exe[0x80093C20 - base:0x80093C20 - base + 64]
print("beta first segments raw:", [seg.hex() for seg in b.split(b"\xF7")[1:5]])
