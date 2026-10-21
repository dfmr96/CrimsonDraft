import struct, sys
data = open(sys.argv[1], "rb").read(); base = struct.unpack_from("<I", data, 0x18)[0] - 0x800
target = 0x80013BBC; lo = target & 0xFFFF; hi = (target >> 16) + (1 if lo & 0x8000 else 0)
ws = struct.unpack(f"<{len(data)//4}I", data[:len(data)//4*4])
found = []
for i, w in enumerate(ws):
    if (w >> 26) in (9, 13) and (w & 0xFFFF) == lo:        # addiu / ori with the low half
        if any((ws[j] >> 26) == 15 and (ws[j] & 0xFFFF) == hi for j in range(max(0, i - 6), i)):
            found.append(hex(base + i * 4))
print("address built at:", found)
p = data.find(struct.pack("<I", target))
print("as data pointer:", hex(base + p) if p != -1 else None)
