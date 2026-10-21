import sys, os, struct
pc = open(sys.argv[2], "rb").read()
EN = " .___()__\u201c\u201d_0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz_________"
dec = lambda b: "".join(EN[c] if c < len(EN) else "?" for c in b)
enc = lambda t: bytes(EN.index(c) for c in t)
start = pc.rfind(b"\xF7", 0, pc.find(enc("Knife") + b"\xF7"))
names, pos = [], start
while pc[pos] == 0xF7 and len(names) < 136:
    nxt = pc.find(b"\xF7", pos + 1); names.append(pc[pos + 1:nxt]); pos = nxt
items, docs = names[:104], names[104:]
print("EN items 0-103:", sum(len(n) + 1 for n in items), "bytes   EN docs 104-135:", sum(len(n) + 1 for n in docs), "bytes")
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read(); base = 0x80010000 - 0x800
blob = exe[0x80093C20 - base:0x80093C20 - base + 1232]
jp = blob.split(b"\xF7")[1:137]
print("JP items 0-103:", sum(len(n) + 1 for n in jp[:104]), "bytes   JP docs:", sum(len(n) + 1 for n in jp[104:]), "bytes")
print("budget 1232; EN total:", sum(len(n) + 1 for n in names))
print("longest EN names:", sorted((dec(n) for n in names), key=len, reverse=True)[:8])
