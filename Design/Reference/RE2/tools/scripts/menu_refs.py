import sys, os, struct, re
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read(); base = 0x80010000 - 0x800
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN); md.skipdata = True
for fname, a, b in (("St_init_disp_menu0", 0x8006D344, 0x8006D43C), ("St_init_disp_menu1", 0x8006D654, 0x8006D7C8), ("St_disp_menu1", 0x8006D7C8, 0x8006D9D4)):
    ins = list(md.disasm(exe[a - base:b - base], a)); hi = {}; addrs = set()
    for x in ins:
        m = re.match(r"\$(\w+), (0x[0-9a-f]+)$", x.op_str)
        if x.mnemonic == "lui" and m: hi[m.group(1)] = int(m.group(2), 16) << 16
        m = re.match(r"\$(\w+), \$(\w+), (-?0x[0-9a-f]+)$", x.op_str)
        if x.mnemonic == "addiu" and m and m.group(2) in hi: addrs.add((hi[m.group(2)] + int(m.group(3), 16)) & 0xFFFFFFFF)
        m = re.match(r"\$\w+, (-?0x[0-9a-f]+)\(\$(\w+)\)$", x.op_str)
        if m and m.group(2) in hi: addrs.add((hi[m.group(2)] + int(m.group(1), 16)) & 0xFFFFFFFF)
    print(fname, "jal:", sorted({x.op_str for x in ins if x.mnemonic == "jal"}))
    print("   data addrs:", [hex(v) for v in sorted(addrs) if 0x80090000 <= v < 0x800E0000])
