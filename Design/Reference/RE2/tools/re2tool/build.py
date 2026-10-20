"""Build data.json for the viewer from an RE2 (PC) install.

Usage: python -I build.py "<RE2 install dir>" [out_json]
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from re2data import parse_room  # noqa: E402

SCENARIOS = {"Pl0": ("Leon", ["Pl0/Rdu", "Pl0/RDU"]), "Pl1": ("Claire", ["Pl1/Rdu", "Pl1/RDU"])}


def build(game_dir):
    out = {}
    for key, (label, subdirs) in SCENARIOS.items():
        folder = next((os.path.join(game_dir, s) for s in subdirs if os.path.isdir(os.path.join(game_dir, s))), None)
        if folder is None:
            continue
        rooms = {}
        for name in sorted(os.listdir(folder)):
            if not name.upper().endswith(".RDT"):
                continue
            room_id = name[4:8].upper()
            try:
                rooms[room_id] = parse_room(open(os.path.join(folder, name), "rb").read())
            except Exception as ex:  # one malformed room must not stop the build
                print(f"{key}/{name}: {ex}")
        out[key] = {"label": label, "rooms": rooms}
        print(f"{label}: {len(rooms)} rooms")
    return out


def main():
    game_dir = sys.argv[1]
    dst = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "data.json")
    with open(dst, "w", encoding="utf-8") as f:
        json.dump(build(game_dir), f, separators=(",", ":"))
    print(f"wrote {dst} ({os.path.getsize(dst) // 1024} KB)")


if __name__ == "__main__":
    main()
