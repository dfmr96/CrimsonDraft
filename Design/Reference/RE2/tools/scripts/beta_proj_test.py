import sys, os
sys.path.insert(0, os.path.join(sys.argv[1], "re2tool"))
from overlay import BetaSource, Camera
from re2data import parse_room
src = BetaSource(); st = src.state(); print("state:", st)
room = parse_room(open(os.path.join(sys.argv[1], "psx", "beta2", "rdt", f"ROOM{st['room']}.RDT"), "rb").read())
cam = Camera(room["cameras"][st["cut"]], False, False)
p = cam.project(st["x"], st["y"] - 900, st["z"])
print("player (torso) projects to screen:", p, "-> on screen" if p and 0 <= p[0] <= 320 and 0 <= p[1] <= 240 else "-> OFF screen")
