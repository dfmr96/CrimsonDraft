"""Dump struct/union definitions (tag + members with offsets) from a Psy-Q MAIN.SYM (MND v1).

Usage: python -I psyq_structs.py <MAIN.SYM> <out_txt>
Def records: 0x94 = value(u32 in addr), class u16, type u16, size u32, name
             0x96 = same + dims u16, dims*u32, tag name, name
Classes: 8 = MOS (struct member), 11 = MOU (union member), 10 = STRTAG, 12 = UNTAG, 102 = EOS.
"""
import struct
import sys


def main():
    data = open(sys.argv[1], "rb").read()
    assert data[:3] == b"MND"
    pos = 8
    out = []
    seen = set()
    cur = None
    lines = []

    def pstr(p):
        n = data[p]
        return data[p + 1:p + 1 + n].decode("latin-1"), p + 1 + n

    while pos + 5 <= len(data):
        addr = struct.unpack_from("<I", data, pos)[0]
        t = data[pos + 4]
        pos += 5
        if t in (1, 2):
            _, pos = pstr(pos)
        elif t in (0x80, 0x8A, 0x9A):
            pass
        elif t == 0x82:
            pos += 1
        elif t == 0x84:
            pos += 2
        elif t in (0x86, 0x8E, 0x90, 0x92):
            pos += 4
        elif t == 0x88:
            pos += 4
            _, pos = pstr(pos)
        elif t == 0x8C:
            pos += 2 + 4 + 2 + 4 + 4 + 4
            _, pos = pstr(pos)
            _, pos = pstr(pos)
        elif t in (0x94, 0x96):
            cls, typ, size = struct.unpack_from("<HHI", data, pos)
            pos += 8
            dims = []
            tag = ""
            if t == 0x96:
                nd = struct.unpack_from("<H", data, pos)[0]
                pos += 2
                dims = list(struct.unpack_from(f"<{nd}I", data, pos))
                pos += 4 * nd
                tag, pos = pstr(pos)
            name, pos = pstr(pos)
            if cls in (10, 12):
                cur = name
                lines = [f"struct {name} (size 0x{size:X})"]
            elif cls in (8, 11) and cur is not None:
                d = "".join(f"[{x}]" for x in dims)
                lines.append(f"  +0x{addr:04X} {name}{d}  (size 0x{size:X}, type 0x{typ:X}{', ' + tag if tag else ''})")
            elif cls == 102 and cur is not None:
                key = "\n".join(lines)
                if key not in seen:
                    seen.add(key)
                    out.append(key)
                cur = None
            elif cls in (2, 3) and t in (0x94, 0x96):  # extern / static globals with their type
                d = "".join(f"[{x}]" for x in dims)
                out.append(f"global 0x{addr:08X} {name}{d} (size 0x{size:X}{', ' + tag if tag else ''})")
        elif t == 0x98:
            pos += 8
        else:
            print(f"unknown record 0x{t:02X} at 0x{pos - 5:X}; stopping")
            break
    with open(sys.argv[2], "w", encoding="utf-8") as f:
        f.write("\n".join(out) + "\n")
    print(f"{len(out)} entries")


if __name__ == "__main__":
    main()
