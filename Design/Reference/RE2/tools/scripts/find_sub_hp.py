import re, sys
lines = open(sys.argv[1]).read().split("\n")
w = re.compile(r"mov word ptr \[e\w\w \+ 0x156\], (\w+)$")
hits = 0
for i, l in enumerate(lines):
    m = w.search(l)
    if not m:
        continue
    reg = m.group(1)
    prev = lines[max(0, i - 4):i]
    if any(re.search(r"\bsub\b", p) for p in prev):
        print("----"); print("\n".join(prev + [">> " + l])); hits += 1
print(f"[{hits}]")
