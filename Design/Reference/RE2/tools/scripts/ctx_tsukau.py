import re, os, sys
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read(); base = 0x80010000 - 0x800
for m in re.finditer(rb"[\xEA-\xEF].\x59", exe, re.S):
    a = m.start() - 48
    print(f"--- hit VA 0x{base + m.start():X}")
    for r in range(0, 112, 16):
        print(f"  {base + a + r:08X}: " + exe[a + r:a + r + 16].hex(" "))
