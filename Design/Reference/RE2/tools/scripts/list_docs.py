import sys
pc = open(sys.argv[1], "rb").read()
EN = " .___()__\u201c\u201d_0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz_________"
enc = lambda t: bytes(EN.index(c) for c in t); dec = lambda b: "".join(EN[c] if c < len(EN) else "?" for c in b)
start = pc.rfind(b"\xF7", 0, pc.find(enc("Knife") + b"\xF7"))
names, pos = [], start
while pc[pos] == 0xF7 and len(names) < 136:
    nxt = pc.find(b"\xF7", pos + 1); names.append(pc[pos + 1:nxt]); pos = nxt
for i in range(96, 136):
    print(i, repr(dec(names[i])), len(names[i]))
