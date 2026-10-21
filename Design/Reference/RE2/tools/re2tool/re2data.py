"""Parse RE2 (PC) room files: cameras (RID), camera switch zones (RVD), AOTs from the room
scripts (SCD) and English messages.

Format knowledge: IntelOrca/biohazard-utils (Rdt2.cs, Bio2ConstantTable.cs, Msg.cs,
DoorAotSeOpcode.cs) plus our own verification against LeonU.exe.
"""
import math
import struct

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

# Parameter signatures (one char per parameter; uppercase / ~ / @ = 16-bit, 'I' signed).
AOT_SIGS = {
    0x2C: ("aot_set", "0sauuIIIIuuuuuu"),
    0x3B: ("door_aot_se", "0sauuIIIIIIIIuuuuuuuutu"),
    0x4E: ("item_aot_set", "0sauuIIUUTUU1n"),
    0x67: ("aot_set_4p", "usauuIIIIIIIIuuuuuu"),
    0x68: ("door_aot_set_4p", "0sauuIIIIIIIIIIIIuuu5uuuutu"),
    0x69: ("item_aot_set_4p", "0sauuIIIIIIIITUUuu"),
}

SCE = ["AUTO", "DOOR", "ITEM", "NORMAL", "MESSAGE", "EVENT", "FLAG_CHG", "WATER", "MOVE", "SAVE",
       "ITEMBOX", "DAMAGE", "STATUS", "HIKIDASHI", "WINDOWS"]
SAT = ["PL", "EM", "SPL", "OB", "MANUAL", "FRONT", "UNDER", "QUAD"]

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
    "UmbrellaKeyCard MasterKey PlatformKey - - - - "
    "File:ChrisDiary File:MailToChris File:MemoToLeon File:PoliceMemorandum File:OperationReport1 "
    "File:MailToTheChief1 File:MailToTheChief2 File:SecretaryDiaryA File:SecretaryDiaryB "
    "File:OperationReport2 File:UserRegistration File:DevelopedFilmA File:DevelopedFilmB "
    "File:DevelopedFilmC File:PatrolReport File:WatchmanDiary File:ChiefDiary File:SewerManagerDiary "
    "File:SewerManagerFax File:DevelopedFilmD File:VaccineSynthesis File:LabSecurityManual "
    "File:PEpsilonReport"
).split()

EN = " .___()__“”_0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz_________"


def item_name(v):
    if v < len(ITEMS) and ITEMS[v] != "-":
        return ITEMS[v]
    return f"ITEM_{v}"


def key_name(v):
    return {0: "unlocked", 254: "unlock", 255: "locked"}.get(v, item_name(v))


def sat_flags(v):
    return [SAT[i] for i in range(8) if v & (1 << i)]


def decode_msg(b):
    out, i = [], 0
    while i < len(b):
        c = b[i]
        if c in (0xF9, 0xFA, 0xFD):
            i += 1
            if c == 0xFD:
                out.append(" / ")
        elif c == 0xFB:
            i += 1
            out.append(" [Yes/No]")
        elif c == 0xFC:
            out.append(" ")
        elif c == 0xFE:
            break
        elif c < len(EN):
            out.append(EN[c])
        i += 1
    return " ".join("".join(out).split())


def _params(sig, raw):
    vals, p = [], 0
    for ch in sig:
        if ch.isupper() or ch in "~@":
            fmt = "<h" if ch == "I" else "<H"
            vals.append(struct.unpack_from(fmt, raw, p)[0])
            p += 2
        else:
            vals.append(raw[p])
            p += 1
    return vals


