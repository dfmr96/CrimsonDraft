import sys, os, struct
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read()
base = 0x80010000 - 0x800
def off(va): return va - base
a = off(0x80093800)
chunk = exe[a:a + 0x500]
for row in range(0, 0x500, 32):
    print(f"{0x80093800 + row:08X}: " + chunk[row:row + 32].hex(" "))
