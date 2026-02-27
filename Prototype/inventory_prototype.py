import pygame
import sys
import math

SCREEN_W, SCREEN_H = 850, 600

# ---------------------------------------------------------------------------
# Layout
# ---------------------------------------------------------------------------
NAV_H    = 26       # top navigation bar height
TABS     = ["INVENTARIO", "MAPA", "ARCHIVOS", "SALIR"]

NUM_ROWS = 4
ROW_H    = 140
ROW_GAP  = 3
ROW_STEP = ROW_H + ROW_GAP
START_Y  = 31       # NAV_H + 5; 31 + 4×140 + 3×3 = 600 ✓

PORT_X,  PORT_W  =   5,  88
ECG_X,   ECG_W   =  96, 110
SLOT_X,  SLOT_W  = 209, 182
GRID_SX          = 394
MINI_CELL        =  34
GRID_COLS = GRID_ROWS = 4
GRID_Y_OFF = (ROW_H - GRID_ROWS * MINI_CELL) // 2   # = 2

STRIP_X = GRID_SX + GRID_COLS * MINI_CELL + 8       # 538
STRIP_W = SCREEN_W - STRIP_X - 5                    # 307

SLOT_CELL    = MINI_CELL
HOLSTER_COLS = 2
SLING_COLS   = 4
SLOT_LABEL_H = 16
SLOT_ITEM_H  = SLOT_CELL
SLOT_BOX_H   = SLOT_LABEL_H + SLOT_ITEM_H + 4

# ---------------------------------------------------------------------------
# Input modes
# ---------------------------------------------------------------------------
MODE_NORMAL  = 0   # cursor jumps item-to-item
MODE_REORDER = 1   # item grabbed, cell-by-cell
MODE_MENU    = 2   # context menu open
MODE_RELOAD  = 3   # 9mm ammo type selection

# ---------------------------------------------------------------------------
# Colors
# ---------------------------------------------------------------------------
DARK_BG        = (18,  20,  18 )
PANEL_BG       = (24,  30,  24 )
PANEL_BORDER   = (52,  72,  52 )
ROW_ACTIVE_BG  = (26,  34,  26 )
ROW_INACTIVE   = (16,  16,  16 )
GRID_LINE      = (34,  44,  34 )
CELL_VALID     = (80,  220, 80,  50)
CELL_INVALID   = (220, 80,  80,  50)
CELL_CURSOR    = (255, 255, 100, 40)
TEXT_COLOR     = (200, 200, 200)
DIM_COLOR      = (90,  90,  90 )
EQUIP_BADGE    = (80,  220, 80 )
SELECT_OUTLINE = (255, 255, 100)
SELECT_ROW     = (60,  90,  60 )
ECG_COLOR      = (60,  220, 60 )
INACTIVE_TINT  = (50,  50,  50 )
MENU_BG        = (20,  30,  20 )
MENU_BORDER    = (100, 140, 100)
MENU_HL        = (40,  65,  40 )

# ---------------------------------------------------------------------------
# Item definitions
# ---------------------------------------------------------------------------
ITEM_DEFS = {
    "p229":    {"label":"P229 9mm",   "mini":"P229","size":(2,1),"category":"weapon",  "caliber":"9mm",  "color":(180,160,100)},
    "mp5":     {"label":"MP5 9mm",    "mini":"MP5", "size":(3,1),"category":"weapon",  "caliber":"9mm",  "color":(100,180,160)},
    "mk18":    {"label":"Mk18 5.56",  "mini":"MK18","size":(3,1),"category":"weapon",  "caliber":"5.56", "color":(100,160,220)},
    "m4":      {"label":"Benelli M4", "mini":"BM4", "size":(4,1),"category":"weapon",  "caliber":"12ga", "color":(220,120, 80)},
    "mag_9mm": {"label":"Carg. 9mm",  "mini":"9mm", "size":(1,2),"category":"ammo",   "caliber":"9mm",  "sub":"magazine","color":(200,200, 80)},
    "mag_556": {"label":"Carg. 5.56", "mini":"556", "size":(1,2),"category":"ammo",   "caliber":"5.56", "sub":"magazine","color":( 80,200,140)},
    "box_9mm": {"label":"Caja 9mm",   "mini":"x9",  "size":(1,1),"category":"ammo",   "caliber":"9mm",  "sub":"box",     "color":(220,200,100)},
    "box_12ga":{"label":"Caja 12ga",  "mini":"12g", "size":(1,1),"category":"ammo",   "caliber":"12ga", "sub":"box",     "color":(200,140, 80)},
    "box_556": {"label":"Caja 5.56",  "mini":"5.56","size":(2,2),"category":"ammo",   "caliber":"5.56", "sub":"box",     "color":(100,200,160)},
    "punal":   {"label":"Punal",      "mini":"NAV", "size":(2,1),"category":"tactical","color":(200, 80, 80)},
    "granada": {"label":"Granada",    "mini":"GRN", "size":(1,1),"category":"tactical","color":( 80,200, 80)},
    "queroseno":{"label":"Queroseno", "mini":"KERO","size":(2,1),"category":"tactical","color":(220,180, 60)},
    "encendedor":{"label":"Encendedor","mini":"ENC","size":(1,1),"category":"tactical","color":(220,140, 40)},
}

# ---------------------------------------------------------------------------
# Operadors
# ---------------------------------------------------------------------------
OPERADORS = [
    {"name":"ALPHA",   "active":True,  "bpm":72, "hp":100, "hp_max":100, "sil_color":( 85,100, 85),
     "inventory":[
        {"type_key":"p229",       "col":0,"row":0,"rotated":False,"equipped":True, "ammo_type":"RIP"},
        {"type_key":"punal",      "col":0,"row":1,"rotated":False,"equipped":True},
        {"type_key":"queroseno",  "col":0,"row":2,"rotated":False,"equipped":False},
        {"type_key":"mag_9mm",    "col":2,"row":0,"rotated":False,"equipped":False,"ammo_type":"RIP","loaded":13,"capacity":13},
        {"type_key":"box_9mm",    "col":3,"row":0,"rotated":False,"equipped":False},
        {"type_key":"encendedor", "col":3,"row":1,"rotated":False,"equipped":False},
        {"type_key":"granada",    "col":3,"row":2,"rotated":False,"equipped":False},
     ]},
    {"name":"BRAVO",   "active":True,  "bpm":68, "hp":100, "hp_max":100, "sil_color":( 75, 85,110),
     "inventory":[
        {"type_key":"mp5",     "col":0,"row":0,"rotated":False,"equipped":True},
        {"type_key":"mag_9mm", "col":3,"row":0,"rotated":False,"equipped":False,"ammo_type":"FMJ","loaded":30,"capacity":30},
        {"type_key":"box_9mm", "col":3,"row":2,"rotated":False,"equipped":False},
     ]},
    {"name":"CHARLIE", "active":True,  "bpm":80, "hp":100, "hp_max":100, "sil_color":(110, 85, 85),
     "inventory":[
        {"type_key":"m4",      "col":0,"row":0,"rotated":False,"equipped":True},
        {"type_key":"box_12ga","col":0,"row":1,"rotated":False,"equipped":False},
        {"type_key":"punal",   "col":1,"row":1,"rotated":False,"equipped":True},
     ]},
    {"name":"CIA",     "active":False, "bpm":65, "hp":100, "hp_max":100, "sil_color":(100, 95, 70),
     "inventory":[]},
]

