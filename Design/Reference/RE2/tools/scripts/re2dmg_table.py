"""Compact per-enemy damage matrix (weapon id 1..19 -> three 10-bit values).

Usage: python -I re2dmg_table.py <exe>
"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from re2dmg_dump import off  # noqa: E402

ENEMIES = {
    0x10: "Zombie (Cop)", 0x11: "Zombie (Brad)", 0x12: "Zombie (Guy1)", 0x13: "Zombie (Girl)",
    0x15: "Zombie (TestSubject)", 0x16: "Zombie (Scientist)", 0x17: "Zombie (Naked)", 0x18: "Zombie (Guy2)",
    0x1E: "Zombie (Guy3)", 0x1F: "Zombie (Random)", 0x20: "Zombie Dog", 0x21: "Crow", 0x22: "Licker (Red)",
    0x23: "Alligator", 0x24: "Licker (Grey)", 0x25: "Spider", 0x26: "Baby Spider", 0x27: "G Embryo",
    0x28: "G Adult", 0x29: "Cockroach", 0x2A: "Tyrant 1", 0x2B: "Tyrant 2", 0x2D: "Zombie Arms",
    0x2E: "Ivy", 0x2F: "Vines", 0x30: "Birkin 1", 0x31: "Birkin 2", 0x32: "Birkin 3", 0x33: "Birkin 4",
    0x34: "Birkin 5", 0x39: "Ivy (Purple)", 0x3A: "Giant Moth", 0x3B: "Maggots",
}
COLS = [(1, "Knife"), (2, "Handgun"), (5, "Magnum"), (6, "C.Magnum"), (7, "Shotgun"), (8, "C.Shotgun"),
        (9, "GL Expl"), (11, "GL Acid"), (10, "GL Flame"), (12, "Bowgun"), (14, "Spark"), (15, "SMG"),
        (16, "Flamethr"), (17, "Rocket"), (18, "Gatling")]


def main():
    data = open(sys.argv[1], "rb").read()
    print("| Enemigo | " + " | ".join(c for _, c in COLS) + " |")
    print("|---" * (len(COLS) + 1) + "|")
    for t, name in ENEMIES.items():
        ptr = struct.unpack_from("<I", data, off(0x543C48 + t * 4))[0]
        o = off(ptr)
        if o is None:
            continue
        cells = []
        for wid, _ in COLS:
            d0 = struct.unpack_from("<I", data, o + (wid - 1) * 20)[0]
            v = [(d0 >> (10 * k)) & 0x3FF for k in range(3)]
            cells.append(str(v[0]) if v[0] == v[1] == v[2] else "/".join(map(str, v)))
        print(f"| {name} | " + " | ".join(cells) + " |")


if __name__ == "__main__":
    main()
