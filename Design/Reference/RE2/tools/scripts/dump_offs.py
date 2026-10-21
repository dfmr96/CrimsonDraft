import struct, os, sys
beta = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read(); base = 0x80010000 - 0x800
offs = struct.unpack_from("<160H", beta, 0x800940F6 - base)
print(list(offs))
blob = beta[0x80093C20 - base: 0x80093C20 - base + 1300]
seps = [i for i, b in enumerate(blob) if b == 0xF7]
print("separator positions (first 20):", seps[:20], "count within 1300:", len(seps))
