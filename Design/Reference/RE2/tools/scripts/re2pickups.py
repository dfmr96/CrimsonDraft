"""Pickup amounts per item, plus counts of save / item-box AOTs and related messages.

Usage: python -I re2pickups.py <disasm_dir>
"""
import os
import re
import sys
from collections import defaultdict

PICK = re.compile(r"item_aot_set\(AOT_\d+, SCE_ITEM, [^,]+, \d+, \d+, -?\d+, -?\d+, \d+, \d+, (\w+), (\d+)")
SCE = re.compile(r"(?:aot_set|aot_reset)\(AOT_\d+, (SCE_SAVE|SCE_ITEMBOX)")
MSG = re.compile(r"^\s+MSG_\d+: (.*(?:typewriter|ink ribbon|ribbon|box|herb|first aid|ammo|bullet|rounds|reload).*)$", re.I)


def main():
    src = sys.argv[1]
    amounts = defaultdict(list)
    sce_rooms = defaultdict(set)
    msgs = set()
    for name in sorted(os.listdir(src)):
        if not name.startswith("ROOM"):
            continue
        room = name[4:8]
        for line in open(os.path.join(src, name), encoding="utf-8"):
            if m := PICK.search(line):
                amounts[m.group(1)].append(int(m.group(2)))
            if m := SCE.search(line):
                sce_rooms[m.group(1)].add(room)
            if m := MSG.match(line):
                msgs.add(f"{room}: {m.group(1).strip()}")
    print("== pickups: item -> count, amounts ==")
    for item, vals in sorted(amounts.items()):
        print(f"{item:18s} n={len(vals):3d} amounts={sorted(set(vals))}")
    print("== save / item box AOTs ==")
    for k, rooms in sce_rooms.items():
        print(f"{k}: {len(rooms)} rooms -> {','.join(sorted(rooms))}")
    print("== related messages ==")
    for m in sorted(msgs):
        print(m)


if __name__ == "__main__":
    main()
