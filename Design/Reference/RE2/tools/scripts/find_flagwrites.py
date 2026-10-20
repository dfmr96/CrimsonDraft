import sys
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
exe = open(sys.argv[1], "rb").read()
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN); md.skipdata = True
ins = list(md.disasm(exe[0x800:], 0x80010000))
for i, x in enumerate(ins):
    if x.mnemonic == "sw" and "0x2510(" in x.op_str:
        if any(w.mnemonic == "lui" and w.op_str.endswith("0x800c") for w in ins[max(0, i - 6):i]):
            print("----")
            for w in ins[max(0, i - 7):i + 1]:
                print(f"{w.address:08X}: {w.mnemonic} {w.op_str}")
