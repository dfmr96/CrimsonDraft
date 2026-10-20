"""In-game overlay for RE2 (GOG, LeonU.exe): draws camera switch zones, the active camera's
area and AOTs on top of the game window, projected with the game's own camera.

Read-only: it only reads game memory (see live.py) and never modifies the game.
Usage: python -I overlay.py            (needs data.json next to this file; run build.py first)

Hotkeys (global, work while the game has focus):
  F5 mirror X   F6 mirror Y   F7 AOTs on/off   F8 switch zones on/off
  F9 overlay on/off   F10 4:3 fit / stretch   F11 quit
"""
import ctypes
import ctypes.wintypes as wt
import json
import math
import os
import sys
import tkinter as tk

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from live import GAME  # noqa: E402

try:
    ctypes.windll.shcore.SetProcessDpiAwareness(2)  # physical pixels, so Tk matches window rects
except Exception:
    pass

user32 = ctypes.windll.user32
KEY = {"F5": 0x74, "F6": 0x75, "F7": 0x76, "F8": 0x77, "F9": 0x78, "F10": 0x79, "F11": 0x7A,
       "NUM0": 0x60, "NUM1": 0x61, "NUM2": 0x62, "NUM5": 0x65, "NUM7": 0x67}
# Zombie perception (LeonU.exe 0x44A1C0): turn if player within 5000 and behind it; grab chance within
# 3000 in a ~70 degree cone or 2000 in a 90 degree cone (cones in 4096 = 360 units: 0x320, 0x400).
ZOMBIE_RULES = [(5000, None, "#5aa0ff", "girarse"), (3000, 70.3, "#ffb020", "agarre 50%"),
                (2000, 90.0, "#ff4757", "agarre 50%")]
DIR_MODES = ["sin/cos", "sin/-cos", "-sin/cos", "cos/sin"]
SCE_COLORS = {"DOOR": "#ff9f43", "ITEM": "#2ed573", "MESSAGE": "#a4b0be", "EVENT": "#b06cff",
              "FLAG_CHG": "#ffd32a", "SAVE": "#18dcff", "ITEMBOX": "#1abc9c", "DAMAGE": "#ff4757",
              "WATER": "#3d7eff", "NORMAL": "#747d8c"}
KEY_COLOR = "#ff00ff"  # made fully transparent
SCREEN_W, SCREEN_H = 320, 240


def hue(n):
    import colorsys
    r, g, b = colorsys.hls_to_rgb(((n * 47) % 360) / 360, 0.58, 0.75)
    return "#%02x%02x%02x" % (int(r * 255), int(g * 255), int(b * 255))


def inside(x, z, quad):
    sign = 0
    for k in range(4):
        (x1, z1), (x2, z2) = quad[k], quad[(k + 1) % 4]
        c = (x2 - x1) * (z - z1) - (z2 - z1) * (x - x1)
        if c:
            s = 1 if c > 0 else -1
            if not sign:
                sign = s
            elif s != sign:
                return False
    return True


def game_window(pid):
    """Largest visible top-level window of the game process, as client rect in screen coords."""
    best = []

    @ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM)
    def cb(hwnd, _):
        p = wt.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(p))
        if p.value == pid and user32.IsWindowVisible(hwnd):
            rc = wt.RECT()
            user32.GetClientRect(hwnd, ctypes.byref(rc))
            if rc.right * rc.bottom > 0:
                pt = wt.POINT(0, 0)
                user32.ClientToScreen(hwnd, ctypes.byref(pt))
                best.append((rc.right * rc.bottom, pt.x, pt.y, rc.right, rc.bottom))
        return True

    user32.EnumWindows(cb, 0)
    return max(best)[1:] if best else None


class Camera:
    """RE2 camera: LookAt(from, to) and projection distance H on a 320x240 screen (y down)."""

    def __init__(self, cam, mirror_x, mirror_y):
        self.eye = cam["from"]
        f = [cam["to"][i] - cam["from"][i] for i in range(3)]
        n = math.sqrt(sum(v * v for v in f)) or 1
        self.f = [v / n for v in f]
        down = (0.0, 1.0, 0.0)  # world y grows downwards in RE (floors are at y = 0, -1800, ...)
        r = [down[1] * self.f[2] - down[2] * self.f[1],
             down[2] * self.f[0] - down[0] * self.f[2],
             down[0] * self.f[1] - down[1] * self.f[0]]
        n = math.sqrt(sum(v * v for v in r)) or 1
        self.r = [v / n for v in r]
        self.u = [self.f[1] * self.r[2] - self.f[2] * self.r[1],
                  self.f[2] * self.r[0] - self.f[0] * self.r[2],
                  self.f[0] * self.r[1] - self.f[1] * self.r[0]]
        self.h = cam["h"]
        self.mx, self.my = (-1 if mirror_x else 1), (-1 if mirror_y else 1)

    def project(self, x, y, z):
        d = (x - self.eye[0], y - self.eye[1], z - self.eye[2])
        cz = sum(d[i] * self.f[i] for i in range(3))
        if cz < 50:
            return None
        cx = sum(d[i] * self.r[i] for i in range(3)) * self.mx
        cy = sum(d[i] * self.u[i] for i in range(3)) * self.my
        return SCREEN_W / 2 + self.h * cx / cz, SCREEN_H / 2 + self.h * cy / cz


