"""MIPS disassembler for Beta 2 PSX.EXE with MAIN.SYM names on calls.
Usage: python -I mipsdis2.py <PSX.EXE> <symbols.txt> <func name | start_hex> [end_hex]
"""
import re, sys
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
exe = open(sys.argv[1], "rb").read()
base = 0x80010000 - 0x800
syms = {}
for line in open(sys.argv[2], encoding="utf-8", errors="replace"):
    m = re.match(r"([0-9A-F]{8}) (\S+)", line)
    if m: syms.setdefault(int(m.group(1), 16), m.group(2))
by_name = {v: k for k, v in syms.items()}
addrs = sorted(syms)
a = sys.argv[3]
start = by_name[a] if a in by_name else int(a, 16)
if len(sys.argv) > 4: end = int(sys.argv[4], 16)
else: end = next(x for x in addrs if x > start)
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN); md.skipdata = True
print(f"== {syms.get(start, hex(start))} {start:08X}-{end:08X}")
for ins in md.disasm(exe[start - base:end - base], start):
    s = f"{ins.address:08X}: {ins.mnemonic} {ins.op_str}"
    for h in re.findall(r"0x([0-9a-f]{6,8})", ins.op_str):
        v = int(h, 16)
        if v in syms: s += f"   ; {syms[v]}"
    print(s)
