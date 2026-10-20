import sys, os, struct
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read(); base = 0x80010000 - 0x800
for label, pat in (("u16 173", struct.pack("<H", 173)), ("u32 173", struct.pack("<I", 173))):
    pass
# Look for small tables that contain 173 together with other status-file codes (161..174).
codes = set(range(161, 175))
for width, fmt in ((1, "B"), (2, "<H"), (4, "<I")):
    for off in range(0x800, len(exe) - 16 * width, width):
        vals = [struct.unpack_from(fmt, exe, off + k * width)[0] for k in range(6)]
        if 173 in vals and sum(v in codes for v in vals) >= 3:
            print(f"{width}-byte table near VA 0x{base + off:X}: {vals}")
            break
