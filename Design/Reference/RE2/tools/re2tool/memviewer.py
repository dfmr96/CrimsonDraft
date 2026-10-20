"""MEMORY VIEWER for Biohazard 2 Beta 2 in DuckStation (read-only, via GDB).

Variables are listed by their real names from the Beta's MAIN.SYM. Entities (player/enemies)
are decoded field by field using the Member_set offset table.

Standalone:     python -I memviewer.py
With overlay:   python -I overlay.py --beta --mem   (shares the single GDB connection)
"""
import os
import re
import struct
import tkinter as tk
from tkinter import ttk

HERE = os.path.dirname(os.path.abspath(__file__))
SYMBOLS = os.path.join(HERE, "..", "psx", "beta2", "symbols.txt")

# Entity fields: (name, offset, size) — from the Member_set jump table plus HP/distance.
ENTITY_FIELDS = [
    ("M_POINTER (bit0 = activo)", 0x000, 2), ("M_ROUTINE0 (estado)", 0x004, 1), ("M_ROUTINE1 (subestado)", 0x005, 1),
    ("M_ROUTINE2", 0x006, 1), ("M_ROUTINE3", 0x007, 1), ("M_ID (tipo de enemigo)", 0x008, 1),
    ("M_OBJ_NO", 0x00A, 1), ("M_SCE_NO", 0x00B, 1), ("M_ATTRIBUTE", 0x010, 4),
    ("M_X_POS", 0x038, 4), ("M_Y_POS", 0x03C, 4), ("M_Z_POS", 0x040, 4),
    ("M_X_DIR", 0x074, 2), ("M_Y_DIR (4096 = 360°)", 0x076, 2), ("M_Z_DIR", 0x078, 2),
    ("M_OBJ_OFS_X", 0x094, 2), ("M_OBJ_OFS_Z", 0x096, 2), ("M_OBJ_OFS_Y", 0x098, 2),
    ("M_OBJ_W", 0x09A, 2), ("M_OBJ_D", 0x09C, 2), ("M_OBJ_H", 0x09E, 2),
    ("M_FLOOR", 0x106, 1), ("M_TYPE (patrón IA & 0x3F)", 0x10E, 2), ("M_SCA_OLD_X", 0x118, 2), ("M_SCA_OLD_Z", 0x11A, 2),
    ("M_X_SPEED", 0x144, 2), ("M_Y_SPEED", 0x146, 2), ("M_Z_SPEED", 0x148, 2), ("M_HOKAN_FLAG", 0x14E, 1),
    ("M_STATUS_FLAG", 0x154, 2), ("VIDA", 0x156, 2), ("VIDA MÁX.", 0x162, 2),
    ("M_GROUND", 0x1C2, 2), ("M_X_DEST", 0x1C4, 2), ("M_Z_DEST", 0x1C6, 2), ("M_SCE_FLAG", 0x1CC, 2),
    ("M_SCE_FREE0", 0x1D4, 2), ("M_SCE_FREE1", 0x1D6, 2), ("M_SCE_FREE2", 0x1D8, 2), ("M_SCE_FREE3", 0x1DA, 2),
    ("M_PARTS_POS_Y", 0x1DE, 2), ("DISTANCIA AL JUGADOR", 0x1F0, 4),
]
PLAYER_PTR, ENEMY_PTRS, EMPTY_ENEMY = 0x800C2744, 0x800C274C, 0x800C6B7C
FIXED = [("Sala/cámara (etapa, sala, cámara)", 0x800C6F08, False), ("Flags de sistema", 0x800C250C, False),
         ("Estructura G", 0x800BEB60, False), ("Estado de juego (Game)", 0x800D1878, False)]


def load_symbols():
    funcs, syms, section = set(), [], None
    for line in open(SYMBOLS, encoding="utf-8"):
        line = line.rstrip("\n")
        if line.startswith("# "):
            section = line
            continue
        m = re.match(r"^([0-9A-F]{8}) (\S+)", line)
        if not m:
            continue
        addr = int(m.group(1), 16)
        if section == "# FUNCTIONS":
            funcs.add(addr)
        elif section == "# SYMBOLS" and 0x80000000 <= addr < 0x80200000 and addr not in funcs:
            syms.append((m.group(2), addr))
    return sorted(set(syms), key=lambda s: s[0].lower())


