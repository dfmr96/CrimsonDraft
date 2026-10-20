import sys, os, struct, zlib
tim = open(os.path.join(sys.argv[1], "psx", "beta2", "tim", "ST_MOJI.TIM"), "rb").read()
flags = struct.unpack_from("<I", tim, 4)[0]; p = 8
clut_len = struct.unpack_from("<I", tim, p)[0]; p += clut_len
blen, ix, iy, w16, h = struct.unpack_from("<IHHHH", tim, p); pix = tim[p + 12:p + blen]
W = w16 * 4
idx = [[(pix[y * w16 * 2 + x // 2] >> (4 if x & 1 else 0)) & 15 for x in range(W)] for y in range(h)]
y0, y1, S = 40, 72, 4
rows = []
for y in range(y0, y1):
    row = bytearray()
    for x in range(W):
        v = idx[y][x] * 17
        c = (v, v, v) if x % 48 else (255, 0, 0)
        row += bytes(c) * S
    for _ in range(S): rows.append(bytes(row))
def png(path, w, h, rows):
    raw = b"".join(b"\0" + r for r in rows)
    ch = lambda t, b: struct.pack(">I", len(b)) + t + b + struct.pack(">I", zlib.crc32(t + b) & 0xFFFFFFFF)
    open(path, "wb").write(b"\x89PNG\r\n\x1a\n" + ch(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) + ch(b"IDAT", zlib.compress(raw)) + ch(b"IEND", b""))
png(os.path.join(sys.argv[1], "psx", "beta2", "tim", "menu_row.png"), W * S, (y1 - y0) * S, rows)
print("image", W, "x", h, "| texture VRAM pos", ix, iy)
