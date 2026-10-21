"""Linear-sweep disassembler + cross-reference index for a 32-bit PE (RE2 PC exe).

Usage:
  python -I re2dis.py build <exe> <out_dir>
  python -I re2dis.py xref  <out_dir> <lo_hex> [hi_hex] [context]
  python -I re2dis.py at    <out_dir> <va_hex> [before] [after]
  python -I re2dis.py imm   <out_dir> <value_hex>        (instructions using an exact immediate)
"""
import bisect
import os
import pickle
import struct
import sys
from collections import defaultdict


def pe_sections(data):
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    n = struct.unpack_from("<H", data, pe + 6)[0]
    opt = struct.unpack_from("<H", data, pe + 20)[0]
    image_base = struct.unpack_from("<I", data, pe + 24 + 28)[0]
    secs = []
    base = pe + 24 + opt
    for i in range(n):
        o = base + 40 * i
        name = data[o:o + 8].rstrip(b"\0").decode()
        vsize, va, rsize, rptr = struct.unpack_from("<IIII", data, o + 8)
        secs.append((name, va, vsize, rptr, rsize))
    return image_base, secs


def build(exe, out):
    from capstone import CS_ARCH_X86, CS_MODE_32, Cs
    from capstone.x86 import X86_OP_IMM, X86_OP_MEM

    data = open(exe, "rb").read()
    image_base, secs = pe_sections(data)
    text = next(s for s in secs if s[0] == ".text")
    _, va, vsize, rptr, rsize = text
    code = data[rptr:rptr + min(vsize, rsize)]
    md = Cs(CS_ARCH_X86, CS_MODE_32)
    md.detail = True
    addrs, lines = [], []
    refs = defaultdict(list)
    imms = defaultdict(list)
    pos = 0
    start_va = image_base + va
    while pos < len(code):
        got = False
        for ins in md.disasm(code[pos:], start_va + pos):
            got = True
            addrs.append(ins.address)
            lines.append(f"{ins.address:08X}: {ins.mnemonic} {ins.op_str}")
            for op in ins.operands:
                if op.type == X86_OP_IMM:
                    v = op.imm & 0xFFFFFFFF
                    imms[v].append(ins.address)
                    if image_base <= v < image_base + 0x1000000:
                        refs[v].append(ins.address)
                elif op.type == X86_OP_MEM and op.mem.disp:
                    v = op.mem.disp & 0xFFFFFFFF
                    if image_base <= v < image_base + 0x1000000:
                        refs[v].append(ins.address)
            pos = ins.address + ins.size - start_va
        if not got or pos < len(code):
            # Undecodable byte (data inside .text): record and skip it.
            if pos < len(code):
                addrs.append(start_va + pos)
                lines.append(f"{start_va + pos:08X}: db 0x{code[pos]:02X}")
                pos += 1
    os.makedirs(out, exist_ok=True)
    with open(os.path.join(out, "listing.txt"), "w") as f:
        f.write("\n".join(lines))
    with open(os.path.join(out, "index.pkl"), "wb") as f:
        pickle.dump({"addrs": addrs, "refs": dict(refs), "imms": dict(imms), "image_base": image_base,
                     "secs": secs}, f)
    print(f"{len(lines)} instructions, {len(refs)} referenced addresses")


def load(out):
    idx = pickle.load(open(os.path.join(out, "index.pkl"), "rb"))
    lines = open(os.path.join(out, "listing.txt")).read().split("\n")
    return idx, lines


def show(idx, lines, va, before, after):
    i = bisect.bisect_left(idx["addrs"], va)
    for j in range(max(0, i - before), min(len(lines), i + after + 1)):
        mark = ">>" if j == i else "  "
        print(f"{mark} {lines[j]}")


def main():
    cmd = sys.argv[1]
    if cmd == "build":
        build(sys.argv[2], sys.argv[3])
        return
    idx, lines = load(sys.argv[2])
    if cmd == "xref":
        lo = int(sys.argv[3], 16)
        hi = int(sys.argv[4], 16) if len(sys.argv) > 4 else lo + 1
        ctx = int(sys.argv[5]) if len(sys.argv) > 5 else 0
        hits = sorted((a, v) for v, al in idx["refs"].items() if lo <= v < hi for a in al)
        for a, v in hits:
            print(f"---- ref 0x{v:X} at 0x{a:X}")
            show(idx, lines, a, ctx, ctx)
        print(f"[{len(hits)} refs]")
    elif cmd == "at":
        va = int(sys.argv[3], 16)
        before = int(sys.argv[4]) if len(sys.argv) > 4 else 10
        after = int(sys.argv[5]) if len(sys.argv) > 5 else 40
        show(idx, lines, va, before, after)
    elif cmd == "imm":
        v = int(sys.argv[3], 16)
        for a in idx["imms"].get(v, []):
            print(f"0x{a:X}: {lines[bisect.bisect_left(idx['addrs'], a)]}")


if __name__ == "__main__":
    main()
