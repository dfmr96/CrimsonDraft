"""Find game-encoded strings (RE2 EN charset) inside an executable.

Usage: python -I re2exe_strings.py <exe> "Ink Ribbon" "Green Herb" ...
"""
import sys

EN = " .___()__“”_0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz_________"


def encode(s):
    return bytes(EN.index(c) for c in s)


def main():
    data = open(sys.argv[1], "rb").read()
    for word in sys.argv[2:]:
        enc = encode(word)
        pos = data.find(enc)
        hits = []
        while pos != -1 and len(hits) < 5:
            hits.append(f"0x{pos:X}")
            pos = data.find(enc, pos + 1)
        print(f"{word!r}: {', '.join(hits) if hits else 'not found'}")


if __name__ == "__main__":
    main()
