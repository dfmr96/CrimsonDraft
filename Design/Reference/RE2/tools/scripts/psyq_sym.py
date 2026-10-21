"""Parse a Psy-Q MAIN.SYM (MND v1) file: dump functions (with source file) and symbols.

Usage: python -I psyq_sym.py <MAIN.SYM> <out_txt>
"""
import struct
import sys


def main():
    data = open(sys.argv[1], "rb").read()
    assert data[:3] == b"MND"
    pos = 8
    funcs, syms, files = [], [], set()

    def pstr(p):
        n = data[p]
        return data[p + 1:p + 1 + n].decode("latin-1"), p + 1 + n

    while pos + 5 <= len(data):
        addr = struct.unpack_from("<I", data, pos)[0]
        t = data[pos + 4]
        pos += 5
        if t in (1, 2):                      # symbol / label
            name, pos = pstr(pos)
            syms.append((addr, name))
        elif t == 0x80:
            pass
        elif t == 0x82:
            pos += 1
        elif t == 0x84:
            pos += 2
        elif t == 0x86:
            pos += 4
        elif t == 0x88:
            pos += 4
            fname, pos = pstr(pos)
            files.add(fname)
        elif t == 0x8A:
            pass
        elif t == 0x8C:                      # function start
            pos += 2 + 4 + 2 + 4 + 4 + 4
            fname, pos = pstr(pos)
            name, pos = pstr(pos)
            funcs.append((addr, name, fname))
            files.add(fname)
        elif t in (0x8E, 0x90, 0x92):
            pos += 4
        elif t == 0x94:                      # def
            pos += 2 + 2 + 4
            _, pos = pstr(pos)
        elif t == 0x96:                      # def2 (arrays)
            pos += 2 + 2 + 4
            dims = struct.unpack_from("<H", data, pos)[0]
            pos += 2 + 4 * dims
            _, pos = pstr(pos)
            _, pos = pstr(pos)
        elif t == 0x98:                      # overlay definition: u32 length, u32 overlay id
            pos += 8
        elif t == 0x9A:                      # set current overlay
            pass
        else:
            print(f"unknown record 0x{t:02X} at 0x{pos - 5:X}; stopping")
            break
    with open(sys.argv[2], "w", encoding="utf-8") as f:
        f.write("# FUNCTIONS\n")
        for a, n, fn in sorted(funcs):
            f.write(f"{a:08X} {n}  [{fn}]\n")
        f.write("\n# SYMBOLS\n")
        for a, n in sorted(syms):
            f.write(f"{a:08X} {n}\n")
        f.write("\n# SOURCE FILES\n" + "\n".join(sorted(files)) + "\n")
    print(f"{len(funcs)} functions, {len(syms)} symbols, {len(files)} source files")


if __name__ == "__main__":
    main()
