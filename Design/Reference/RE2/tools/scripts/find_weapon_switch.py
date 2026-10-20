import re, sys
lines = open(sys.argv[1]).read().split("\n")
for i, l in enumerate(lines):
    if re.search(r"jmp dword ptr \[e\w\w\*4 \+ 0x", l):
        prev = lines[max(0, i - 4):i]
        m = [re.search(r"cmp e\w\w, (0x1[0-3]|1[89])$", p) for p in prev]
        if any(m):
            print("----"); print("\n".join(prev + [">> " + l]))
