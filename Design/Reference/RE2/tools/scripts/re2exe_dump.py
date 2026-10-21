"""Dump a region of an executable as RE2-encoded text segments (split on 0xFE end markers).

Usage: python -I re2exe_dump.py <exe> <start_hex> <end_hex>
"""
import sys

sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
from re2scd import decode_msg  # noqa: E402


def main():
    data = open(sys.argv[1], "rb").read()
    start, end = int(sys.argv[2], 16), int(sys.argv[3], 16)
    pos = start
    while pos < end:
        nxt = data.find(b"\xFE", pos, end)
        if nxt == -1:
            nxt = end
        seg = data[pos:nxt + 2]
        print(f"0x{pos:X}: {decode_msg(seg)}")
        pos = nxt + 2


if __name__ == "__main__":
    main()
