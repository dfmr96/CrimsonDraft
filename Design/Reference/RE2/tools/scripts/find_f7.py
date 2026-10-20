import sys, os, struct
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read()
base = 0x80010000 - 0x800
# Find windows of 1 KB with many 0xF7 bytes (string separators).
best = []
for start in range(0x800, len(exe) - 0x400, 0x100):
    n = exe[start:start + 0x400].count(0xF7)
    if n >= 40:
        best.append((start, n))
# Merge consecutive windows into regions.
regions = []
for st, n in best:
    if regions and st <= regions[-1][1]:
        regions[-1][1] = st + 0x400; regions[-1][2] = max(regions[-1][2], n)
    else:
        regions.append([st, st + 0x400, n])
for a, b, n in regions:
    print(f"region VA 0x{base + a:X}-0x{base + b:X} ({b - a} bytes), max {n} separators/KB")