class BetaSource:
    """Biohazard 2 Beta 2 running in DuckStation, read through its GDB server (read-only).
    Addresses come from the Beta's own MAIN.SYM / disassembly (Cut_chg_main, Debug_menu)."""

    STAGE_ROOM_CUT = 0x800C6F08   # u16 stage, u16 room, u16 cut
    PLAYER_PTR = 0x800C2744       # -> player work (Work_set kind 1)
    ENEMY_PTRS = 0x800C274C       # 16 pointers (Work_set kind 3); unused slots -> EMPTY_ENEMY
    EMPTY_ENEMY = 0x800C6B7C

    def __init__(self):
        self.gdb, self.pid = None, None

    def read(self, addr, length):
        """Shared read for the memory viewer (DuckStation's GDB server takes a single client)."""
        from gdbmem import GdbMem
        if self.gdb is None:
            self.gdb = GdbMem()
        try:
            return self.gdb.read(addr, length)
        except Exception:
            self.gdb = None
            raise

    def state(self):
        import struct
        from gdbmem import GdbMem
        if self.pid is None:
            self.pid = _find_pid("duckstation")
            if self.pid is None:
                return {"connected": False, "error": "DuckStation no está abierto"}
        try:
            if self.gdb is None:
                self.gdb = GdbMem()
            stage, room, cut = struct.unpack("<3H", self.gdb.read(self.STAGE_ROOM_CUT, 6))
            player = read_entity(self.gdb, struct.unpack("<I", self.gdb.read(self.PLAYER_PTR, 4))[0])
            enemies = []
            for i, p in enumerate(struct.unpack("<16I", self.gdb.read(self.ENEMY_PTRS, 64))):
                if p == self.EMPTY_ENEMY or not 0x80000000 <= p < 0x80200000:
                    continue
                e = read_entity(self.gdb, p)
                if e["active"]:
                    e["slot"] = i
                    enemies.append(e)
        except Exception as ex:
            self.gdb = None
            return {"connected": False, "error": f"GDB de DuckStation no disponible ({ex})"}
        x, y, z = player["pos"]
        return {"connected": True, "room": f"{stage + 1:X}{room:02X}0", "cut": cut, "x": x, "y": y, "z": z,
                "player": player, "enemies": enemies}


ENEMY_NAMES = {
    0x10: "Zombie (Cop)", 0x11: "Zombie (Brad)", 0x12: "Zombie (Guy1)", 0x13: "Zombie (Girl)",
    0x15: "Zombie (TestSubject)", 0x16: "Zombie (Scientist)", 0x17: "Zombie (Naked)", 0x18: "Zombie (Guy2)",
    0x1E: "Zombie (Guy3)", 0x1F: "Zombie (Random)", 0x20: "Zombie Dog", 0x21: "Crow", 0x22: "Licker (Red)",
    0x23: "Alligator", 0x24: "Licker (Grey)", 0x25: "Spider", 0x26: "Baby Spider", 0x27: "G Embryo",
    0x28: "G Adult", 0x29: "Cockroach", 0x2A: "Tyrant 1", 0x2B: "Tyrant 2", 0x2D: "Zombie Arms", 0x2E: "Ivy",
    0x2F: "Vines", 0x30: "Birkin 1", 0x31: "Birkin 2", 0x32: "Birkin 3", 0x33: "Birkin 4", 0x34: "Birkin 5",
    0x39: "Ivy (Purple)", 0x3A: "Giant Moth", 0x3B: "Maggots",
}
ZOMBIE_STATES = {0: "aparecer", 1: "comportamiento", 2: "herido", 3: "morir"}


