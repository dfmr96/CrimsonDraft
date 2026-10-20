import sys, os
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read()
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN); md.skipdata = True
ins = list(md.disasm(exe[0x800:], 0x80010000))
for i, x in enumerate(ins):
    if x.mnemonic in ("addiu", "ori") and x.op_str.endswith(("0xad", ", 173")) and "$zero" in x.op_str:
        print("----")
        for w in ins[max(0, i - 4): i + 8]:
            print(f"{w.address:08X}: {w.mnemonic} {w.op_str}")
