import re, sys
lines = open(sys.argv[1]).read().split("\n")
for i, l in enumerate(lines):
    if re.search(r"word ptr \[e\w\w \+ 2\]", l):
        prev = lines[max(0, i - 8):i]
        if any(re.search(r"shl e\w\w, 5$", p) for p in prev):
            print("----"); print("\n".join(lines[max(0, i - 8):i + 8]))