# ---------------------------------------------------------------------------
# Map data  (x, y relative to content area — START_Y added when drawing)
# ---------------------------------------------------------------------------
_OY = START_Y + 22   # vertical origin for map rooms
# Rooms are close together (12-14 px gaps) — RE2 style floor plan

MAP_ROOMS = [
    # (x, y, w, h, label, state)   — gaps ~13 px H, ~26-30 px V
    ( 25, _OY+ 32, 130, 55, "Cabina del Capitán",  "explored"),   # 0
    (168, _OY+ 32, 140, 55, "Puente de Mando",      "current"),    # 1
    (321, _OY+ 32, 125, 55, "Sala de Radar",        "explored"),   # 2
    (459, _OY+ 32, 110, 55, "Armería",              "unexplored"), # 3
    ( 25, _OY+115, 105, 55, "Camarotes A",          "explored"),   # 4
    (143, _OY+115, 110, 55, "Camarotes B",          "explored"),   # 5
    (266, _OY+115, 110, 55, "Enfermería",           "explored"),   # 6
    (389, _OY+115, 125, 55, "Bodega de Armas",      "unexplored"), # 7
    ( 50, _OY+196, 148, 65, "Sala de Máquinas",     "explored"),   # 8
    (210, _OY+196, 158, 65, "Cubierta de Carga",    "unexplored"), # 9
    (380, _OY+196, 128, 65, "Lab. Zona C",          "unknown"),    # 10
    (185, _OY+291, 165, 55, "Reactor",              "unknown"),    # 11
]

MAP_CORRIDORS = [
    # (room_a, room_b, door_state)  door_state: "open" | "blocked"
    (0, 1, "open"),
    (1, 2, "open"),
    (2, 3, "blocked"),
    (0, 4, "open"),
    (1, 5, "open"),
    (2, 6, "open"),
    (3, 7, "blocked"),
    (4, 5, "open"),
    (5, 6, "open"),
    (6, 7, "open"),
    (4, 8, "open"),
    (6, 9, "open"),
    (7, 10, "blocked"),
    (8, 9, "open"),
    (9, 10, "blocked"),
    (8, 11, "blocked"),
    (9, 11, "blocked"),
]

# ---------------------------------------------------------------------------
# Files data
# ---------------------------------------------------------------------------
FILES = [
    {
        "title":    "INFORME OP. ALFA NOCTURNO",
        "category": "OPERACIONAL",
        "date":     "2024-03-14",
        "content": [
            "CLASIFICACIÓN: TOP SECRET",
            "",
            "Equipo desplegado: 02:00 hrs.",
            "Objetivo: extracción activo SIERRA.",
            "Estado: EN CURSO",
            "",
            "Actividad biológica anómala",
            "detectada en Cubierta C.",
            "Proceder con máxima precaución.",
        ]
    },
    {
        "title":    "PERFIL: KROKONIL V2.3",
        "category": "INTELIGENCIA",
        "date":     "2024-03-10",
        "content": [
            "Sustancia sintética experimental.",
            "",
            "Efectos primarios:",
            "  · Supresión del dolor (6-8h)",
            "  · Aumento de reflejos",
            "  · Distorsión cognitiva",
            "",
            "Efectos secundarios: NO DOCUMENT.",
            "PRECAUCIÓN: consecuencias de uso",
            "prolongado desconocidas.",
        ]
    },
    {
        "title":    "TRANSMISIÓN INTERCEPTADA #1",
        "category": "INTELIGENCIA",
        "date":     "2024-03-13",
        "content": [
            "...no responden al protocolo...",
            "...cubierta C completamente...",
            "...los que sobreviven no son...",
            "...repito, NO son los mismos...",
            "",
            "[SEÑAL INTERRUMPIDA]",
        ]
    },
    {
        "title":    "NOTA: CUARENTENA ZONA C",
        "category": "LOGÍSTICA",
        "date":     "2024-03-14",
        "content": [
            "Código de acceso Cubierta C:",
            "  > [REDACTADO]",
            "",
            "Código cambiado antes del incidente.",
            "Contactar con Alpha para",
            "procedimiento alternativo.",
        ]
    },
    {
        "title":    "PERFIL: DRA. ELENA VASQUEZ",
        "category": "PERSONAL",
        "date":     "2024-03-01",
        "content": [
            "Cargo: Investigadora Principal",
            "Div.: BioSíntesis / Farmacología",
            "",
            "Especialidad: neurológica y",
            "farmacología de combate.",
            "",
            "Estado: DESAPARECIDA",
            "Última ubicación: Laboratorio B2",
        ]
    },
]

# ---------------------------------------------------------------------------
# Item helpers
# ---------------------------------------------------------------------------

def get_item_size(item):
    w, h = ITEM_DEFS[item["type_key"]]["size"]
    return (h, w) if item["rotated"] else (w, h)

def get_item_cells(item):
    w, h = get_item_size(item)
    return [(item["col"]+dc, item["row"]+dr) for dr in range(h) for dc in range(w)]

def item_rect_in_row(item, op_idx):
    w, h = get_item_size(item)
    gy = row_y(op_idx) + GRID_Y_OFF
    return pygame.Rect(GRID_SX + item["col"]*MINI_CELL,
                       gy      + item["row"]*MINI_CELL,
                       w*MINI_CELL, h*MINI_CELL)

def can_place(inventory, item, col, row, exclude=None):
    test = dict(item, col=col, row=row)
    w, h = get_item_size(test)
    if col < 0 or row < 0 or col+w > GRID_COLS or row+h > GRID_ROWS:
        return False
    occupied = {cell for other in inventory if other is not exclude
                for cell in get_item_cells(other)}
    return all(cell not in occupied for cell in get_item_cells(test))

def row_y(op_idx):
    return START_Y + op_idx * ROW_STEP

