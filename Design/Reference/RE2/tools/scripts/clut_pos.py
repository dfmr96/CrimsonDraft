import os, sys, struct
t = os.path.join(sys.argv[1], "psx", "beta2", "tim")
for n in sorted(os.listdir(t)):
    if not n.upper().endswith(".TIM") or "_EN" in n: continue
    tim = open(os.path.join(t, n), "rb").read()
    flags = struct.unpack_from("<I", tim, 4)[0]
    if flags & 8:
        _, cx, cy, cw, ch = struct.unpack_from("<IHHHH", tim, 8)
        mark = "  <-- covers rows 480+" if cy <= 486 and cy + ch > 480 and cx <= 256 < cx + cw else ""
        print(f"{n:14s} CLUT at x={cx:4d} y={cy:4d}  {cw}x{ch}{mark}")
