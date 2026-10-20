import sys
pc = open(sys.argv[1], "rb").read()
EN = " .___()__\u201c\u201d_0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz_________"
enc = lambda t: bytes(EN.index(c) for c in t)
start = pc.rfind(b"\xF7", 0, pc.find(enc("Knife") + b"\xF7"))
names, pos = [], start
while pc[pos] == 0xF7 and len(names) < 136:
    nxt = pc.find(b"\xF7", pos + 1); names.append(pc[pos + 1:nxt]); pos = nxt
uniq = sorted(set(names), key=len, reverse=True)
kept = [u for u in uniq if not any(o != u and o.endswith(u) for o in uniq)]   # suffix sharing
size = 1 + sum(len(k) + 1 for k in kept)
print(f"136 English names: naive {sum(len(n) + 1 for n in names)} B, unique {1 + sum(len(u) + 1 for u in uniq)} B, with suffix sharing {size} B  (budget 1232)")
