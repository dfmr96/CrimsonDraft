"""Print a table of function pointers from Beta 2 PSX.EXE with MAIN.SYM names.
Usage: python -I beta_table.py <PSX.EXE> <symbols.txt> <addr_hex> <count>
"""
import struct, re, sys
exe = open(sys.argv[1], 'rb').read(); base = 0x80010000 - 0x800
syms = {}
for l in open(sys.argv[2], encoding='utf-8', errors='replace'):
    m = re.match(r'([0-9A-F]{8}) (\S+)', l)
    if m: syms.setdefault(int(m.group(1), 16), m.group(2))
t, n = int(sys.argv[3], 16), int(sys.argv[4])
for k in range(n):
    v = struct.unpack_from('<I', exe, t - base + 4 * k)[0]
    print(f"  {k:2} {v:08X} {syms.get(v, '?')}")
