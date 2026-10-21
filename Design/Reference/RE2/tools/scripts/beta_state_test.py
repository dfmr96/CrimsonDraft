import sys, os
sys.path.insert(0, os.path.join(sys.argv[1], "re2tool"))
from overlay import BetaSource
st = BetaSource().state()
print({k: v for k, v in st.items() if k not in ("player", "enemies")})
print("player:", {k: st["player"][k] for k in ("hp", "hp_max", "floor", "ydir", "state", "sub")})
print("enemies:", [(hex(e["id"]), e["hp"], e["state"], e["pos"]) for e in st["enemies"]])
