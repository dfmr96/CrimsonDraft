import sys, os, struct, re
b2 = os.path.join(sys.argv[1], "psx", "beta2")
exe = open(os.path.join(b2, "PSX.EXE"), "rb").read(); base = 0x80010000 - 0x800
text_end = 0x80010000 + struct.unpack_from("<I", exe, 0x1C)[0]
funcs = []
for line in open(os.path.join(b2, "symbols.txt"), encoding="utf-8"):
    m = re.match(r"^([0-9A-F]{8}) (\S+)  \[", line)
    if m:
        a = int(m.group(1), 16)
        if 0x80010000 <= a < text_end: funcs.append((a, m.group(2)))
funcs = sorted(set(funcs))
words = struct.unpack(f"<{(len(exe) - 0x800) // 4}I", exe[0x800:0x800 + (len(exe) - 0x800) // 4 * 4])
jal_targets = {((w & 0x3FFFFFF) << 2) | 0x80000000 for w in words if (w >> 26) == 3}
data_ptrs = set(words)
lo_imm = {}
for i, w in enumerate(words):                       # lui hi + addiu/ori lo pairs
    if (w >> 26) == 15:
        hi = (w & 0xFFFF) << 16
        for j in range(i + 1, min(i + 8, len(words))):
            w2 = words[j]
            if (w2 >> 26) in (9, 13):
                lo = w2 & 0xFFFF
                lo_imm[(hi + (lo if (w2 >> 26) == 13 or lo < 0x8000 else lo - 0x10000)) & 0xFFFFFFFF] = 1
dead = []
for k, (a, name) in enumerate(funcs):
    end = funcs[k + 1][0] if k + 1 < len(funcs) else text_end
    if a not in jal_targets and a not in data_ptrs and a not in lo_imm:
        dead.append((a, end - a, name))
dead.sort(key=lambda d: -d[1])
print("uncalled functions:", len(dead), " total bytes:", sum(d[1] for d in dead))
for a, size, name in dead[:15]:
    print(f"  0x{a:08X} {size:6d} bytes  {name}")
