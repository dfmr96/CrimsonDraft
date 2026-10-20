import re, sys
lines = open(sys.argv[1]).read().split("\n")
w = re.compile(r"mov word ptr \[e\w\w \+ 0x156\], (\w+)$")
for i, l in enumerate(lines):
    if not w.search(l):
        continue
    prev = lines[max(0, i - 15):i]
    if any("0x156]" in p and ("mov" in p) for p in prev) and any(re.search(r"\bsub\b", p) for p in prev):
        print("----"); print("\n".join(prev + [">> " + l]))
