# Resident Evil 3 (PC) Rofs*.dat archive reader.
# Decryption and Hi_Comp depacking ported from reevengi-tools src/rofs.c (C) 2010 Patrice Mandin, GPL v2.
import os, struct, sys

BASE_ARRAY = [
    0x00e6, 0x01a4, 0x00e6, 0x01c5, 0x0130, 0x00e8, 0x03db, 0x008b,
    0x0141, 0x018e, 0x03ae, 0x0139, 0x00f0, 0x027a, 0x02c9, 0x01b0,
    0x01f7, 0x0081, 0x0138, 0x0285, 0x025a, 0x015b, 0x030f, 0x0335,
    0x02e4, 0x01f6, 0x0143, 0x00d1, 0x0337, 0x0385, 0x007b, 0x00c6,
    0x0335, 0x0141, 0x0186, 0x02a1, 0x024d, 0x0342, 0x01fb, 0x03e5,
    0x01b0, 0x006d, 0x0140, 0x00c0, 0x0386, 0x016b, 0x020b, 0x009a,
    0x0241, 0x00de, 0x015e, 0x035a, 0x025b, 0x0154, 0x0068, 0x02e8,
    0x0321, 0x0071, 0x01b0, 0x0232, 0x02d9, 0x0263, 0x0164, 0x0290,
]


def read_dirs(d):
    o = 0x15
    l1 = d[o:d.index(b'\0', o)]
    o += len(l1) + 1 + 8
    l2 = d[o:d.index(b'\0', o)]
    return l1.decode('ascii'), l2.decode('ascii')


def list_files(d):
    count = struct.unpack_from('<I', d, 0x1000)[0]
    o = 0x1004
    files = []
    for _ in range(count):
        ofs, size = struct.unpack_from('<II', d, o)
        end = d.index(b'\0', o + 8)
        files.append((d[o + 8:end].decode('ascii'), ofs * 8, size))
        o = end + 1
    return files


def _decrypt(buf, key):
    def nxt():
        nonlocal key
        key = (key * 0x5d588b65 + 0x8000000b) & 0xFFFFFFFF
        return key >> 24
    xor_key = nxt()
    base_index = nxt() % 0x3f
    block_index = 0
    for i in range(len(buf)):
        if block_index > BASE_ARRAY[base_index]:
            base_index = nxt() % 0x3f
            xor_key = nxt()
            block_index = 0
        buf[i] ^= xor_key
        block_index += 1


def _depack(src, max_len=32768):
    tmp = bytearray(4096 + 256)
    for i in range(256):
        tmp[i * 16:i * 16 + 16] = bytes([i]) * 16
    dst = bytearray()
    n = len(src)
    bit = si = ti = 0
    while si < n and len(dst) < max_len:
        bit += 1
        value = src[si] << bit
        si += 1
        if si < n:
            value |= src[si] >> (8 - bit)
        if bit == 8:
            si += 1
            bit = 0
        if not value & 0x100:
            b = value & 0xFF
            dst.append(b)
            tmp[ti] = b
            ti += 1
        else:
            v2 = (src[si] << bit) & 0xFF
            si += 1
            if si < n:
                v2 |= src[si] >> (8 - bit)
            length = (v2 & 0x0F) + 2
            start = ((v2 >> 4) & 0xFFF) | ((value & 0xFF) << 4)
            length = min(length, max_len - len(dst))
            chunk = bytes(tmp[start:start + length])
            dst += chunk
            tmp[ti:ti + length] = chunk
            ti += length
        if ti >= 4096:
            ti = 0
    return bytes(dst)


def extract(d, ofs):
    hdr, nkeys, length = struct.unpack_from('<HHI', d, ofs)
    ident = bytearray(d[ofs + 8:ofs + 16])
    mask = ident[7]
    compressed = bytes(b ^ mask for b in ident) == b'Hi_Comp\0'
    keys = struct.unpack_from('<%dI' % nkeys, d, ofs + 16)
    lens = struct.unpack_from('<%dI' % nkeys, d, ofs + 16 + nkeys * 4)
    out = bytearray()
    o = ofs + hdr
    for k, l in zip(keys, lens):
        blk = bytearray(d[o:o + l])
        o += l
        _decrypt(blk, k)
        out += _depack(blk) if compressed else blk
    return bytes(out[:length])


if __name__ == '__main__':
    # rofs.py <Rofs.dat> <out_dir> [name filter...]
    src, out_dir, names = sys.argv[1], sys.argv[2], set(sys.argv[3:])
    d = open(src, 'rb').read()
    for name, ofs, size in list_files(d):
        if names and name not in names:
            continue
        data = extract(d, ofs)
        open(os.path.join(out_dir, name), 'wb').write(data)
        print(name, len(data))
