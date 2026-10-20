import sys, os, struct
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
exe = open(sys.argv[1], "rb").read()
base = 0x80010000 - 0x800
start, end = int(sys.argv[2], 16), int(sys.argv[3], 16)
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN); md.skipdata = True
for ins in md.disasm(exe[start - base:end - base], start):
    print(f"{ins.address:08X}: {ins.mnemonic} {ins.op_str}")
