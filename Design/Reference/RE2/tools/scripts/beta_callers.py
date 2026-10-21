"""Callers (jal) of Beta 2 functions, reported with the containing function name.
Usage: python -I beta_callers.py <PSX.EXE> <symbols.txt> <name|hex> [...]
"""
import bisect, re, struct, sys
exe = open(sys.argv[1], 'rb').read(); base = 0x80010000 - 0x800
syms = {}
for l in open(sys.argv[2], encoding='utf-8', errors='replace'):
    m = re.match(r'([0-9A-F]{8}) (\S+)', l)
    if m and int(m.group(1), 16) >= 0x80010000: syms.setdefault(int(m.group(1), 16), m.group(2))
by = {v: k for k, v in syms.items()}; addrs = sorted(syms)
def owner(a):
    i = bisect.bisect_right(addrs, a) - 1
    return f"{syms[addrs[i]]}+0x{a - addrs[i]:X}" if i >= 0 else hex(a)
for arg in sys.argv[3:]:
    t = by.get(arg) or int(arg, 16)
    w = struct.pack('<I', 0x0C000000 | ((t >> 2) & 0x3FFFFFF))
    hits, p = [], exe.find(w)
    while p != -1:
        if p % 4 == 0: hits.append(owner(base + p))
        p = exe.find(w, p + 1)
    print(f"{syms.get(t, arg)}: {', '.join(hits) or '(no direct jal)'}")
