import struct, sys, re
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
exe = open(sys.argv[1], "rb").read(); base = 0x80010000 - 0x800
NAMES = ["M_POINTER","M_BE_FLAG","M_ROUTINE0","M_ROUTINE1","M_ROUTINE2","M_ROUTINE3","M_ID","M_TYPE","M_OBJ_NO",
 "M_SCE_NO","M_ATTRIBUTE","M_X_POS","M_Y_POS","M_Z_POS","M_X_DIR","M_Y_DIR","M_Z_DIR","M_FLOOR","M_STATUS_FLAG",
 "M_GROUND","M_X_DEST","M_Z_DEST","M_SCE_FLAG","M_SCE_FREE0","M_SCE_FREE1","M_SCE_FREE2","M_SCE_FREE3","M_X_SPEED0",
 "M_X_SPEED1","M_Y_SPEED","M_Z_SPEED","M_HOKAN_FLAG","M_OBJ_OFS_X","M_OBJ_OFS_Y","M_OBJ_OFS_Z","M_OBJ_W","M_OBJ_H",
 "M_OBJ_D","M_PARTS_POS_Y","M_SCA_OLD_X","M_SCA_OLD_Z","M_FREE0","M_FREE1","M_DAMAGE_CNT"]
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
for i in range(0x29):
    t = struct.unpack_from("<I", exe, 0x80010FDC - base + 4 * i)[0]
    ins = list(md.disasm(exe[t - base:t - base + 8], t))
    store = next((x for x in ins if x.mnemonic in ("sb", "sh", "sw")), None)
    desc = f"{store.mnemonic} {store.op_str}" if store else " ".join(f"{x.mnemonic} {x.op_str}" for x in ins)
    m = re.search(r"(0x[0-9a-f]+|\d+)?\(\$a0\)", desc)
    off = int(m.group(1), 0) if m and m.group(1) else 0
    print(f"{i:2d} {NAMES[i] if i < len(NAMES) else '?':15s} +0x{off:03X}  {desc}")
