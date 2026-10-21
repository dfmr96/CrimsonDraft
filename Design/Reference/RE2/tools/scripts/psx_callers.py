import struct, sys
data = open(sys.argv[1], "rb").read(); base = struct.unpack_from("<I", data, 0x18)[0] - 0x800
t = int(sys.argv[2], 16)
w = struct.pack("<I", 0x0C000000 | ((t >> 2) & 0x3FFFFFF)); p = data.find(w); hits = []
while p != -1:
    hits.append(base + p); p = data.find(w, p + 1)
print("callers:", [hex(h) for h in hits])
