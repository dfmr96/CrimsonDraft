"""Minimal RE2 (PC) RDT -> readable SCD disassembly + English messages.

Format knowledge taken from IntelOrca/biohazard-utils (Rdt2.cs, Bio2ConstantTable.cs, Msg.cs).
Usage: python -I re2scd.py <rdt_dir> <out_dir>
"""
import os
import struct
import sys

SIZES = [
    1, 2, 1, 4, 4, 2, 4, 4, 1, 4, 3, 1, 1, 6, 2, 4,
    2, 4, 2, 4, 6, 2, 2, 6, 2, 2, 2, 6, 1, 4, 1, 1,
    1, 4, 4, 6, 4, 3, 6, 4, 1, 2, 1, 6, 20, 38, 3, 4,
    1, 1, 8, 8, 4, 3, 12, 4, 3, 8, 16, 32, 2, 3, 6, 4,
    8, 10, 1, 4, 22, 5, 10, 2, 16, 8, 2, 3, 5, 22, 22, 4,
    4, 6, 6, 6, 22, 6, 4, 8, 4, 4, 2, 2, 3, 2, 2, 2,
    14, 4, 2, 1, 16, 2, 1, 28, 40, 30, 6, 4, 1, 4, 6, 2,
    1, 1, 16, 8, 4, 22, 3, 4, 6, 1, 16, 16, 6, 6, 6, 6,
    2, 3, 3, 1, 2, 6, 1, 1, 3, 1, 6, 6, 8, 24, 24,
]

SIGS = [
    "nop", "evt_end", "evt_next", "evt_chain:ugp", "evt_exec:ugp", "evt_kill", "if:uL", "else:u@",
    "endif", "sleep:uU", "sleeping:U", "wsleep", "wsleeping", "for:uLU", "next", "while:uL",
    "ewhile", "do:uL", "edwhile:'", "switch:uL", "case:uLU", "default", "eswitch", "goto:uuu~",
    "gosub:p", "return", "break", "for2", "break_point", "work_copy", "nop_1E", "nop_1F",
    "nop_20", "ck:fuu", "set:fuu", "cmp:uvcI", "save:vI", "copy:vv", "calc:uovI", "calc2:ovv",
    "sce_rnd", "cut_chg", "cut_old", "message_on:u3uuu", "aot_set:0sauuIIIIuuuuuu",
    "obj_model_set:1uuuuUUIIIIIIIIIIIIuuuu", "work_set:wu", "speed_set:uI",
    "add_speed", "add_aspeed", "pos_set:uIII", "dir_set:uIII", "member_set:mI", "member_set2:mv",
    "se_on:uIIIII", "sca_id_set", "flr_set", "dir_ck", "sce_espr_on:uUUUIIII",
    "door_aot_se:0sauuIIIIIIIIuuuuuuuutu", "cut_auto", "member_copy:vm", "member_cmp", "plc_motion",
    "plc_dest:uuuII", "plc_neck:uIIIuu", "plc_ret", "plc_flg:uU", "sce_em_set:u2euhu4uuIIIIUU",
    "col_chg_set", "aot_reset:0sauuuuuu", "aot_on:0", "super_set:uuuIIIIII", "super_reset:uIII",
    "plc_gun", "cut_replace", "sce_espr_kill", "", "item_aot_set:0sauuIIUUTUU1n", "sce_key_ck:uU",
    "sce_trg_ck:uU", "sce_bgm_control:xyzuu", "sce_espr_control", "sce_fade_set",
    "sce_espr3d_on:uUUUIIIIIII", "member_calc:oUI", "member_calc2:ouu", "sce_bgmtbl_set:uuuUU",
    "plc_rot:uU", "xa_on:uU", "weapon_chg", "plc_cnt", "sce_shake_on", "mizu_div_set",
    "keep_item_ck:t", "xa_vol",
    "kage_set:uuuuuIIII", "cut_be_set", "sce_item_lost:t", "plc_gun_eff", "sce_espr_on2",
    "sce_espr_kill2", "plc_stop", "aot_set_4p:usauuIIIIIIIIuuuuuu",
    "door_aot_set_4p:0sauuIIIIIIIIIIIIuuu5uuuutu", "item_aot_set_4p:0sauuIIIIIIIITUUuu",
    "light_pos_set:uuuI", "light_kido_set:uI", "rbj_reset", "sce_scr_move:uI", "parts_set:uuuI",
    "movie_on",
    "splc_ret", "splc_sce", "super_on", "mirror_set", "sce_fade_adjust", "sce_espr3d_on2",
    "sce_item_get", "sce_line_start", "sce_line_main", "sce_line_end", "sce_parts_bomb",
    "sce_parts_down", "light_color_set", "light_pos_set2:uuuI", "light_kido_set2:uuuU",
    "light_color_set2",
    "se_vol", "", "", "heal", "", "", "poison_ck", "poison_clr", "sce_item_ck_lost:tu",
    "heal_partner", "nop_8a", "nop_8b", "nop_8c", "", "",
]

