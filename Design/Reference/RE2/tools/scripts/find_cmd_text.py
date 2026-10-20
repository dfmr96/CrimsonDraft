import re, os, sys
exe = open(os.path.join(sys.argv[1], "psx", "beta2", "PSX.EXE"), "rb").read(); base = 0x80010000 - 0x800
K = rb"[\xEA-\xEF]."
# 調べる = kanji + (hiragana) + る(0x7F); 組合せ = kanji kanji + せ(0x64); 使う = kanji + う(0x59)
for name, pat in (("shiraberu", K + rb"[\x57-\x9F]\x7F"), ("kumiawase", K + K + rb"\x64"), ("tsukau", K + rb"\x59")):
    hits = [m.start() for m in re.finditer(pat, exe, re.S)]
    print(f"{name}: {len(hits)} hits")
cands = []
for m in re.finditer(K + rb"[\x57-\x9F]\x7F", exe, re.S):          # 調べる
    win = exe[max(0, m.start() - 48): m.start() + 48]
    if re.search(K + K + rb"\x64", win, re.S) and re.search(K + K, win, re.S):
        cands.append(m.start())
for c in cands[:10]:
    a = c - 40
    print(f"VA 0x{base + a:X}: " + exe[a:a + 96].hex(" "))
