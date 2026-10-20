import sys, os, struct
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read()
base = 0x80010000 - 0x800
start = 0x80093C20 - base
# Walk names: each begins after 0xF7; stop when a segment is implausible (very long or has 0x00 runs).
pos, names = start, []
while exe[pos] == 0xF7:
    nxt = exe.find(b"\xF7", pos + 1)
    seg = exe[pos + 1:nxt]
    if nxt - pos > 40 or seg.count(0) > 2:
        break
    names.append((pos + 1 - start, seg)); pos = nxt
print("names found:", len(names), " blob bytes:", pos - start)
rel = [p for p, _ in names]
# Look for a u16 table containing these offsets (relative to blob start or to the byte before).
for adj in (0, -1, 1):
    pat = struct.pack(f"<{min(8, len(rel))}H", *[r + adj for r in rel[:8]])
    p = exe.find(pat)
    print(f"u16 offset table (adj {adj}):", hex(base + p) if p != -1 else "not found")
# Also check the u16 table right before the blob (PC style: header of offsets).
print("first offsets:", rel[:10])
