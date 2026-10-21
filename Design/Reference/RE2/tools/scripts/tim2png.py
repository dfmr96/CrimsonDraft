"""Extract the first PSX TIM texture found in a file and write it as PNG (pure Python).

4bpp/8bpp TIMs with several CLUT rows: each 128-pixel-wide column of the image uses the CLUT row
of the same index (common RE2 layout); falls back to row 0.
Usage: python -I tim2png.py <in_file> <out_png>
"""
import struct
import sys
import zlib


def find_tim(data):
    pos = 0
    while True:
        pos = data.find(b"\x10\x00\x00\x00", pos)
        if pos == -1 or pos + 8 > len(data):
            return None
        flags = struct.unpack_from("<I", data, pos + 4)[0]
        if flags in (0x08, 0x09, 0x02, 0x03):
            return pos
        pos += 4


def rgb555(c):
    r = (c & 31) * 255 // 31
    g = ((c >> 5) & 31) * 255 // 31
    b = ((c >> 10) & 31) * 255 // 31
    return r, g, b


def write_png(path, w, h, rows):
    raw = b"".join(b"\x00" + bytes(r) for r in rows)

    def chunk(tag, body):
        c = struct.pack(">I", len(body)) + tag + body
        return c + struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    open(path, "wb").write(png)


def main():
    data = open(sys.argv[1], "rb").read()
    pos = find_tim(data)
    if pos is None:
        print("no TIM found")
        return
    flags = struct.unpack_from("<I", data, pos + 4)[0]
    mode = flags & 7
    p = pos + 8
    clut = []
    clut_w = 0
    if flags & 8:
        blen, _, _, clut_w, clut_h = struct.unpack_from("<IHHHH", data, p)
        cols = struct.unpack_from(f"<{clut_w * clut_h}H", data, p + 12)
        clut = [cols[i * clut_w:(i + 1) * clut_w] for i in range(clut_h)]
        p += blen
    blen, _, _, w16, h = struct.unpack_from("<IHHHH", data, p)
    pix = data[p + 12:p + blen]
    rows = []
    if mode == 0:
        w = w16 * 4
        for y in range(h):
            row = bytearray()
            for x in range(w):
                b = pix[y * w16 * 2 + x // 2]
                idx = (b >> 4) if x & 1 else (b & 15)
                pal = clut[min(x // 128, len(clut) - 1)]
                row += bytes(rgb555(pal[idx]))
            rows.append(row)
    elif mode == 1:
        w = w16 * 2
        for y in range(h):
            row = bytearray()
            for x in range(w):
                idx = pix[y * w + x]
                pal = clut[min(x // 128, len(clut) - 1)]
                row += bytes(rgb555(pal[idx]))
            rows.append(row)
    else:
        w = w16
        for y in range(h):
            row = bytearray()
            for x in range(w):
                row += bytes(rgb555(struct.unpack_from("<H", pix, (y * w + x) * 2)[0]))
            rows.append(row)
    write_png(sys.argv[2], w, h, rows)
    print(f"TIM at 0x{pos:X}, mode {mode}, {w}x{h}, cluts {len(clut)}x{clut_w} -> {sys.argv[2]}")


if __name__ == "__main__":
    main()