ITEMS = (
    "None Knife HandgunLeon HandgunClaire CustomHandgun Magnum CustomMagnum Shotgun CustomShotgun "
    "GL_Explosive GL_Flame GL_Acid Bowgun ColtSAA Sparkshot SMG Flamethrower RocketLauncher GatlingGun "
    "Beretta HandgunAmmo ShotgunAmmo MagnumAmmo FuelTank ExplosiveRounds FlameRounds AcidRounds SMGAmmo "
    "SparkshotAmmo BowgunAmmo InkRibbon SmallKey HandgunParts MagnumParts ShotgunParts FAidSpray "
    "AntivirusBomb ChemicalACw32 HerbG HerbR HerbB HerbGG HerbGR HerbGB HerbGGG HerbGGB HerbGRB Lighter "
    "Lockpick PhotoSherry ValveHandle RedJewel RedCard BlueCard SerpentStone JaguarStone JaguarStoneL "
    "JaguarStoneR EagleStone BishopPlug RookPlug KnightPlug KingPlug WeaponBoxKey Detonator C4Explosive "
    "C4Detonator Crank FilmA FilmB FilmC UnicornMedal EagleMedal WolfMedal Cog ManholeOpener MainFuse "
    "FuseCase Vaccine VaccineCart FilmD VaccineBase GVirus SpecialKey JointPlugBlue JointPlugRed Cord "
    "PhotoAda CabinKey SpadeKey DiamondKey HeartKey ClubKey DownKey UpKey PowerRoomKey MODisk "
    "UmbrellaKeyCard MasterKey PlatformKey"
).split()

SCE = ["SCE_AUTO", "SCE_DOOR", "SCE_ITEM", "SCE_NORMAL", "SCE_MESSAGE", "SCE_EVENT", "SCE_FLAG_CHG",
       "SCE_WATER", "SCE_MOVE", "SCE_SAVE", "SCE_ITEMBOX", "SCE_DAMAGE", "SCE_STATUS", "SCE_HIKIDASHI",
       "SCE_WINDOWS"]
SAT = ["SAT_PL", "SAT_EM", "SAT_SPL", "SAT_OB", "SAT_MANUAL", "SAT_FRONT", "SAT_UNDER", "0x80"]
CMP = ["EQ", "GT", "GE", "LT", "LE", "NE"]
FG = ["FG_SYSTEM", "FG_STATUS", "FG_STOP", "FG_SCENARIO", "FG_COMMON", "FG_ROOM", "FG_ENEMY",
      "FG_ENEMY_2", "FG_ITEM", "FG_MAP", "FG_USE", "FG_MESSAGE", "FG_ROOM_ENEMY"]
VARS = {2: "V_USED_ITEM", 16: "V_TEMP", 24: "V_STAGE", 25: "V_ROOM", 26: "V_CUT", 27: "V_LAST_RDT",
        29: "V_RNG", 32: "V_NEXT_PLD"}

EN = " .___()__“”_0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz_________"


def item_name(v):
    if v == 0:
        return "UNLOCKED"
    if v == 254:
        return "UNLOCK"
    if v == 255:
        return "LOCKED"
    return ITEMS[v] if v < len(ITEMS) else f"ITEM_{v}"


def sat_name(v):
    if v == 0:
        return "SAT_AUTO"
    return "|".join(SAT[i] for i in range(8) if v & (1 << i))


def decode_msg(b):
    out = []
    i = 0
    while i < len(b):
        c = b[i]
        if c == 0xF9:
            i += 1
            out.append("{" if i < len(b) and b[i] == 1 else "}")
        elif c == 0xFA:
            i += 1
        elif c == 0xFB:
            i += 1
            out.append(f" [YES/NO]")
        elif c == 0xFC:
            out.append(" / ")
        elif c == 0xFD:
            i += 1
            out.append(" // ")
        elif c == 0xFE:
            break
        elif c < len(EN):
            out.append(EN[c])
        else:
            out.append(f"<{c:02X}>")
        i += 1
    return "".join(out)


