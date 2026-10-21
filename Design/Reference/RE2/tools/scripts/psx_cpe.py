import struct, sys, os
b2 = os.path.join(sys.argv[1], "psx", "beta2")
cpe = open(os.path.join(b2, "MAIN.CPE"), "rb").read()
pos = 4; chunks = []
while pos < len(cpe):
    t = cpe[pos]; pos += 1
    if t == 0: break
    if t == 1:
        addr, ln = struct.unpack_from("<II", cpe, pos); pos += 8 + ln; chunks.append((addr, ln))
    elif t == 3:
        pos += 2 + 4
    elif t == 8:
        pos += 1
    else:
        print("unknown CPE chunk", hex(t), "at", hex(pos - 1)); break
lo = min(a for a, _ in chunks); hi = max(a + l for a, l in chunks)
print(f"CPE: {len(chunks)} load chunks, range 0x{lo:X}-0x{hi:X}")
over = [(a, l) for a, l in chunks if a >= 0x80200000]
print("chunks above 2MB:", [(hex(a), l) for a, l in over[:10]], "count", len(over))
exe = open(os.path.join(b2, "PSX.EXE"), "rb").read()
base = 0x80010000 - 0x800
for name, target in (("Debug_menu", 0x800134B4), ("Padd2_menu", 0x80014230)):
    word = struct.pack("<I", 0x0C000000 | ((target >> 2) & 0x3FFFFFF))
    hits, p = [], exe.find(word)
    while p != -1:
        hits.append(hex(base + p)); p = exe.find(word, p + 1)
    print(f"jal {name}: {hits}")
