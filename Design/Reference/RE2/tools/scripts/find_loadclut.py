import sys, os, re
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read()
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN); md.skipdata = True
ins = list(md.disasm(exe[0x800:], 0x80010000))
names = {"0x80086c70": "LoadClut", "0x80086cd8": "LoadClut2"}
for i, x in enumerate(ins):
    if x.mnemonic == "jal" and x.op_str in names:
        args = {}
        for w in ins[max(0, i - 10): i + 2]:
            m = re.match(r"\$(a[0-3]), \$zero, (0x[0-9a-f]+|\d+)$", w.op_str)
            if w.mnemonic in ("addiu", "ori") and m: args[m.group(1)] = int(m.group(2), 0)
        print(f"{x.address:08X} {names[x.op_str]} args {args}")
