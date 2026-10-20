import sys, os, struct, zlib
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read(); base = 0x80010000 - 0x800
raw = exe[0x8009DD00 - base: 0x8009DD00 - base + 0x60]
for r in range(0, 0x60, 16): print(f"{0x8009DD00 + r:08X}: " + raw[r:r + 16].hex(" "))
# Upscale the TIM preview 4x (nearest) for inspection.
src = os.path.join(sys.argv[1], "psx", "beta2", "tim", "ST_MOJI.png")
import re
data = open(src, "rb").read()
w, h = struct.unpack(">II", data[16:24])
idat = b"".join(data[m.start() + 8: m.start() + 8 + struct.unpack(">I", data[m.start():m.start() + 4])[0]] for m in re.finditer(b"IDAT", data) for _ in [0]) if False else None
