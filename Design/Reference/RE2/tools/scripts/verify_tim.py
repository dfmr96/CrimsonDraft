import sys, os
sys.path.insert(0, os.path.join(sys.argv[1], "scripts"))
from psx_extract import Disc
t = os.path.join(sys.argv[1], "psx", "beta2", "tim")
copy = Disc(sys.argv[3]).read(*Disc(sys.argv[3]).find("COMMON\\DATA\\ST_MOJI.TIM"))
orig = Disc(sys.argv[2]).read(*Disc(sys.argv[2]).find("COMMON\\DATA\\ST_MOJI.TIM"))
print("copy == English texture :", copy == open(os.path.join(t, "ST_MOJI_EN.TIM"), "rb").read())
print("copy != original texture:", copy != orig)
print("original ISO untouched  :", orig == open(os.path.join(t, "ST_MOJI.TIM"), "rb").read())