class MemViewer:
    def __init__(self, master, read):
        self.read = read
        self.addr, self.entity, self.prev = 0x800C6F08, False, None
        master.title("RE2 Beta 2 · Memory Viewer (solo lectura)")
        master.geometry("1100x640")
        self.master = master
        self.symbols = load_symbols()

        left = ttk.Frame(master, padding=6)
        left.pack(side="left", fill="y")
        ttk.Label(left, text="Buscar variable:").pack(anchor="w")
        self.query = tk.StringVar()
        self.query.trace_add("write", lambda *_: self.fill())
        ttk.Entry(left, textvariable=self.query, width=34).pack(fill="x")
        self.listbox = tk.Listbox(left, width=44, height=24, font=("Consolas", 9))
        self.listbox.pack(fill="y", expand=True, pady=4)
        self.listbox.bind("<<ListboxSelect>>", self.on_pick)
        quick = ttk.LabelFrame(left, text="Accesos rápidos", padding=4)
        quick.pack(fill="x")
        ttk.Button(quick, text="Jugador", command=lambda: self.goto_ptr(PLAYER_PTR)).pack(fill="x")
        row = ttk.Frame(quick)
        row.pack(fill="x")
        for i in range(4):
            ttk.Button(row, text=f"Enemigo {i}", width=9,
                       command=lambda i=i: self.goto_ptr(ENEMY_PTRS + 4 * i)).pack(side="left")
        for label, addr, ent in FIXED:
            ttk.Button(quick, text=label, command=lambda a=addr, e=ent: self.goto(a, e)).pack(fill="x")

        right = ttk.Frame(master, padding=6)
        right.pack(side="left", fill="both", expand=True)
        bar = ttk.Frame(right)
        bar.pack(fill="x")
        ttk.Label(bar, text="Dirección:").pack(side="left")
        self.addr_var = tk.StringVar(value=f"{self.addr:08X}")
        entry = ttk.Entry(bar, textvariable=self.addr_var, width=12)
        entry.pack(side="left")
        entry.bind("<Return>", lambda _: self.goto(int(self.addr_var.get(), 16), False))
        self.title = ttk.Label(bar, text="", font=("Segoe UI", 10, "bold"))
        self.title.pack(side="left", padx=10)
        self.status = ttk.Label(bar, text="", foreground="#a33")
        self.status.pack(side="right")
        self.hex = tk.Text(right, height=18, font=("Consolas", 10), bg="#16181d", fg="#d7dbe2")
        self.hex.pack(fill="x", pady=4)
        self.hex.tag_configure("changed", foreground="#ff5e6c")
        self.hex.tag_configure("addr", foreground="#6f7b8c")
        self.fields = tk.Text(right, font=("Consolas", 10), bg="#16181d", fg="#d7dbe2")
        self.fields.pack(fill="both", expand=True)
        self.fill()
        self.master.after(200, self.refresh)

    def fill(self):
        q = self.query.get().lower()
        self.listbox.delete(0, "end")
        self.visible = [s for s in self.symbols if q in s[0].lower()][:800]
        for name, addr in self.visible:
            self.listbox.insert("end", f"{addr:08X}  {name}")

    def on_pick(self, _):
        sel = self.listbox.curselection()
        if sel:
            self.goto(self.visible[sel[0]][1], False)

    def goto(self, addr, entity):
        self.addr, self.entity, self.prev = addr, entity, None
        self.addr_var.set(f"{addr:08X}")
        names = [n for n, a in self.symbols if a == addr]
        self.title.config(text=names[0] if names else "")

    def goto_ptr(self, ptr_addr):
        try:
            p = struct.unpack("<I", self.read(ptr_addr, 4))[0]
        except Exception as ex:
            self.status.config(text=str(ex))
            return
        if p == EMPTY_ENEMY:
            self.status.config(text="Hueco vacío: no hay enemigo en esa posición")
        self.goto(p, True)
        self.title.config(text=f"Entidad en {p:08X} (puntero en {ptr_addr:08X})")

    def refresh(self):
        try:
            data = self.read(self.addr, 0x200 if self.entity else 0x100)
            self.status.config(text="")
        except Exception as ex:
            self.status.config(text=f"Sin conexión: {ex}")
            self.master.after(1000, self.refresh)
            return
        self.hex.delete("1.0", "end")
        for row in range(0, 0x100, 16):
            self.hex.insert("end", f"{self.addr + row:08X}  ", "addr")
            for i in range(16):
                b = data[row + i]
                changed = self.prev is not None and self.prev[row + i] != b
                self.hex.insert("end", f"{b:02X} ", "changed" if changed else ())
            text = "".join(chr(c) if 32 <= c < 127 else "." for c in data[row:row + 16])
            self.hex.insert("end", f" {text}\n")
        self.fields.delete("1.0", "end")
        if self.entity:
            for name, off, size in ENTITY_FIELDS:
                fmt = {1: "<B", 2: "<h", 4: "<i"}[size]
                v = struct.unpack_from(fmt, data, off)[0]
                old = struct.unpack_from(fmt, self.prev, off)[0] if self.prev else v
                self.fields.insert("end", f"+0x{off:03X}  {name:30s} {v:>12}   0x{v & ((1 << (8 * size)) - 1):0{2 * size}X}\n",
                                   "changed" if v != old else ())
            self.fields.tag_configure("changed", foreground="#ff5e6c")
        else:
            w = struct.unpack_from("<4h", data, 0)
            self.fields.insert("end", f"Como int16: {w}\nComo int32: {struct.unpack_from('<2i', data, 0)}\n")
        self.prev = data
        self.master.after(200, self.refresh)


def main():
    from gdbmem import GdbMem
    gdb = GdbMem()
    root = tk.Tk()
    MemViewer(root, gdb.read)
    root.mainloop()


if __name__ == "__main__":
    import sys
    sys.path.insert(0, HERE)
    main()