def get_equipped_weapon(inventory, slot):
    for item in inventory:
        defn = ITEM_DEFS[item["type_key"]]
        if defn["category"] == "weapon" and item.get("equipped"):
            w = defn["size"][0]
            if slot == "holster" and w <= 2: return item
            if slot == "sling"   and w >= 3: return item
    return None

def next_active_row(current, delta):
    for _ in range(NUM_ROWS):
        current = (current + delta) % NUM_ROWS
        if OPERADORS[current]["active"]:
            return current
    return current

def first_item(op_idx):
    inv = OPERADORS[op_idx]["inventory"]
    if not inv:
        return None
    return min(inv, key=lambda it: (it["row"], it["col"]))

def nav_item(inventory, current, direction):
    """Return spatially nearest item in given direction, or None."""
    if not inventory:
        return None
    if current is None:
        return first_item_from(inventory)
    cw, ch = get_item_size(current)
    ccx = current["col"] + cw / 2
    ccy = current["row"] + ch / 2
    best, best_score = None, float("inf")
    for item in inventory:
        if item is current:
            continue
        iw, ih = get_item_size(item)
        icx = item["col"] + iw / 2
        icy = item["row"] + ih / 2
        dx, dy = icx - ccx, icy - ccy
        if   direction == "right" and dx >  0.3: score = dx  + abs(dy) * 3
        elif direction == "left"  and dx < -0.3: score = -dx + abs(dy) * 3
        elif direction == "down"  and dy >  0.3: score = dy  + abs(dx) * 3
        elif direction == "up"    and dy < -0.3: score = -dy + abs(dx) * 3
        else: continue
        if score < best_score:
            best_score, best = score, item
    return best

def first_item_from(inventory):
    if not inventory:
        return None
    return min(inventory, key=lambda it: (it["row"], it["col"]))

def effective_bpm(op):
    """BPM rises as HP falls (stress response)."""
    hp, hp_max = op["hp"], op["hp_max"]
    ratio = hp / hp_max if hp_max > 0 else 1
    return int(op["bpm"] + (1 - ratio) * 50)

def get_menu_options(item, inventory):
    defn = ITEM_DEFS[item["type_key"]]
    opts = []
    cat = defn["category"]
    if cat in ("weapon", "tactical"):
        opts.append("Desequipar" if item.get("equipped") else "Equipar")
    if defn.get("sub") == "magazine":
        cal = defn["caliber"]
        boxes = sum(1 for it in inventory
                    if ITEM_DEFS[it["type_key"]].get("sub") == "box"
                    and ITEM_DEFS[it["type_key"]]["caliber"] == cal)
        if boxes > 0:
            opts.append("Recargar")
    opts.append("Examinar")
    return opts

def count_boxes(inventory, caliber):
    return sum(1 for it in inventory
               if ITEM_DEFS[it["type_key"]].get("sub") == "box"
               and ITEM_DEFS[it["type_key"]]["caliber"] == caliber)

def consume_box(inventory, caliber):
    for i, it in enumerate(inventory):
        if (ITEM_DEFS[it["type_key"]].get("sub") == "box"
                and ITEM_DEFS[it["type_key"]]["caliber"] == caliber):
            inventory.pop(i)
            return True
    return False

# ---------------------------------------------------------------------------
# ECG
# ---------------------------------------------------------------------------

def ecg_sample(phase):
    if 0.10 < phase < 0.14:
        return -math.sin((phase-0.10)/0.04*math.pi)*0.20
    elif 0.14 < phase < 0.19:
        return  math.sin((phase-0.14)/0.05*math.pi)*1.00
    elif 0.19 < phase < 0.24:
        return -math.sin((phase-0.19)/0.05*math.pi)*0.25
    elif 0.32 < phase < 0.52:
        return  math.sin((phase-0.32)/0.20*math.pi)*0.28
    return 0.0

# ---------------------------------------------------------------------------
# Draw helpers
# ---------------------------------------------------------------------------

