import struct, sys
data = open(sys.argv[1], "rb").read()
RAW, RVA, SIZE, BASE = 0x120400, 0x122000, 0x2B600, 0x400000
TEXT_LO, TEXT_HI = 0x401000, 0x401000 + 0x11C1E8
run_start, run = None, 0
for off in range(RAW, RAW + SIZE - 4, 4):
    v = struct.unpack_from("<I", data, off)[0]
    if TEXT_LO <= v < TEXT_HI:
        if run == 0:
            run_start = off
        run += 1
    else:
        if run >= 120:
            va = run_start - RAW + RVA + BASE
            print(f"run of {run} code pointers at VA 0x{va:X}")
        run = 0
