"""Cross-reference every item mention in the disassembled RE2 rooms.

Usage: python -I re2items.py <disasm_dir>
"""
import os
import re
import sys
from collections import defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from re2scd import ITEMS  # noqa: E402

USED = re.compile(r"cmp\(0, V_USED_ITEM, CMP_(EQ|NE), (\d+)\)")
KEEP = re.compile(r"keep_item_ck\((\w+)\)")
LOST = re.compile(r"sce_item(?:_ck)?_lost\((\w+)")
DOOR = re.compile(r"door_aot_se\(.*, (\w+), \d+\)$")
PICK = re.compile(r"item_aot_set\(AOT_\d+, SCE_ITEM, [^,]+, \d+, \d+, -?\d+, -?\d+, \d+, \d+, (\w+), (\d+)")
YESNO = re.compile(r"message_on\(.*\[YES/NO\]")


def main():
    src = sys.argv[1]
    refs = defaultdict(lambda: defaultdict(set))  # item -> kind -> rooms
    for name in sorted(os.listdir(src)):
        if not name.startswith("ROOM"):
            continue
        room = name[4:8]
        lines = open(os.path.join(src, name), encoding="utf-8").read().splitlines()
        for i, line in enumerate(lines):
            if m := USED.search(line):
                v = int(m.group(2))
                refs[ITEMS[v] if v < len(ITEMS) else f"ITEM_{v}"]["use_from_inventory"].add(room)
            if m := KEEP.search(line):
                window = "\n".join(lines[i:i + 3])
                kind = "detect_and_prompt" if YESNO.search(window) else "detect"
                refs[m.group(1)][kind].add(room)
            if m := LOST.search(line):
                refs[m.group(1)]["consumed"].add(room)
            if m := DOOR.search(line):
                if m.group(1) not in ("UNLOCKED", "LOCKED", "UNLOCK"):
                    refs[m.group(1)]["door_key"].add(room)
            if m := PICK.search(line):
                refs[m.group(1)]["pickup"].add(room)

    for idx, item in enumerate(ITEMS):
        if idx < 31 and item not in refs:
            continue
        kinds = refs.get(item, {})
        parts = [f"{k}={','.join(sorted(r))}" for k, r in sorted(kinds.items())]
        print(f"{idx:3d} {item:18s} " + (" | ".join(parts) if parts else "(no room-script reference)"))
    for item in sorted(k for k in refs if k.startswith("ITEM_")):
        parts = [f"{k}={','.join(sorted(r))}" for k, r in sorted(refs[item].items())]
        print(f"    {item:18s} " + " | ".join(parts))


if __name__ == "__main__":
    main()
