import sys, os, struct
sys.path.insert(0, os.path.join(sys.argv[1], "scripts")); sys.path.insert(0, os.path.join(sys.argv[1], "re2tool"))
from beta_english import rewrap
EN = " .___()__\u201c\u201d_0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz_________"
def show(b):
    out, i = "", 0
    while i < len(b):
        c = b[i]
        if c == 0xFE: break
        if c == 0xFC: out += "\n    "
        elif c == 0xFD: out += "\n    --- página ---\n    "; i += 1
        elif c in (0xF9, 0xFA, 0xFB): i += 1
        elif c < len(EN): out += EN[c]
        i += 1
    return out
d = os.path.join(sys.argv[1], "psx", "beta2", "rdt"); shown = 0; longest = 0
for name in sorted(os.listdir(d)):
    data = open(os.path.join(d, name), "rb").read()
    t = struct.unpack_from("<23i", data, 8)
    if not t[14]: continue
    end = min([o for o in t if o > t[14]] + [len(data)])
    tb = data[t[14]:end]; n = struct.unpack_from("<H", tb)[0] // 2
    offs = list(struct.unpack_from(f"<{n}H", tb)) + [len(tb)]
    for k in range(n):
        orig = tb[offs[k]:offs[k+1]]; new = rewrap(orig, 18)
        assert len(new) == len(orig)
        for line in show(new).split("\n"):
            longest = max(longest, len(line.strip()))
        if shown < 6 and len(orig) > 40:
            print(f"{name} msg {k}:\n    {show(new)}\n"); shown += 1
print("longest line after re-wrap:", longest)
