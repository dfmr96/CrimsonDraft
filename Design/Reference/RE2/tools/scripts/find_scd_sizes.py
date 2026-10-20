import sys
data = open(sys.argv[1], "rb").read()
sizes = bytes([1, 2, 1, 4, 4, 2, 4, 4, 1, 4, 3, 1, 1, 6, 2, 4, 2, 4, 2, 4, 6, 2, 2, 6])
p = data.find(sizes)
print("sizes table file offset:", hex(p) if p >= 0 else None)
if p >= 0:
    va = p - 0x120400 + 0x122000 + 0x400000
    print("VA:", hex(va))
