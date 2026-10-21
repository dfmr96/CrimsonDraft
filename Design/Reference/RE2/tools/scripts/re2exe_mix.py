import sys, os
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from re2scd import ITEMS
data = open(sys.argv[2], "rb").read()
IMAGE_BASE, DATA_RVA, DATA_RAW = 0x400000, 0x122000, 0x120400
def va_to_off(va): return va - IMAGE_BASE - DATA_RVA + DATA_RAW
def nm(v): return ITEMS[v] if v < len(ITEMS) else f"ITEM_{v}"
start, end = va_to_off(0x547D60), va_to_off(0x547EA8)
for off in range(start, end, 4):
    b = data[off:off+4]
    va = off - DATA_RAW + DATA_RVA + IMAGE_BASE
    print(f"VA 0x{va:X}: " + " ".join(f"{x:02X}" for x in b) + f"   with={nm(b[0]):16s} b1={b[1]:3d} result={nm(b[2]):16s} b3={b[3]}")
