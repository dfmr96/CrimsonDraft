import sys
from collections import Counter
from PIL import Image
im = Image.open(sys.argv[1]).convert("RGB")
# Region of the green item name "F. Aid Spray" in the 800x600 screenshot.
box = im.crop((70, 448, 340, 482))
c = Counter(box.getdata())
greens = [(col, n) for col, n in c.most_common(40) if col[1] > col[0] + 30 and col[1] > col[2] + 30]
print("most common green-ish colors (RGB, count):", greens[:8])
print("darkest colors:", sorted(c, key=sum)[:5])
