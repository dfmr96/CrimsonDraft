"""Look for a function from the Beta 2 exe (located via MAIN.SYM) inside other PS-X EXEs,
comparing MIPS instructions with immediates/targets masked out (addresses differ per build).

Usage: python -I psx_funcmatch.py <beta2 PSX.EXE> <func_va_hex> <n_instr> <exe>...
"""
import struct
import sys


def load(path):
    data = open(path, "rb").read()
    load_addr = struct.unpack_from("<I", data, 0x18)[0]
    return data[0x800:], load_addr


def mask(word):
    op = word >> 26
    if op in (2, 3):                 # j / jal: keep opcode only
        return word & 0xFC000000
    if op == 0:                      # R-type: fully significant
        return word
    return word & 0xFFFF0000         # I-type: drop the 16-bit immediate


def words(code):
    return [mask(w) for w in struct.unpack(f"<{len(code) // 4}I", code[:len(code) // 4 * 4])]


def main():
    src, src_base = load(sys.argv[1])
    va, n = int(sys.argv[2], 16), int(sys.argv[3])
    pat = words(src[va - src_base: va - src_base + n * 4])
    for path in sys.argv[4:]:
        code, base = load(path)
        w = words(code)
        best = (0, None)
        for i in range(len(w) - n):
            if w[i] != pat[0]:
                continue
            score = sum(1 for k in range(n) if w[i + k] == pat[k])
            if score > best[0]:
                best = (score, base + i * 4)
        loc = f"0x{best[1]:X}" if best[1] else "-"
        print(f"{path.split(chr(92))[-2]:12s} best match {best[0]}/{n} instructions at {loc}")


if __name__ == "__main__":
    main()