def read_msgs(data, start, end):
    if start == 0 or start + 2 > len(data):
        return []
    first = struct.unpack_from("<H", data, start)[0]
    n = first // 2
    if n == 0 or start + first > end:
        return []
    offs = list(struct.unpack_from(f"<{n}H", data, start))
    msgs = []
    for i, o in enumerate(offs):
        nxt = offs[i + 1] if i + 1 < n else end - start
        msgs.append(decode_msg(data[start + o:start + nxt]))
    return msgs


def fmt_param(ch, v, msgs):
    if ch == "s":
        return SCE[v] if v < len(SCE) else str(v)
    if ch == "a":
        return sat_name(v)
    if ch in "tT":
        return item_name(v)
    if ch == "v":
        return VARS.get(v, f"V_{v:02X}")
    if ch == "c":
        return "CMP_" + CMP[v] if v < len(CMP) else str(v)
    if ch == "f":
        return FG[v] if v < len(FG) else f"FG_{v}"
    if ch == "p":
        return f"proc_{v:02X}"
    if ch == "3":
        txt = msgs[v] if v < len(msgs) else "?"
        return f"MSG_{v} \"{txt}\""
    if ch == "0":
        return f"AOT_{v}"
    return str(v)


def disasm(data, start, end, msgs):
    lines = []
    pos = start
    depth = 0
    while pos < end:
        op = data[pos]
        if op >= len(SIZES):
            lines.append(f"  ; bad opcode {op:02X} at {pos:X}")
            break
        size = SIZES[op]
        sig = SIGS[op] or f"op_{op:02X}"
        name, _, pchars = sig.partition(":")
        raw = data[pos + 1:pos + size]
        params = []
        p = 0
        for ch in pchars:
            if ch.isupper() or ch in "~@":
                if p + 2 > len(raw):
                    break
                v = struct.unpack_from("<H" if ch != "I" else "<h", raw, p)[0]
                p += 2
            else:
                if p >= len(raw):
                    break
                v = raw[p]
                p += 1
            params.append((ch, v))
        if name in ("endif", "ewhile", "next", "eswitch") or name == "else":
            depth = max(0, depth - 1)
        text = ", ".join(fmt_param(c, v, msgs) for c, v in params)
        # Annotate AOT payloads (message id / gosub target live in the data bytes).
        if name == "aot_set" and len(params) >= 15:
            sce = params[1][1]
            d = [v for _, v in params[9:15]]
            if sce == 4:
                text += f"   ; on check -> MSG_{d[0]} \"{msgs[d[0]] if d[0] < len(msgs) else '?'}\""
            elif sce in (5, 6) and d[2] == 0x18:
                text += f"   ; on trigger -> gosub proc_{d[3]:02X}"
        lines.append("  " + "  " * depth + f"{name}({text})")
        if name in ("if", "else", "while", "for", "switch", "do"):
            depth += 1
        if name == "evt_end" and depth == 0:
            pass
        pos += size
    return lines


def proc_list(data, start, end):
    if start == 0 or start + 2 > len(data):
        return []
    first = struct.unpack_from("<H", data, start)[0]
    n = first // 2
    offs = [start + o for o in struct.unpack_from(f"<{n}H", data, start)]
    res = []
    for i, o in enumerate(offs):
        e = offs[i + 1] if i + 1 < n else end
        res.append((o, e))
    return res


def process(path, out_path):
    data = open(path, "rb").read()
    offsets = list(struct.unpack_from("<23i", data, 8))
    bounds = sorted(set(o for o in offsets if o > 0) | {len(data)})

    def chunk_end(start):
        return next(b for b in bounds if b > start)

    msgs = []
    # 13 = Japanese table, 14 = English table.
    if offsets[14]:
        msgs = read_msgs(data, offsets[14], chunk_end(offsets[14]))
    out = [f"# {os.path.basename(path)}", "", "## Messages"]
    out += [f"  MSG_{i}: {m}" for i, m in enumerate(msgs)]
    for label, idx in (("init", 16), ("main", 17)):
        if not offsets[idx]:
            continue
        for pi, (s, e) in enumerate(proc_list(data, offsets[idx], chunk_end(offsets[idx]))):
            out.append("")
            out.append(f"## {label} proc_{pi:02X}")
            out += disasm(data, s, e, msgs)
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(out) + "\n")


def main():
    src, dst = sys.argv[1], sys.argv[2]
    os.makedirs(dst, exist_ok=True)
    count = 0
    for name in sorted(os.listdir(src)):
        if not name.upper().endswith(".RDT"):
            continue
        try:
            process(os.path.join(src, name), os.path.join(dst, name[:-4] + ".txt"))
            count += 1
        except Exception as ex:  # keep going: a single odd room shouldn't stop the batch
            print(f"{name}: {ex}")
    print(f"{count} rooms written to {dst}")


if __name__ == "__main__":
    main()
