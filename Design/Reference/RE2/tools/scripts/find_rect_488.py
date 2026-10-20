import sys, os
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read()
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN); md.skipdata = True
ins = list(md.disasm(exe[0x800:], 0x80010000))
for i, x in enumerate(ins):
    if x.mnemonic == "addiu" and x.op_str.endswith(", 0x1e8") and "$zero" in x.op_str:
        win = ins[max(0, i - 8): i + 10]
        if any(w.op_str.endswith(", 0x100") for w in win):
            print("----")
            for w in win: print(f"{w.address:08X}: {w.mnemonic} {w.op_str}")