def read_entity(gdb, ptr):
    """Entity work layout (same in PC and Beta): offsets from the Member_set table."""
    import struct
    b = gdb.read(ptr, 0x200)
    return {
        "ptr": ptr, "active": bool(b[0] & 1), "state": b[4], "sub": b[5], "id": b[8],
        "pattern": struct.unpack_from("<H", b, 0x10E)[0] & 0x3F,
        "pos": struct.unpack_from("<3i", b, 0x38),
        "ydir": struct.unpack_from("<h", b, 0x76)[0] & 0xFFF,      # 4096 = 360 degrees
        "floor": b[0x106],
        "hp": struct.unpack_from("<h", b, 0x156)[0], "hp_max": struct.unpack_from("<h", b, 0x162)[0],
        "dist": struct.unpack_from("<i", b, 0x1F0)[0],
    }


def _find_pid(fragment):
    import ctypes.wintypes as w
    from live import PROCESSENTRY32W, k32, TH32CS_SNAPPROCESS
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
    entry = PROCESSENTRY32W()
    entry.dwSize = ctypes.sizeof(entry)
    try:
        ok = k32.Process32FirstW(snap, ctypes.byref(entry))
        while ok:
            if fragment in entry.szExeFile.lower():
                return entry.th32ProcessID
            ok = k32.Process32NextW(snap, ctypes.byref(entry))
    finally:
        k32.CloseHandle(snap)
    return None


class PcSource:
    """GOG PC version (LeonU.exe), read through ReadProcessMemory (see live.py)."""

    def state(self):
        return GAME.state()

    @property
    def pid(self):
        return GAME.pid