def draw_navbar(surface, active_tab, font):
    pygame.draw.rect(surface, (12, 18, 12), (0, 0, SCREEN_W, NAV_H))
    tab_w = SCREEN_W // len(TABS)
    for i, name in enumerate(TABS):
        tx      = i * tab_w
        sel     = (i == active_tab)
        is_exit = (i == len(TABS) - 1)
        if sel:
            bg = (55, 22, 22) if is_exit else (28, 46, 28)
            pygame.draw.rect(surface, bg, (tx, 0, tab_w, NAV_H - 1))
        col = (220, 80, 80) if is_exit else ((220, 220, 200) if sel else (75, 100, 75))
        lbl = font.render(name, True, col)
        surface.blit(lbl, (tx + (tab_w - lbl.get_width()) // 2,
                           (NAV_H - 1 - lbl.get_height()) // 2))
        if sel:
            underline = (180, 60, 60) if is_exit else (90, 200, 90)
            pygame.draw.line(surface, underline, (tx + 4, NAV_H - 2), (tx + tab_w - 4, NAV_H - 2), 2)
        if i > 0:
            pygame.draw.line(surface, (35, 50, 35), (tx, 4), (tx, NAV_H - 5))
    pygame.draw.line(surface, (45, 65, 45), (0, NAV_H - 1), (SCREEN_W, NAV_H - 1))

_STATE_COLS = {
    "current":    {"fill": (22,  68,  88),  "border": (68, 195, 220),  "text": (195, 245, 255)},
    "explored":   {"fill": (18,  38,  92),  "border": (52, 115, 195),  "text": (130, 180, 230)},
    "unexplored": {"fill": (12,  12,  24),  "border": (35,  35,  60),  "text": ( 48,  48,  80)},
    "unknown":    {"fill": (18,  10,  10),  "border": (52,  20,  20),  "text": ( 72,  35,  35)},
}

_DOOR_COLS = {
    "open":    {"fill": (155, 135, 18), "border": (218, 195, 55)},   # yellow
    "blocked": {"fill": (128,  18, 18), "border": (210,  45, 45)},   # red
}

MAP_BG  = (6, 6, 10)    # near-black for map background
MAP_DOT = (18, 20, 28)  # subtle grid dot color

def _door_rect(a, b):
    """Small door tab at the wall face between two rooms."""
    ax, ay, aw, ah = a[:4]
    bx, by, bw, bh = b[:4]
    a_cx, a_cy = ax + aw / 2, ay + ah / 2
    b_cx, b_cy = bx + bw / 2, by + bh / 2
    if abs(b_cx - a_cx) >= abs(b_cy - a_cy):           # horizontal
        door_x = ((ax + aw) + bx) // 2 if b_cx > a_cx else ((bx + bw) + ax) // 2
        door_y = (max(ay, by) + min(ay + ah, by + bh)) // 2
        return pygame.Rect(door_x - 4, door_y - 9, 8, 18)
    else:                                                # vertical
        door_y = ((ay + ah) + by) // 2 if b_cy > a_cy else ((by + bh) + ay) // 2
        door_x = (max(ax, bx) + min(ax + aw, bx + bw)) // 2
        return pygame.Rect(door_x - 9, door_y - 4, 18, 8)

def draw_map(surface, font_big, font_tiny):
    # Near-black background for the map area
    pygame.draw.rect(surface, MAP_BG, (0, START_Y, SCREEN_W, SCREEN_H - START_Y))
    # Dot grid (every 16 px)
    for gx in range(0, SCREEN_W, 16):
        for gy in range(START_Y, SCREEN_H, 16):
            surface.set_at((gx, gy), MAP_DOT)

    # Title
    title = font_big.render("MAPA — BUQUE SIERRA NEGRA", True, (55, 125, 170))
    surface.blit(title, (30, START_Y + 6))

    # Doors first (rooms drawn on top so tabs protrude from walls)
    for a_idx, b_idx, door_state in MAP_CORRIDORS:
        dr   = _door_rect(MAP_ROOMS[a_idx], MAP_ROOMS[b_idx])
        dcol = _DOOR_COLS[door_state]
        pygame.draw.rect(surface, dcol["fill"],   dr)
        pygame.draw.rect(surface, dcol["border"], dr, 2)

    # Rooms
    for rx, ry, rw, rh, name, state in MAP_ROOMS:
        cols = _STATE_COLS[state]
        pygame.draw.rect(surface, cols["fill"],   (rx, ry, rw, rh))
        pygame.draw.rect(surface, cols["border"], (rx, ry, rw, rh), 2)
        # Wrap label
        words = name.split()
        lines, line = [], ""
        for word in words:
            test = (line + " " + word).strip()
            if font_tiny.size(test)[0] < rw - 6:
                line = test
            else:
                if line: lines.append(line)
                line = word
        if line: lines.append(line)
        total_h = len(lines) * 13
        for i, ln in enumerate(lines):
            lbl = font_tiny.render(ln, True, cols["text"])
            surface.blit(lbl, (rx + (rw - lbl.get_width()) // 2,
                               ry + (rh - total_h) // 2 + i * 13))
        # Current-location blinking dot
        if state == "current":
            pygame.draw.circle(surface, (68, 195, 220), (rx + rw - 9, ry + 9), 4)
            pygame.draw.circle(surface, MAP_BG,          (rx + rw - 9, ry + 9), 2)

    # Legend (top-right)
    lx, ly = 685, START_Y + 6
    for label, state in [("ACTUAL","current"), ("EXPLORADA","explored"),
                          ("INEXPLORADA","unexplored"), ("DESCONOCIDA","unknown")]:
        cols = _STATE_COLS[state]
        pygame.draw.rect(surface, cols["fill"],   (lx, ly, 12, 10))
        pygame.draw.rect(surface, cols["border"], (lx, ly, 12, 10), 1)
        surface.blit(font_tiny.render(label, True, cols["text"]), (lx + 15, ly - 1))
        ly += 15
    ly += 4
    for label, door_state in [("ABIERTA","open"), ("BLOQUEADA","blocked")]:
        dcol = _DOOR_COLS[door_state]
        pygame.draw.rect(surface, dcol["fill"],   (lx + 2, ly, 8, 14))
        pygame.draw.rect(surface, dcol["border"], (lx + 2, ly, 8, 14), 1)
        surface.blit(font_tiny.render(label, True, dcol["border"]), (lx + 15, ly + 1))
        ly += 18

def draw_files(surface, file_idx, font, font_big, font_tiny):
    LIST_W   = 282
    CONTENT_X = LIST_W + 8

    # Title bar
    title = font_big.render("ARCHIVOS — OP. ALFA NOCTURNO", True, (80, 120, 80))
    surface.blit(title, (8, START_Y + 6))

    # File list
    ly = START_Y + 28
    for i, f in enumerate(FILES):
        sel = (i == file_idx)
        item_r = pygame.Rect(4, ly, LIST_W - 4, 50)
        pygame.draw.rect(surface, (24, 40, 24) if sel else (13, 18, 13), item_r)
        if sel:
            pygame.draw.rect(surface, (55, 95, 55), item_r, 1)
        tc = (215, 215, 175) if sel else (110, 140, 110)
        mc = (75, 110, 75)   if sel else (50,  70,  50)
        # Truncate title if needed
        t_str = f["title"]
        while font.size(t_str)[0] > LIST_W - 14 and len(t_str) > 4:
            t_str = t_str[:-1]
        if t_str != f["title"]: t_str = t_str[:-1] + "…"
        surface.blit(font.render(t_str,          True, tc), (10, ly + 4))
        surface.blit(font_tiny.render(f["category"], True, mc), (10, ly + 20))
        surface.blit(font_tiny.render(f["date"],     True, mc), (10, ly + 32))
        ly += 54

    # Vertical separator
    pygame.draw.line(surface, (42, 62, 42), (LIST_W, START_Y + 26), (LIST_W, 596), 1)

    # Content panel
    f = FILES[file_idx]
    cx, cy = CONTENT_X + 8, START_Y + 28
    title_lbl = font_big.render(f["title"], True, (195, 215, 155))
    surface.blit(title_lbl, (cx, cy)); cy += 22
    meta = f"{f['category']}  ·  {f['date']}"
    surface.blit(font_tiny.render(meta, True, (65, 95, 65)), (cx, cy)); cy += 16
    pygame.draw.line(surface, (42, 62, 42), (cx, cy), (SCREEN_W - 8, cy)); cy += 10
    for line in f["content"]:
        col = (155, 155, 125) if line else (0, 0, 0)
        surface.blit(font_tiny.render(line, True, col), (cx, cy))
        cy += 15
    # Nav hint
    surface.blit(font_tiny.render("W/S  ↑↓ : navegar", True, (45, 65, 45)),
                 (cx, SCREEN_H - 16))

def draw_exit_screen(surface, font_big, font_tiny):
    lbl = font_big.render("¿Salir del juego?", True, (220, 80, 80))
    surface.blit(lbl, (SCREEN_W // 2 - lbl.get_width() // 2, SCREEN_H // 2 - 20))
    hint = font_tiny.render("[Space / Enter]: confirmar    [Tab]: cancelar", True, (150, 60, 60))
    surface.blit(hint, (SCREEN_W // 2 - hint.get_width() // 2, SCREEN_H // 2 + 10))

def draw_portrait(surface, rect, op, font_tiny):
    active = op["active"]
    dead   = op["hp"] <= 0
    col    = op["sil_color"] if (active and not dead) else INACTIVE_TINT
    pygame.draw.rect(surface, PANEL_BG if active else ROW_INACTIVE, rect)
    pygame.draw.rect(surface, col, rect, 1)
    if not active or dead:
        lbl_text  = "KIA"       if dead   else "NO DISP."
        lbl_color = (180, 60, 60) if dead else DIM_COLOR
        lbl = font_tiny.render(lbl_text, True, lbl_color)
        surface.blit(lbl, (rect.centerx-lbl.get_width()//2, rect.centery-lbl.get_height()//2))
        if dead:
            # Still draw faded silhouette
            cx, hy = rect.centerx, rect.y + 22
            pygame.draw.ellipse(surface, INACTIVE_TINT, (cx-13, hy, 26, 30))
            pygame.draw.rect(surface,   INACTIVE_TINT, (cx-5,  hy+28, 10, 8))
            pygame.draw.rect(surface,   INACTIVE_TINT, (cx-25, hy+35, 50, 46))
        return
    cx, hy = rect.centerx, rect.y + 22
    pygame.draw.ellipse(surface, col, (cx-13, hy, 26, 30))
    pygame.draw.rect(surface,   col, (cx-5,  hy+28, 10, 8))
    pygame.draw.rect(surface,   col, (cx-25, hy+35, 50, 46))
    # HP bar
    hp, hp_max = op["hp"], op["hp_max"]
    ratio  = hp / hp_max if hp_max > 0 else 0
    bar_x  = rect.x + 4
    bar_y  = rect.bottom - 24
    bar_w  = rect.width - 8
    bar_h  = 6
    hp_col = (80, 200, 80) if ratio > 0.6 else (200, 200, 80) if ratio > 0.3 else (200, 80, 80)
    pygame.draw.rect(surface, (30, 30, 30), (bar_x, bar_y, bar_w, bar_h))
    if ratio > 0:
        pygame.draw.rect(surface, hp_col, (bar_x, bar_y, int(bar_w * ratio), bar_h))
    pygame.draw.rect(surface, (70, 70, 70), (bar_x, bar_y, bar_w, bar_h), 1)
    hp_lbl = font_tiny.render(f"{hp}", True, hp_col)
    surface.blit(hp_lbl, (rect.centerx - hp_lbl.get_width()//2, bar_y - 13))
    name = font_tiny.render(op["name"], True, (160,180,160))
    surface.blit(name, (rect.centerx-name.get_width()//2, rect.bottom-13))

def draw_ecg_row(surface, rect, time_ms, bpm, active, hp_ratio, font_tiny):
    pygame.draw.rect(surface, PANEL_BG if active else ROW_INACTIVE, rect)
    pygame.draw.rect(surface, PANEL_BORDER if active else DIM_COLOR, rect, 1)
    if not active:
        return
    # Color: green → yellow → red as HP falls
    if hp_ratio > 0.6:
        ecg_col = (60, 220, 60)
    elif hp_ratio > 0.3:
        t = (hp_ratio - 0.3) / 0.3          # 1→0 as ratio goes 0.6→0.3
        ecg_col = (int(60 + (1-t)*160), int(220 - (1-t)*60), 60)
    else:
        t = hp_ratio / 0.3                   # 1→0 as ratio goes 0.3→0
        ecg_col = (220, int(t * 160), 60)
    beat_ms = 60_000 / bpm
    wx, wy  = rect.x+4, rect.y+12
    ww, wh  = rect.width-8, rect.height-24
    # Amplitude shrinks with HP: full=0.42, near-dead≈0.12
    amp   = wh * (0.12 + 0.30 * hp_ratio)
    cy    = wy + wh // 2
    ms_pp = (beat_ms * 2.5) / ww
    pts = [(wx+i, int(cy - ecg_sample(((time_ms-(ww-i)*ms_pp) % beat_ms)/beat_ms)*amp))
           for i in range(ww)]
    if len(pts) > 1:
        pygame.draw.lines(surface, ecg_col, False, pts, 1)
    bpm_lbl = font_tiny.render(str(bpm), True, ecg_col)
    surface.blit(bpm_lbl, (rect.right-bpm_lbl.get_width()-4, rect.y+2))
    surface.blit(font_tiny.render("BPM", True, DIM_COLOR), (rect.right-30, rect.y+14))

def draw_equip_slots(surface, rect, inventory, active, font_tiny):
    pygame.draw.rect(surface, PANEL_BG if active else ROW_INACTIVE, rect)
    pygame.draw.rect(surface, PANEL_BORDER if active else DIM_COLOR, rect, 1)
    if not active:
        return
    total_h = SLOT_BOX_H*2+4
    top = rect.y + (rect.height-total_h)//2
    for i, (slot_name, n_cols) in enumerate([("HOLSTER", HOLSTER_COLS), ("SLING", SLING_COLS)]):
        bx, by, bw = rect.x, top+i*(SLOT_BOX_H+4), rect.width
        pygame.draw.rect(surface, (30,40,30), pygame.Rect(bx,by,bw,SLOT_BOX_H))
        pygame.draw.rect(surface, GRID_LINE,  pygame.Rect(bx,by,bw,SLOT_BOX_H), 1)
        surface.blit(font_tiny.render(slot_name, True, DIM_COLOR), (bx+4, by+2))
        slot_px_w = n_cols*SLOT_CELL
        sx = bx+(bw-slot_px_w)//2
        sy = by+SLOT_LABEL_H+2
        for c in range(n_cols):
            cr = pygame.Rect(sx+c*SLOT_CELL, sy, SLOT_CELL, SLOT_ITEM_H)
            pygame.draw.rect(surface, (25,35,25), cr)
            pygame.draw.rect(surface, (40,55,40), cr, 1)
        item = get_equipped_weapon(inventory, slot_name.lower())
        if item:
            defn  = ITEM_DEFS[item["type_key"]]
            iw    = defn["size"][0]
            ir    = pygame.Rect(sx, sy, iw*SLOT_CELL, SLOT_ITEM_H)
            s     = pygame.Surface((ir.width, ir.height), pygame.SRCALPHA)
            cr2,cg2,cb2 = defn["color"]
            s.fill((cr2,cg2,cb2,210))
            surface.blit(s, (ir.x, ir.y))
            pygame.draw.rect(surface, defn["color"], ir, 2)
            t = font_tiny.render(defn["mini"], True, (0,0,0))
            surface.blit(t, (ir.x+(ir.width-t.get_width())//2,
                              ir.y+(ir.height-t.get_height())//2))
            # ammo type badge on magazine slot
            if item.get("ammo_type") and defn.get("caliber") == "9mm":
                at = font_tiny.render(item["ammo_type"], True, (0,0,0))
                atw = at.get_width()+4
                pygame.draw.rect(surface, (180,80,80) if item["ammo_type"]=="RIP" else (180,180,80),
                                 (ir.x+2, ir.y+2, atw, at.get_height()+2))
                surface.blit(at, (ir.x+4, ir.y+3))

def draw_grid_row(surface, op_idx, is_selected, mode, reorder_item, reorder_op, reorder_col, reorder_row):
    gy  = row_y(op_idx) + GRID_Y_OFF
    gx  = GRID_SX
    bcol = SELECT_ROW if is_selected else PANEL_BORDER
    pygame.draw.rect(surface, bcol,
                     pygame.Rect(gx-1,gy-1,GRID_COLS*MINI_CELL+2,GRID_ROWS*MINI_CELL+2), 1)
    for c in range(GRID_COLS+1):
        pygame.draw.line(surface, GRID_LINE, (gx+c*MINI_CELL,gy), (gx+c*MINI_CELL,gy+GRID_ROWS*MINI_CELL))
    for r in range(GRID_ROWS+1):
        pygame.draw.line(surface, GRID_LINE, (gx,gy+r*MINI_CELL), (gx+GRID_COLS*MINI_CELL,gy+r*MINI_CELL))

    # Cursor highlight
    if mode == MODE_NORMAL and is_selected:
        # Highlight drawn by the item itself (SELECT_OUTLINE)
        pass
    elif mode == MODE_REORDER and reorder_op == op_idx:
        valid = can_place(OPERADORS[op_idx]["inventory"], reorder_item, reorder_col, reorder_row)
        iw, ih = get_item_size(reorder_item)
        ghost_r = pygame.Rect(gx+reorder_col*MINI_CELL,
                               gy+reorder_row*MINI_CELL,
                               iw*MINI_CELL, ih*MINI_CELL)
        color = CELL_VALID if valid else CELL_INVALID
        s = pygame.Surface((ghost_r.width, ghost_r.height), pygame.SRCALPHA)
        s.fill(color)
        surface.blit(s, (ghost_r.x, ghost_r.y))
        pygame.draw.rect(surface, color[:3], ghost_r, 2)

def draw_item_in_row(surface, item, op_idx, font_tiny, alpha=210, selected=False):
    defn = ITEM_DEFS[item["type_key"]]
    r    = item_rect_in_row(item, op_idx)
    surf = pygame.Surface((r.width, r.height), pygame.SRCALPHA)
    cr,cg,cb = defn["color"]
    surf.fill((cr,cg,cb,alpha))
    surface.blit(surf, (r.x, r.y))
    border = SELECT_OUTLINE if selected else defn["color"]
    pygame.draw.rect(surface, border, r, 2)
    lbl = font_tiny.render(defn["mini"], True, (0,0,0))
    surface.blit(lbl, (r.x+(r.width-lbl.get_width())//2, r.y+(r.height-lbl.get_height())//2))
    if item.get("equipped"):
        badge = font_tiny.render("E", True, (0,0,0))
        bw2,bh2 = badge.get_width()+3, badge.get_height()+2
        bx2,by2 = r.right-bw2-1, r.y+1
        pygame.draw.rect(surface, EQUIP_BADGE, (bx2,by2,bw2,bh2))
        surface.blit(badge, (bx2+1, by2+1))

def draw_strip(surface, mode, cursor_item, menu_options, menu_idx,
               reload_item, reload_op, reload_idx, font, font_big, font_tiny):
    """Right strip: shows mode status, item info, or context menu."""
    strip = pygame.Rect(STRIP_X, START_Y, STRIP_W,
                        NUM_ROWS*ROW_H+(NUM_ROWS-1)*ROW_GAP)
    pygame.draw.rect(surface, PANEL_BG, strip)
    pygame.draw.rect(surface, PANEL_BORDER, strip, 1)

    x, y = STRIP_X+8, START_Y+8

    # Mode label
    mode_labels = {MODE_NORMAL:"NORMAL", MODE_REORDER:"REORDENAR",
                   MODE_MENU:"MENU", MODE_RELOAD:"RECARGAR"}
    mode_colors = {MODE_NORMAL:(100,200,100), MODE_REORDER:(200,200,80),
                   MODE_MENU:(200,140,80), MODE_RELOAD:(80,180,220)}
    surface.blit(font_big.render(mode_labels[mode], True, mode_colors[mode]), (x, y))
    y += 24

    if mode == MODE_NORMAL:
        if cursor_item:
            defn = ITEM_DEFS[cursor_item["type_key"]]
            surface.blit(font_big.render(defn["label"], True, (255,255,100)), (x, y)); y += 20
            surface.blit(font_tiny.render(defn["category"], True, DIM_COLOR),  (x, y)); y += 22
            if cursor_item.get("ammo_type"):
                at_col = (220,80,80) if cursor_item["ammo_type"]=="RIP" else (200,200,80)
                surface.blit(font_tiny.render(f"Tipo: {cursor_item['ammo_type']}", True, at_col), (x, y)); y += 16
        y += 10
        hints = ["Flechas: navegar", "[Space]: menu", "[X]: reordenar",
                 "[C]: CIA on/off", "[F]: dano -10", "[G]: curar +10"]
        for h in hints:
            surface.blit(font_tiny.render(h, True, DIM_COLOR), (x, y)); y += 16

    elif mode == MODE_REORDER:
        surface.blit(font_tiny.render("Mover con flechas", True, DIM_COLOR), (x,y)); y += 16
        hints = ["[Y]: rotar", "[Space]: colocar", "[Esc]: cancelar", "", "Cruzar fila = transferir"]
        for h in hints:
            surface.blit(font_tiny.render(h, True, DIM_COLOR), (x,y)); y += 16

    elif mode == MODE_MENU:
        if cursor_item:
            defn = ITEM_DEFS[cursor_item["type_key"]]
            surface.blit(font_big.render(defn["label"], True, (255,255,100)), (x,y)); y += 24
        y += 4
        for i, opt in enumerate(menu_options):
            hl  = i == menu_idx
            bg  = MENU_HL if hl else MENU_BG
            col = SELECT_OUTLINE if hl else TEXT_COLOR
            opt_rect = pygame.Rect(x-4, y-2, STRIP_W-12, 22)
            pygame.draw.rect(surface, bg, opt_rect)
            if hl:
                pygame.draw.rect(surface, MENU_BORDER, opt_rect, 1)
            prefix = "> " if hl else "  "
            surface.blit(font.render(prefix+opt, True, col), (x, y)); y += 24
        y += 8
        surface.blit(font_tiny.render("[Space]: confirmar", True, DIM_COLOR), (x,y)); y += 16
        surface.blit(font_tiny.render("[Esc]: cerrar",      True, DIM_COLOR), (x,y))

    elif mode == MODE_RELOAD:
        defn = ITEM_DEFS[reload_item["type_key"]]
        caliber = defn["caliber"]
        inv = OPERADORS[reload_op]["inventory"]
        n_boxes = count_boxes(inv, caliber)
        surface.blit(font_big.render(f"Recargar {caliber}", True,(255,255,100)),(x,y)); y+=24
        surface.blit(font_tiny.render(f"Cajas disponibles: {n_boxes}", True, DIM_COLOR),(x,y)); y+=20
        y += 8
        options = [("RIP", (220,80,80)), ("FMJ", (200,200,80))]
        for i,(name,col2) in enumerate(options):
            hl = i == reload_idx
            bg = MENU_HL if hl else MENU_BG
            opt_rect = pygame.Rect(x-4, y-2, STRIP_W-12, 22)
            pygame.draw.rect(surface, bg, opt_rect)
            if hl: pygame.draw.rect(surface, MENU_BORDER, opt_rect, 1)
            prefix = "> " if hl else "  "
            surface.blit(font.render(prefix+name, True, col2 if hl else DIM_COLOR),(x,y)); y+=24
        y += 8
        surface.blit(font_tiny.render("[Space]: cargar",  True, DIM_COLOR),(x,y)); y+=16
        surface.blit(font_tiny.render("[Esc]: cancelar",  True, DIM_COLOR),(x,y))

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    pygame.init()
    screen    = pygame.display.set_mode((SCREEN_W, SCREEN_H))
    pygame.display.set_caption("Crimson Draft — Inventario de Party")
    clock     = pygame.time.Clock()
    font      = pygame.font.SysFont("Consolas", 13)
    font_big  = pygame.font.SysFont("Consolas", 15, bold=True)
    font_tiny = pygame.font.SysFont("Consolas", 11)

    active_tab  = 0
    mode        = MODE_NORMAL
    cursor_op   = 0
    cursor_item = first_item(0)

    # Reorder state
    reorder_item     = None
    reorder_orig     = (0, 0)
    reorder_orig_rot = False
    reorder_orig_op  = 0
    reorder_col      = 0
    reorder_row      = 0
    reorder_op       = 0

    # Menu state
    menu_options = []
    menu_idx     = 0

    # Reload state
    reload_item = None
    reload_op   = 0
    reload_idx  = 0   # 0=RIP, 1=FMJ

    # Files state
    file_idx = 0

    running = True
    while running:
        time_ms = pygame.time.get_ticks()

        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                running = False

            elif event.type == pygame.KEYDOWN:
                key = event.key

                # --- Tab: cycle navbar tabs ---
                if key == pygame.K_TAB:
                    active_tab = (active_tab + 1) % len(TABS)
                    continue

                # --- Non-inventory screens ---
                if active_tab != 0:
                    if active_tab == 2:  # ARCHIVOS
                        if key in (pygame.K_DOWN, pygame.K_s):
                            file_idx = (file_idx + 1) % len(FILES)
                        elif key in (pygame.K_UP, pygame.K_w):
                            file_idx = (file_idx - 1) % len(FILES)
                    elif active_tab == len(TABS) - 1:  # SALIR
                        if key in (pygame.K_SPACE, pygame.K_RETURN):
                            running = False
                    continue  # don't process inventory keys on other screens

                # --- ESC always cancels ---
                if key == pygame.K_ESCAPE:
                    if mode == MODE_MENU:
                        mode = MODE_NORMAL
                    elif mode == MODE_RELOAD:
                        mode = MODE_MENU
                    elif mode == MODE_REORDER:
                        # Restore item to original position
                        reorder_item["col"]     = reorder_orig[0]
                        reorder_item["row"]     = reorder_orig[1]
                        reorder_item["rotated"] = reorder_orig_rot
                        OPERADORS[reorder_orig_op]["inventory"].append(reorder_item)
                        cursor_item = reorder_item
                        cursor_op   = reorder_orig_op
                        reorder_item = None
                        mode = MODE_NORMAL
                    else:
                        running = False
                    continue

                # --- CIA toggle (always available) ---
                if key == pygame.K_c and mode == MODE_NORMAL:
                    OPERADORS[3]["active"] = not OPERADORS[3]["active"]
                    if not OPERADORS[3]["active"] and cursor_op == 3:
                        cursor_op   = next_active_row(3, -1)
                        cursor_item = first_item(cursor_op)
                    continue

                # --- MODE_NORMAL ---
                if mode == MODE_NORMAL:
                    inv = OPERADORS[cursor_op]["inventory"]

                    if key in (pygame.K_RIGHT, pygame.K_d):
                        nxt = nav_item(inv, cursor_item, "right")
                        if nxt: cursor_item = nxt

                    elif key in (pygame.K_LEFT, pygame.K_a):
                        nxt = nav_item(inv, cursor_item, "left")
                        if nxt: cursor_item = nxt

                    elif key in (pygame.K_DOWN, pygame.K_s):
                        nxt = nav_item(inv, cursor_item, "down")
                        if nxt:
                            cursor_item = nxt
                        else:
                            cursor_op   = next_active_row(cursor_op, 1)
                            cursor_item = first_item(cursor_op)

                    elif key in (pygame.K_UP, pygame.K_w):
                        nxt = nav_item(inv, cursor_item, "up")
                        if nxt:
                            cursor_item = nxt
                        else:
                            cursor_op   = next_active_row(cursor_op, -1)
                            cursor_item = first_item(cursor_op)

                    elif key in (pygame.K_SPACE, pygame.K_RETURN):
                        if cursor_item:
                            menu_options = get_menu_options(cursor_item, inv)
                            menu_idx     = 0
                            mode         = MODE_MENU

                    elif key == pygame.K_f:
                        # Deal 10 damage to current operador (for testing)
                        op = OPERADORS[cursor_op]
                        op["hp"] = max(0, op["hp"] - 10)
                        if op["hp"] == 0:
                            op["active"] = False
                            nxt = next_active_row(cursor_op, 1)
                            if nxt != cursor_op:
                                cursor_op   = nxt
                                cursor_item = first_item(cursor_op)

                    elif key == pygame.K_g:
                        # Heal 10 HP (revives if KIA, for testing)
                        op = OPERADORS[cursor_op]
                        was_dead = op["hp"] <= 0
                        op["hp"] = min(op["hp_max"], op["hp"] + 10)
                        if was_dead and op["hp"] > 0:
                            op["active"] = True

                    elif key == pygame.K_x:
                        if cursor_item:
                            reorder_item     = cursor_item
                            reorder_orig     = (cursor_item["col"], cursor_item["row"])
                            reorder_orig_rot = cursor_item["rotated"]
                            reorder_orig_op  = cursor_op
                            reorder_op       = cursor_op
                            reorder_col      = cursor_item["col"]
                            reorder_row      = cursor_item["row"]
                            OPERADORS[cursor_op]["inventory"].remove(cursor_item)
                            mode = MODE_REORDER

                # --- MODE_REORDER ---
                elif mode == MODE_REORDER:
                    if key in (pygame.K_RIGHT, pygame.K_d):
                        reorder_col = min(reorder_col+1, GRID_COLS-1)

                    elif key in (pygame.K_LEFT, pygame.K_a):
                        reorder_col = max(reorder_col-1, 0)

                    elif key in (pygame.K_DOWN, pygame.K_s):
                        if reorder_row < GRID_ROWS-1:
                            reorder_row += 1
                        else:
                            nxt = next_active_row(reorder_op, 1)
                            if nxt != reorder_op:
                                reorder_op  = nxt
                                reorder_row = 0

                    elif key in (pygame.K_UP, pygame.K_w):
                        if reorder_row > 0:
                            reorder_row -= 1
                        else:
                            prv = next_active_row(reorder_op, -1)
                            if prv != reorder_op:
                                reorder_op  = prv
                                reorder_row = GRID_ROWS-1

                    elif key == pygame.K_y:
                        reorder_item["rotated"] = not reorder_item["rotated"]

                    elif key in (pygame.K_SPACE, pygame.K_RETURN):
                        inv = OPERADORS[reorder_op]["inventory"]
                        if can_place(inv, reorder_item, reorder_col, reorder_row):
                            reorder_item["col"] = reorder_col
                            reorder_item["row"] = reorder_row
                            inv.append(reorder_item)
                            cursor_item  = reorder_item
                            cursor_op    = reorder_op
                            reorder_item = None
                            mode = MODE_NORMAL

                # --- MODE_MENU ---
                elif mode == MODE_MENU:
                    if key in (pygame.K_DOWN, pygame.K_s):
                        menu_idx = (menu_idx+1) % len(menu_options)
                    elif key in (pygame.K_UP, pygame.K_w):
                        menu_idx = (menu_idx-1) % len(menu_options)
                    elif key in (pygame.K_SPACE, pygame.K_RETURN):
                        action = menu_options[menu_idx]
                        inv    = OPERADORS[cursor_op]["inventory"]
                        if action in ("Equipar", "Desequipar"):
                            cat = ITEM_DEFS[cursor_item["type_key"]]["category"]
                            if cursor_item.get("equipped"):
                                cursor_item["equipped"] = False
                            else:
                                for it in inv:
                                    if ITEM_DEFS[it["type_key"]]["category"] == cat:
                                        it["equipped"] = False
                                cursor_item["equipped"] = True
                            mode = MODE_NORMAL
                        elif action == "Recargar":
                            defn    = ITEM_DEFS[cursor_item["type_key"]]
                            caliber = defn["caliber"]
                            if caliber == "9mm":
                                reload_item = cursor_item
                                reload_op   = cursor_op
                                reload_idx  = 0
                                mode        = MODE_RELOAD
                            else:
                                if consume_box(inv, caliber):
                                    cursor_item["loaded"] = cursor_item.get("capacity", 30)
                                mode = MODE_NORMAL
                        elif action == "Examinar":
                            mode = MODE_NORMAL  # info shown in strip

                # --- MODE_RELOAD ---
                elif mode == MODE_RELOAD:
                    if key in (pygame.K_LEFT, pygame.K_a, pygame.K_UP, pygame.K_w):
                        reload_idx = (reload_idx-1) % 2
                    elif key in (pygame.K_RIGHT, pygame.K_d, pygame.K_DOWN, pygame.K_s):
                        reload_idx = (reload_idx+1) % 2
                    elif key in (pygame.K_SPACE, pygame.K_RETURN):
                        inv     = OPERADORS[reload_op]["inventory"]
                        caliber = ITEM_DEFS[reload_item["type_key"]]["caliber"]
                        if consume_box(inv, caliber):
                            reload_item["ammo_type"] = ["RIP","FMJ"][reload_idx]
                            reload_item["loaded"]    = reload_item.get("capacity", 13)
                        reload_item = None
                        mode = MODE_NORMAL

        # --- Draw ---
        screen.fill(DARK_BG)
        draw_navbar(screen, active_tab, font)

        if active_tab == 0:
            for op_idx, op in enumerate(OPERADORS):
                ry       = row_y(op_idx)
                active   = op["active"]
                is_sel   = (op_idx == cursor_op) and active

                row_rect = pygame.Rect(0, ry, STRIP_X-1, ROW_H)
                pygame.draw.rect(screen, ROW_ACTIVE_BG if active else ROW_INACTIVE, row_rect)
                if is_sel and mode != MODE_REORDER:
                    pygame.draw.rect(screen, SELECT_ROW, row_rect, 2)

                draw_portrait(screen, pygame.Rect(PORT_X, ry+3, PORT_W, ROW_H-6), op, font_tiny)
                hp_ratio = op["hp"] / op["hp_max"] if op["hp_max"] > 0 else 0
                draw_ecg_row(screen, pygame.Rect(ECG_X, ry+3, ECG_W, ROW_H-6), time_ms, effective_bpm(op), active and op["hp"] > 0, hp_ratio, font_tiny)
                draw_equip_slots(screen, pygame.Rect(SLOT_X, ry+3, SLOT_W, ROW_H-6), op["inventory"], active, font_tiny)

                if active:
                    draw_grid_row(screen, op_idx, is_sel,
                                  mode, reorder_item, reorder_op, reorder_col, reorder_row)
                    for item in op["inventory"]:
                        sel = (item is cursor_item) and is_sel and mode in (MODE_NORMAL, MODE_MENU, MODE_RELOAD)
                        draw_item_in_row(screen, item, op_idx, font_tiny, selected=sel)

            draw_strip(screen, mode, cursor_item,
                       menu_options, menu_idx,
                       reload_item, reload_op, reload_idx,
                       font, font_big, font_tiny)

        elif active_tab == 1:
            draw_map(screen, font_big, font_tiny)
        elif active_tab == 2:
            draw_files(screen, file_idx, font, font_big, font_tiny)
        elif active_tab == len(TABS) - 1:
            draw_exit_screen(screen, font_big, font_tiny)

        pygame.display.flip()
        clock.tick(60)

    pygame.quit()
    sys.exit()


if __name__ == "__main__":
    main()
