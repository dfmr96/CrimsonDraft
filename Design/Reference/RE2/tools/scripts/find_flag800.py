import struct, sys
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
exe = open(sys.argv[1], "rb").read(); base = 0x80010000 - 0x800
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
ins = list(md.disasm(exe[0x800:], 0x80010000))
for i, x in enumerate(ins):
    if x.mnemonic == "sw" and x.op_str.endswith("0x2510($at)") or (x.mnemonic == "sw" and "0x2510(" in x.op_str):
        window = ins[max(0, i - 8):i + 1]
        if any(w.mnemonic == "ori" and w.op_str.endswith("0x800") for w in window):
            print("----")
            for w in window:
                print(f"{w.address:08X}: {w.mnemonic} {w.op_str}")