class Overlay:
    def __init__(self, rooms, source):
        self.data = rooms
        self.source = source
        self.opts = {"mx": False, "my": False, "aots": True, "zones": True, "on": True, "fit": True, "ai": True}
        self.dir_mode = 0
        self.ruler_a = self.ruler_b = None
        self.last_state = None
        self.keys = {}
        self.root = tk.Tk()
        self.root.overrideredirect(True)
        self.root.attributes("-topmost", True)
        self.root.attributes("-transparentcolor", KEY_COLOR)
        self.root.configure(bg=KEY_COLOR)
        self.canvas = tk.Canvas(self.root, bg=KEY_COLOR, highlightthickness=0)
        self.canvas.pack(fill="both", expand=True)
        self.root.geometry("400x120+40+40")
        self.root.update_idletasks()
        self._click_through()
        self.root.after(50, self.tick)

    def _click_through(self):
        hwnd = user32.GetParent(self.root.winfo_id()) or self.root.winfo_id()
        GWL_EXSTYLE, WS_EX_LAYERED, WS_EX_TRANSPARENT, WS_EX_TOOLWINDOW = -20, 0x80000, 0x20, 0x80
        style = user32.GetWindowLongW(hwnd, GWL_EXSTYLE)
        user32.SetWindowLongW(hwnd, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW)

    def _pressed(self, name):
        down = bool(user32.GetAsyncKeyState(KEY[name]) & 0x8000)
        was = self.keys.get(name, False)
        self.keys[name] = down
        return down and not was

    def _hotkeys(self):
        for name, opt in (("F5", "mx"), ("F6", "my"), ("F7", "aots"), ("F8", "zones"), ("F9", "on"), ("F10", "fit"),
                          ("NUM5", "ai")):
            if self._pressed(name):
                self.opts[opt] = not self.opts[opt]
        s = self.last_state
        here = (s["x"], s["y"], s["z"]) if s and s.get("connected") else None
        if self._pressed("NUM1") and here:
            self.ruler_a, self.ruler_b = here, None
        if self._pressed("NUM2") and here and self.ruler_a:
            self.ruler_b = here
        if self._pressed("NUM0"):
            self.ruler_a = self.ruler_b = None
        if self._pressed("NUM7"):
            self.dir_mode = (self.dir_mode + 1) % len(DIR_MODES)
        if self._pressed("F11"):
            self.root.destroy()
            return False
        return True

    def tick(self):
        if not self._hotkeys():
            return
        self.canvas.delete("all")
        s = self.source.state()
        self.last_state = s
        if not s.get("connected"):
            self.root.geometry("520x40+40+40")
            self._text(8, 8, f"RE2 overlay: {s.get('error')}", "#ffb3bc", anchor="nw")
            self.root.after(500, self.tick)
            return
        win = game_window(self.source.pid)
        if win:
            x, y, w, h = win
            self.root.geometry(f"{w}x{h}+{x}+{y}")
            self.draw(s, w, h)
        self.root.after(33, self.tick)

    def _text(self, x, y, txt, color, anchor="center", size=10):
        for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            self.canvas.create_text(x + dx, y + dy, text=txt, fill="#000000", anchor=anchor,
                                    font=("Segoe UI", size, "bold"))
        self.canvas.create_text(x, y, text=txt, fill=color, anchor=anchor, font=("Segoe UI", size, "bold"))

    def draw(self, s, w, h):
        o = self.opts
        status = (f"ROOM{s['room']} cam {s['cut']}  ({s['x']}, {s['y']}, {s['z']})  "
                  f"F5 espejoX:{'sí' if o['mx'] else 'no'}  F6 espejoY:{'sí' if o['my'] else 'no'}  "
                  f"F7 AOT  F8 zonas  F9 ocultar  F10 {'4:3' if o['fit'] else 'estirar'}  F11 salir")
        self._text(6, 6, status, "#ffffff", anchor="nw", size=9)
        room = self.data.get(s["room"])
        if not o["on"] or not room or s["cut"] >= len(room["cameras"]):
            return
        cam = Camera(room["cameras"][s["cut"]], o["mx"], o["my"])
        if o["fit"]:
            sc = min(w / SCREEN_W, h / SCREEN_H)
            ox, oy = (w - SCREEN_W * sc) / 2, (h - SCREEN_H * sc) / 2
            sx_scale, sy_scale = sc, sc
        else:
            ox = oy = 0
            sx_scale, sy_scale = w / SCREEN_W, h / SCREEN_H
        floor_y = s["y"]  # zones and AOTs are flat: draw them at the player's feet height

        def poly(quad):
            out = []
            for qx, qz in quad:
                p = cam.project(qx, floor_y, qz)
                if p is None:
                    return None
                out += [ox + p[0] * sx_scale, oy + p[1] * sy_scale]
            return out

        def label(coords, txt, color):
            xs, ys = coords[0::2], coords[1::2]
            self._text(sum(xs) / 4, sum(ys) / 4, txt, color, size=9)

        if o["zones"]:
            for z in room["zones"]:
                if z["from"] != s["cut"]:
                    continue
                c = poly(z["quad"])
                if not c:
                    continue
                if z["area"]:
                    self.canvas.create_polygon(c, outline="#5aa0ff", fill="", width=2, dash=(6, 4))
                    continue
                hit = inside(s["x"], s["z"], z["quad"])
                col = hue(z["to"])
                self.canvas.create_polygon(c, outline="#ffffff" if hit else col, fill=col,
                                           stipple="gray25", width=4 if hit else 2)
                label(c, f"→ cam {z['to']}", "#ffffff")

        if o["aots"]:
            for a in room["aots"]:
                c = poly(a["quad"])
                if not c:
                    continue
                col = SCE_COLORS.get(a["sce"], "#ffffff")
                enemy_only = "EM" in a["sat"] and "PL" not in a["sat"]
                dash = (2, 4) if enemy_only else () if "MANUAL" in a["sat"] else (8, 5)
                self.canvas.create_polygon(c, outline=col, fill="", width=2, dash=dash)
                txt = (f"puerta → {a['door']['room']} (cam {a['door']['camera']})" if a.get("door")
                       else a["item"]["name"] if a.get("item") else
                       f"usar {a['use_item']}" if a.get("use_item") else a["sce"].lower())
                label(c, txt, col)

        p = cam.project(s["x"], s["y"], s["z"])
        if p:
            px, py = ox + p[0] * sx_scale, oy + p[1] * sy_scale
            self.canvas.create_oval(px - 6, py - 6, px + 6, py + 6, outline="#ffffff", fill="#ff4d5e", width=2)

        def to_screen(x, y, z):
            q = cam.project(x, y, z)
            return None if q is None else (ox + q[0] * sx_scale, oy + q[1] * sy_scale)

        def polyline(points3d, color, width=2, dash=()):
            seg = []
            for pt in points3d:
                q = to_screen(*pt)
                if q is None:
                    if len(seg) >= 4:
                        self.canvas.create_line(seg, fill=color, width=width, dash=dash)
                    seg = []
                else:
                    seg += q
            if len(seg) >= 4:
                self.canvas.create_line(seg, fill=color, width=width, dash=dash)

        self._draw_ruler(s, to_screen, polyline)
        if o["ai"]:
            self._draw_ai(s, to_screen, polyline)

    def _draw_ruler(self, s, to_screen, polyline):
        """MONOSASHI: distance between point A and point B (or the player, live)."""
        if not self.ruler_a:
            return
        a = self.ruler_a
        b = self.ruler_b or (s["x"], s["y"], s["z"])
        dist = math.dist((a[0], a[2]), (b[0], b[2]))
        height = b[1] - a[1]
        polyline([a, b], "#00e5ff", width=3)
        for pt, tag in ((a, "A"), (b, "B" if self.ruler_b else "")):
            q = to_screen(*pt)
            if q:
                self.canvas.create_oval(q[0] - 5, q[1] - 5, q[0] + 5, q[1] + 5, outline="#00e5ff", width=2)
                if tag:
                    self._text(q[0], q[1] - 12, tag, "#00e5ff", size=10)
        mid = to_screen((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2)
        txt = f"{dist:.0f} u  (~{dist / 1000:.2f} m)" + (f"  alto {height:+d}" if height else "")
        if mid:
            self._text(mid[0], mid[1] - 14, txt, "#00e5ff", size=11)
        self._text(6, 24, f"REGLA  {txt}   (Num1 A · Num2 B · Num0 borrar)", "#00e5ff", anchor="nw", size=9)

    def _facing(self, ydir):
        a = ydir / 4096 * 2 * math.pi
        sx, cx = math.sin(a), math.cos(a)
        return [(sx, cx), (sx, -cx), (-sx, cx), (cx, sx)][self.dir_mode]

    def _draw_ai(self, s, to_screen, polyline):
        """Enemy viewer: HP, state, facing and (zombies) the perception radii/cones from the exe."""
        enemies = s.get("enemies")
        if enemies is None:
            return
        px, pz = s["x"], s["z"]
        self._text(6, 42, f"IA: {len(enemies)} enemigos   Num5 ocultar · Num7 dirección [{DIR_MODES[self.dir_mode]}]",
                   "#ffd32a", anchor="nw", size=9)
        for e in enemies:
            x, y, z = e["pos"]
            fx, fz = self._facing(e["ydir"])
            is_zombie = 0x10 <= e["id"] <= 0x1F
            dist = math.dist((x, z), (px, pz))
            if is_zombie and e["state"] != 3:
                to_p = math.atan2(px - x, pz - z)
                face = math.atan2(fx, fz)
                off = abs((math.degrees(to_p - face) + 180) % 360 - 180)
                for radius, cone, color, label in ZOMBIE_RULES:
                    if cone is None:
                        ring = [(x + radius * math.sin(t / 32 * 2 * math.pi), y, z + radius * math.cos(t / 32 * 2 * math.pi))
                                for t in range(33)]
                        polyline(ring, color, width=1, dash=(4, 4))
                    else:
                        half = math.radians(cone / 2)
                        arc = [(x, y, z)]
                        for k in range(17):
                            ang = face - half + 2 * half * k / 16
                            arc.append((x + radius * math.sin(ang), y, z + radius * math.cos(ang)))
                        arc.append((x, y, z))
                        hot = dist < radius and off < cone / 2
                        polyline(arc, "#ffffff" if hot else color, width=3 if hot else 2)
            tip = (x + fx * 900, y, z + fz * 900)
            polyline([(x, y, z), tip], "#ffd32a", width=3)
            q = to_screen(x, y, z)
            if q:
                self.canvas.create_oval(q[0] - 7, q[1] - 7, q[0] + 7, q[1] + 7, outline="#000000", fill="#ffd32a", width=2)
                name = ENEMY_NAMES.get(e["id"], f"EM {e['id']:02X}")
                st = ZOMBIE_STATES.get(e["state"], str(e["state"])) if is_zombie else str(e["state"])
                self._text(q[0], q[1] - 30, f"{name}  HP {e['hp']}", "#ffd32a", size=9)
                self._text(q[0], q[1] - 16, f"{st} · sub {e['sub']} · patrón {e['pattern']} · {dist:.0f} u",
                           "#ffffff", size=8)


def main():
    if "--beta" in sys.argv:
        from re2data import parse_room
        rdt_dir = os.path.join(HERE, "..", "psx", "beta2", "rdt")
        if not os.path.isdir(rdt_dir):
            sys.exit("Faltan las salas de la Beta 2 extraídas del disco (psx/beta2/rdt).")
        rooms = {}
        for name in os.listdir(rdt_dir):
            if name.upper().endswith(".RDT"):
                try:
                    rooms[name[4:8].upper()] = parse_room(open(os.path.join(rdt_dir, name), "rb").read())
                except Exception as ex:
                    print(f"{name}: {ex}")
        source = BetaSource()
        overlay = Overlay(rooms, source)
        if "--mem" in sys.argv:
            from memviewer import MemViewer
            MemViewer(tk.Toplevel(overlay.root), source.read)
        overlay.root.mainloop()
        return
    path = os.path.join(HERE, "data.json")
    if not os.path.exists(path):
        sys.exit('Falta data.json: ejecuta primero  python -I build.py "<carpeta de RE2>"')
    Overlay(json.load(open(path, encoding="utf-8"))["Pl0"]["rooms"], PcSource()).root.mainloop()


if __name__ == "__main__":
    main()
