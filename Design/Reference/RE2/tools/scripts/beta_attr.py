import sys, os, struct
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read(); base = 0x80010000 - 0x800
SIG = {5: 8, 6: 8, 7: 5, 8: 7, 14: 100, 15: 100, 16: 100}
for stride in range(4, 17):
    for pos in range(0x800, len(exe) - stride * 20):
        if all(exe[pos + (i - 5) * stride] == v for i, v in SIG.items()):
            start = pos - 5 * stride
            print(f"stride {stride}: table id0 at VA 0x{base + start:X}")
            for i in (0, 1, 2, 5, 9, 10, 11, 14, 20, 21, 24, 25, 26, 30, 38):
                rec = exe[start + i * stride: start + i * stride + stride]
                print(f"   id {i:2d}: " + rec.hex(" "))
            raise SystemExit
print("not found")