class Rdt:
    def __init__(self, data):
        self.data = data
        self.offsets = list(struct.unpack_from("<23i", data, 8))
        bounds = sorted(set(o for o in self.offsets if o > 0) | {len(data)})
        self._bounds = bounds

    def _end(self, start):
        return next(b for b in self._bounds if b > start)

    def messages(self):
        start = self.offsets[14]
        if not start:
            return []
        end = self._end(start)
        first = struct.unpack_from("<H", self.data, start)[0]
        n = first // 2
        if n == 0 or start + first > end:
            return []
        offs = list(struct.unpack_from(f"<{n}H", self.data, start))
        return [decode_msg(self.data[start + o:start + (offs[i + 1] if i + 1 < n else end - start)])
                for i, o in enumerate(offs)]

    def cameras(self):
        rid = self.offsets[7]
        cams = []
        for i in range(self.data[1]):
            flag, lens, fx, fy, fz, tx, ty, tz, _ = struct.unpack_from("<HH6iI", self.data, rid + i * 32)
            h = max(1, lens >> 7)
            cams.append({
                "id": i, "flag": flag, "h": h,
                "fov_v": round(math.degrees(2 * math.atan(120 / h)), 1),
                "fov_h": round(math.degrees(2 * math.atan(160 / h)), 1),
                "from": [fx, fy, fz], "to": [tx, ty, tz],
            })
        return cams

    def zones(self):
        """RVD records. The first record of each source-camera group is the camera's own area."""
        off = self.offsets[8]
        out, seen, i = [], set(), 0
        while off and off + i * 20 + 20 <= len(self.data):
            rec = self.data[off + i * 20: off + i * 20 + 20]
            if struct.unpack_from("<I", rec)[0] == 0xFFFFFFFF:
                break
            frm = rec[2]
            p = struct.unpack_from("<8h", rec, 4)
            out.append({
                "enabled": rec[0], "floor": None if rec[1] == 255 else rec[1],
                "from": frm, "to": rec[3], "area": frm not in seen,
                "quad": [[p[k], p[k + 1]] for k in range(0, 8, 2)],
            })
            seen.add(frm)
            i += 1
        return out

    def _procedures(self, idx):
        start = self.offsets[idx]
        if not start:
            return []
        end = self._end(start)
        first = struct.unpack_from("<H", self.data, start)[0]
        n = first // 2
        offs = [start + o for o in struct.unpack_from(f"<{n}H", self.data, start)]
        return [(o, offs[i + 1] if i + 1 < n else end) for i, o in enumerate(offs)]

    def aots(self, msgs):
        out = []
        for label, idx in (("init", 16), ("main", 17)):
            for pi, (s, e) in enumerate(self._procedures(idx)):
                pos = s
                while pos < e:
                    op = self.data[pos]
                    if op >= len(SIZES):
                        break
                    size = SIZES[op]
                    if op in AOT_SIGS:
                        name, sig = AOT_SIGS[op]
                        v = _params(sig, self.data[pos + 1:pos + size])
                        out.append(self._aot(name, v, f"{label}:{pi:02X}", msgs))
                    pos += size
        return out

    @staticmethod
    def _aot(name, v, proc, msgs):
        quad = name.endswith("_4p")
        a = {"op": name, "proc": proc, "id": v[0], "sce": SCE[v[1]] if v[1] < len(SCE) else str(v[1]),
             "sat": sat_flags(v[2]), "floor": v[3]}
        if quad:
            a["quad"] = [[v[5 + k], v[6 + k]] for k in range(0, 8, 2)]
            rest = v[13:]
        else:
            x, z, w, d = v[5:9]
            a["quad"] = [[x, z], [x + w, z], [x + w, z + d], [x, z + d]]
            rest = v[9:]
        if name.startswith("door"):
            nx, ny, nz, nd = rest[0:4]
            stage, room, cam, nfloor = rest[4:8]
            key = rest[-2]
            a["door"] = {"room": f"{stage + 1:X}{room:02X}0", "camera": cam, "floor": nfloor,
                         "arrive": [nx, ny, nz], "dir": nd, "lock": key_name(key)}
        elif name.startswith("item"):
            a["item"] = {"name": item_name(rest[0]), "amount": rest[1], "flag": rest[2]}
        else:
            d = rest
            if a["sce"] == "MESSAGE":
                a["text"] = msgs[d[0]] if d[0] < len(msgs) else f"MSG_{d[0]}"
            elif a["sce"] == "EVENT" and len(d) > 3 and d[2] == 0x18:
                a["gosub"] = f"main:{d[3]:02X}"
            if a["sce"] == "FLAG_CHG" and d[0] == 10:
                a["use_item"] = item_name(d[2])
        return a


def parse_room(data):
    r = Rdt(data)
    msgs = r.messages()
    return {"cameras": r.cameras(), "zones": r.zones(), "aots": r.aots(msgs)}
