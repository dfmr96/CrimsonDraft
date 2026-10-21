import struct, sys
data = open(sys.argv[1], "rb").read()
off = struct.unpack_from("<i", data, 8 + 8 * 4)[0]
print("cameras:", data[1])
i = 0
while True:
    rec = data[off + i * 20: off + i * 20 + 20]
    flag, floor, frm, to = rec[0], rec[1], rec[2], rec[3]
    pts = struct.unpack_from("<8h", rec, 4)
    if struct.unpack_from("<I", rec, 0)[0] == 0xFFFFFFFF:
        print(f"[{i}] END marker"); break
    quad = [(pts[k], pts[k + 1]) for k in range(0, 8, 2)]
    print(f"[{i:2d}] enabled={flag} floor={'any' if floor == 255 else floor} from=cam{frm} to=cam{to} quad={quad}")
    i += 1
    if i > 60: break
