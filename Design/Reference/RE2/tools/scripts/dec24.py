import re, sys
data = open(sys.argv[1], "rb").read()
dec = bytes((b + 0x24) if 0x00 <= b + 0x24 - 0x20 < 0x5F and b < 0x5B else 0 for b in data)
for m in re.finditer(rb"[ -~]{4,}", dec):
    s = m.group().decode()
    letters = sum(c.isalpha() for c in s)
    if letters >= 4 and letters / len(s) > 0.6 and re.search(r"[a-z]{3}", s):
        print(f"0x{m.start():X}: {s}")
