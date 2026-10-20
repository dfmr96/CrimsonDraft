import math, os, struct, sys
from collections import Counter
src = sys.argv[1]
c = Counter()
for name in sorted(os.listdir(src)):
    if not name.upper().endswith(".RDT"):
        continue
    data = open(os.path.join(src, name), "rb").read()
    rid = struct.unpack_from("<i", data, 8 + 7 * 4)[0]
    for i in range(data[1]):
        c[struct.unpack_from("<H", data, rid + i * 32 + 2)[0] >> 7] += 1
total = sum(c.values())
print("| H | Cámaras | % | FOV vertical | FOV horizontal |")
print("|---|---|---|---|---|")
for h, n in sorted(c.items(), key=lambda kv: -kv[1]):
    v = math.degrees(2 * math.atan(120 / h)); hz = math.degrees(2 * math.atan(160 / h))
    print(f"| {h} | {n} | {100*n/total:.1f}% | {v:.1f}° | {hz:.1f}° |")
print("total", total)
