"""
Crimson Draft - QTE Bidimensional Prototype
Sistema de puntería: RE Gaiden + Shadow Hearts + Vagrant Story targeting

Flujo:
1. [SPACE] para iniciar QTE
2. Barra vertical se mueve — [SPACE] fija eje Y
3. Barra horizontal se mueve — [SPACE] fija eje X
4. Se aplica dispersión según arma + vida
5. Intersección final determina zona de impacto + daño
"""

import pygame
import sys
import math
import random
import numpy as np

# --- Constantes ---
SCREEN_W, SCREEN_H = 850, 600
GRID_ORIGIN_X, GRID_ORIGIN_Y = 250, 50
GRID_W, GRID_H = 300, 500
CELL_SIZE = 25

# QTE
BAR_THICKNESS = 3
RESULT_DISPLAY_TIME = 1500  # ms
QTE_TIME_LIMIT = 2000       # ms para cada eje antes de auto-disparo
VIBRATION_MAX_PX = 14       # px de vibración máxima a 0% vida
HEARTBEAT_BPM_MIN = 60      # BPM a 100% vida (sin vibración real)
HEARTBEAT_BPM_MAX = 160     # BPM a 10% vida (pánico)

# Distracciones visuales
SHAKE_MAX_PX = 8            # px de screen shake máximo
VIGNETTE_MAX_DEPTH = 40     # px de profundidad de viñeta desde cada borde
VIGNETTE_MAX_ALPHA = 180    # alpha máximo de viñeta en bordes
NOISE_MAX_DENSITY = 150     # cantidad máxima de píxeles de ruido
GHOST_MAX_OFFSET = 5        # px de offset máximo para ghost lines
FLICKER_THRESHOLD = 0.35    # daño mínimo para que la silueta parpadee

# Armas
WEAPONS = {
    "P229": {
        "base_damage": 28,
        "dispersion_base": 12,
        "bar_speed_x": 4.0,
        "bar_speed_y": 4.5,
        "weapon_deviation": 2,
        "pattern_spread": 2,
        "magazine_capacity": 13,
        "caliber": "9mm",
        "sfx": "fire_pistol",
        # Shape: "7" — rises gently, then drifts progressively right
        # Player compensation: pull down-left
        "recoil_pattern": [
            (0, -5),     # 2nd: gentle rise
            (2, -6),     # 3rd: starts right drift
            (3, -5),     # 4th
            (4, -4),     # 5th: curve right
            (5, -4),     # 6th: constant right drift
            (5, -3),     # 7th
            (6, -2),     # 8th: more horizontal
            (6, -2),     # 9th
            (6, -1),     # 10th: stabilizes
            (5, -1),     # 11th
            (5, 0),      # 12th: nearly pure horizontal
            (4, 0),      # 13th: final
        ],
    },
    "MP5": {
        "base_damage": 22,
        "dispersion_base": 14,
        "bar_speed_x": 5.5,
        "bar_speed_y": 6.0,
        "weapon_deviation": 2,
        "pattern_spread": 3,
        "magazine_capacity": 30,
        "caliber": "9mm",
        "sfx": "fire_smg",
        # Shape: "I" with slight right lean — controlled vertical, soft right drift after shot 15
        # Most predictable pattern in the arsenal. Player compensation: soft down, almost no lateral.
        "recoil_pattern": [
            (0, -4),     # 2
            (0, -4),     # 3
            (1, -5),     # 4
            (1, -4),     # 5
            (1, -4),     # 6
            (1, -3),     # 7
            (1, -3),     # 8
            (2, -3),     # 9
            (2, -3),     # 10
            (2, -2),     # 11
            (2, -2),     # 12
            (2, -2),     # 13
            (2, -2),     # 14
            (2, -1),     # 15
            (3, -1),     # 16: soft right drift begins
            (3, -1),     # 17
            (3, -1),     # 18
            (3, -1),     # 19
            (3, 0),      # 20
            (3, 0),      # 21
            (3, 0),      # 22
            (3, 0),      # 23
            (3, 0),      # 24
            (3, 1),      # 25: slight downward drift
            (3, 1),      # 26
            (3, 1),      # 27
            (2, 1),      # 28: decelerates
            (2, 0),      # 29: final
        ],
    },
    "Benelli M4": {
        "base_damage": 12,
        "dispersion_base": 40,
        "bar_speed_x": 4.0,
        "bar_speed_y": 4.5,
        "pellets": 6,
        "weapon_deviation": 3,
        "pattern_spread": 4,
        "magazine_capacity": 7,
        "caliber": "12ga",
        "sfx": "fire_shotgun",
        # Shape: inverted "V" — massive vertical kick, then drops right
        # Player compensation: pull hard down at start
        "recoil_pattern": [
            (0, -25),    # 2nd: massive vertical kick
            (5, -20),    # 3rd: still high, starts right
            (10, -10),   # 4th: drops, pulls right
            (8, -5),     # 5th: stabilizes low-right
            (5, 0),      # 6th: pure horizontal right
            (3, 2),      # 7th: slight down-right
        ],
    },
    "Mk18": {
        "base_damage": 55,
        "dispersion_base": 6,
        "bar_speed_x": 7.5,
        "bar_speed_y": 8.0,
        "weapon_deviation": 1,
        "pattern_spread": 2,
        "magazine_capacity": 30,
        "caliber": "5.56",
        "sfx": "fire_rifle",
        # Shape: extended inverted "J" — aggressive vertical then hard left curve,
        # flattens from shot 11 onward, ends in soft left drift
        # Player compensation: pull down-right
        "recoil_pattern": [
            (0, -14),    # 2: strong vertical
            (0, -16),    # 3: continues strong
            (-2, -14),   # 4: starts left
            (-4, -12),   # 5: more left
            (-6, -10),   # 6: left curve
            (-8, -8),    # 7: diagonal left-up
            (-10, -6),   # 8: mostly horizontal left
            (-12, -4),   # 9: nearly pure left
            (-14, -2),   # 10: very horizontal left
            (-14, -1),   # 11: plateau
            (-14, 0),    # 12
            (-13, 0),    # 13
            (-13, 0),    # 14
            (-12, 0),    # 15: curve flattens
            (-12, 1),    # 16
            (-11, 1),    # 17
            (-11, 1),    # 18
            (-10, 1),    # 19
            (-10, 1),    # 20
            (-9, 1),     # 21
            (-9, 0),     # 22
            (-8, 0),     # 23
            (-8, 0),     # 24
            (-7, 0),     # 25
            (-7, 0),     # 26
            (-6, 0),     # 27
            (-6, 0),     # 28
            (-5, 0),     # 29: final
        ],
    },
}
WEAPON_NAMES = list(WEAPONS.keys())

# Vida
PLAYER_MAX_HP = 100
DISPERSION_HP_FACTOR = 2.0  # a 0% vida, radio = base * factor

# Estados del QTE
STATE_IDLE = 0
STATE_QTE_X = 1
STATE_QTE_Y = 2
STATE_RESULT = 3
STATE_SELECT_SHOTS = 4
STATE_RELOAD_SELECT = 5

# --- Proteccion (Armor) ---
ARMOR_TYPES = {
    "casco_militar": {
        "base_zone": "CABEZA",
        "coverage": (0.0, 0.0, 1.0, 0.65),  # (rx, ry, rw, rh) fraction of base rect
        "dmg_reduction": 0.50,
        "label": "CASCO",
        "color": (80, 120, 80),
    },
    "chaleco_torax": {
        "base_zone": "TORSO",
        "coverage": (0.0, 0.0, 1.0, 0.45),
        "dmg_reduction": 0.60,
        "label": "CHALECO",
        "color": (60, 60, 120),
    },
    "chaleco_torax_est": {
        "base_zone": "TORSO",
        "coverage": (0.0, 0.0, 1.0, 0.75),
        "dmg_reduction": 0.60,
        "label": "CHALECO+EST",
        "color": (60, 60, 120),
    },
    "chaleco_hombro": {
        "base_zone": "TORSO",
        "coverage": (0.0, 0.0, 0.5, 0.45),
        "dmg_reduction": 0.60,
        "label": "HOMBRO IZQ",
        "color": (60, 60, 120),
    },
    "placas": {
        "base_zone": "TORSO",
        "coverage": (0.1, 0.05, 0.8, 0.55),
        "dmg_reduction": 0.80,
        "label": "PLACAS",
        "color": (100, 100, 100),
    },
}

ARMOR_CONFIGS = [
    {"name": "Sin proteccion",      "pieces": []},
    {"name": "Casco",               "pieces": ["casco_militar"]},
    {"name": "Chaleco torax",       "pieces": ["chaleco_torax"]},
    {"name": "Chaleco torax+est",   "pieces": ["chaleco_torax_est"]},
    {"name": "Chaleco hombro",      "pieces": ["chaleco_hombro"]},
    {"name": "Placas",              "pieces": ["placas"]},
    {"name": "Casco + chaleco",     "pieces": ["casco_militar", "chaleco_torax"]},
    {"name": "Casco + placas",      "pieces": ["casco_militar", "placas"]},
]

ARMOR_ZONE_ALPHA = 100

# --- Tipos de Municion ---
AMMO_TYPES = {
    "RIP": {
        "label": "9mm RIP",
        "short": "RIP",
        "flesh_mult": 1.0,
        "vs_chaleco": 0.4,
        "vs_placas": 0.2,
        "color": (220, 80, 80),
    },
    "FMJ": {
        "label": "9mm FMJ",
        "short": "FMJ",
        "flesh_mult": 0.8,
        "vs_chaleco": 0.7,
        "vs_placas": 0.5,
        "color": (180, 180, 80),
    },
}

# ─── Generador de SFX sintéticos ─────────────────────────

def _make_sound(frequency, duration_ms, volume=0.3, wave="sine", fade_out_ms=0):
    """Genera un pygame.Sound sintético a partir de onda numpy."""
    sample_rate = 44100
    n_samples = int(sample_rate * duration_ms / 1000)
    t = np.linspace(0, duration_ms / 1000, n_samples, endpoint=False)
    if wave == "sine":
        samples = np.sin(2 * np.pi * frequency * t)
    elif wave == "square":
        samples = np.sign(np.sin(2 * np.pi * frequency * t))
    elif wave == "noise":
        samples = np.random.uniform(-1, 1, n_samples)
    else:
        samples = np.sin(2 * np.pi * frequency * t)
    if fade_out_ms > 0:
        fade_n = min(int(sample_rate * fade_out_ms / 1000), n_samples)
        samples[-fade_n:] *= np.linspace(1, 0, fade_n)
    fade_in = min(200, n_samples // 10)
    if fade_in > 0:
        samples[:fade_in] *= np.linspace(0, 1, fade_in)
    samples = (samples * volume * 32767).astype(np.int16)
    stereo = np.column_stack((samples, samples))
    return pygame.sndarray.make_sound(stereo)


def _make_gunshot(freq, dur_ms, noise_dur_ms, volume=0.3):
    """Genera sonido de disparo: onda cuadrada + ruido blanco mezclados."""
    sr = 44100
    n_base = int(sr * dur_ms / 1000)
    n_noise = int(sr * noise_dur_ms / 1000)
    n_total = max(n_base, n_noise)
    t = np.linspace(0, n_total / sr, n_total, endpoint=False)
    base = np.zeros(n_total)
    base[:n_base] = np.sign(np.sin(2 * np.pi * freq * t[:n_base]))
    noise = np.zeros(n_total)
    noise[:n_noise] = np.random.uniform(-1, 1, n_noise) * 0.6
    mixed = base * 0.6 + noise * 0.4
    # Decay rapido
    decay = np.exp(-t * 20)
    mixed *= decay
    # Fade in
    fade_in = min(100, n_total // 10)
    if fade_in > 0:
        mixed[:fade_in] *= np.linspace(0, 1, fade_in)
    mixed = (mixed * volume * 32767).astype(np.int16)
    stereo = np.column_stack((mixed, mixed))
    return pygame.sndarray.make_sound(stereo)


def _make_compound(parts, volume=0.3):
    """Genera sonido compuesto de varios segmentos [(freq, dur_ms, wave), ...]."""
    sr = 44100
    segments = []
    for freq, dur_ms, wave in parts:
        n = int(sr * dur_ms / 1000)
        if freq == 0:  # silencio
            segments.append(np.zeros(n))
        else:
            t = np.linspace(0, dur_ms / 1000, n, endpoint=False)
            if wave == "sine":
                segments.append(np.sin(2 * np.pi * freq * t))
            else:
                segments.append(np.sign(np.sin(2 * np.pi * freq * t)))
    samples = np.concatenate(segments)
    fade_in = min(100, len(samples) // 10)
    if fade_in > 0:
        samples[:fade_in] *= np.linspace(0, 1, fade_in)
    fade_out = min(200, len(samples) // 5)
    if fade_out > 0:
        samples[-fade_out:] *= np.linspace(1, 0, fade_out)
    samples = (samples * volume * 32767).astype(np.int16)
    stereo = np.column_stack((samples, samples))
    return pygame.sndarray.make_sound(stereo)


def init_sounds():
    """Genera todos los SFX del prototipo. Llamar después de pygame.init()."""
    return {
        "lock_y": _make_sound(800, 60, 0.3, "sine", fade_out_ms=30),
        "lock_x": _make_sound(1000, 60, 0.35, "sine", fade_out_ms=30),
        "timeout": _make_compound([(600, 80, "sine"), (400, 120, "sine")], 0.25),
        "fire_pistol": _make_gunshot(150, 100, 60, 0.3),
        "fire_smg":    _make_gunshot(180, 70,  50, 0.28),
        "fire_rifle": _make_gunshot(100, 80, 40, 0.35),
        "fire_shotgun": _make_gunshot(80, 150, 100, 0.4),
        "hit_critical": _make_sound(1200, 100, 0.4, "sine", fade_out_ms=50),
        "hit_solid": _make_sound(800, 80, 0.3, "sine", fade_out_ms=40),
        "hit_graze": _make_sound(500, 60, 0.2, "sine", fade_out_ms=30),
        "miss": _make_sound(200, 200, 0.25, "sine", fade_out_ms=150),
        "reload": _make_compound([(2000, 30, "sine"), (0, 50, "sine"), (1500, 30, "sine")], 0.3),
        "empty": _make_sound(3000, 20, 0.15, "square", fade_out_ms=10),
        "select_shot": _make_sound(1500, 30, 0.2, "sine", fade_out_ms=15),
        "heartbeat_lub": _make_sound(50, 100, 0.3, "sine", fade_out_ms=60),
        "heartbeat_dub": _make_sound(40, 80, 0.2, "sine", fade_out_ms=50),
        "weapon_switch": _make_compound([(600, 40, "sine"), (900, 40, "sine")], 0.25),
        "armor_absorb": _make_sound(300, 150, 0.3, "square", fade_out_ms=100),
    }



def play_hit_sfx(sounds, result):
    """Reproduce SFX de impacto según resultado del disparo."""
    if result["damage"] == 0:
        sounds["miss"].play()
    elif result.get("armor_absorbed"):
        sounds["armor_absorb"].play()
    elif "CENTRO" in result.get("prec_label", ""):
        sounds["hit_critical"].play()
    elif "SOLIDO" in result.get("prec_label", ""):
        sounds["hit_solid"].play()
    else:
        sounds["hit_graze"].play()


# Colores
BLACK = (0, 0, 0)
WHITE = (255, 255, 255)
DARK_GRAY = (30, 30, 30)
GRID_COLOR = (40, 50, 40)
GRID_ACCENT = (60, 80, 60)
SILHOUETTE_COLOR = (180, 180, 180)
HEAD_COLOR = (220, 60, 60)
TORSO_COLOR = (60, 120, 220)
ARM_COLOR = (220, 180, 60)
LEG_COLOR = (60, 200, 120)
ZONE_ALPHA = 80
BAR_COLOR = (255, 220, 50)
BAR_LOCKED_COLOR = (50, 255, 100)
TIMER_BG_COLOR = (50, 50, 50)
TIMER_FILL_COLOR = (220, 180, 40)
TIMER_LOW_COLOR = (220, 50, 50)
TEXT_COLOR = (200, 200, 200)
HIGHLIGHT_COLOR = (255, 255, 100)
IMPACT_COLOR = (255, 80, 80)
GRAZE_COLOR = (180, 180, 180)
DISPERSION_COLOR = (255, 255, 100, 40)
HP_COLOR = (80, 200, 80)
HP_LOW_COLOR = (220, 60, 60)


# ─── Grilla ───────────────────────────────────────────────

def draw_grid(surface, sx=0, sy=0, ghost_offset=0, time_ms=0):
    ox = GRID_ORIGIN_X + int(sx)
    oy = GRID_ORIGIN_Y + int(sy)
    grid_rect = pygame.Rect(ox - 10, oy - 10, GRID_W + 20, GRID_H + 20)
    pygame.draw.rect(surface, (15, 20, 15), grid_rect)
    pygame.draw.rect(surface, (80, 100, 80), grid_rect, 2)

    for x in range(0, GRID_W + 1, CELL_SIZE):
        color = GRID_ACCENT if x % (CELL_SIZE * 4) == 0 else GRID_COLOR
        pygame.draw.line(surface, color,
                         (ox + x, oy), (ox + x, oy + GRID_H))

    for y in range(0, GRID_H + 1, CELL_SIZE):
        color = GRID_ACCENT if y % (CELL_SIZE * 4) == 0 else GRID_COLOR
        pygame.draw.line(surface, color,
                         (ox, oy + y), (ox + GRID_W, oy + y))

    cx = ox + GRID_W // 2
    cy = oy + GRID_H // 2
    pygame.draw.line(surface, (80, 100, 80), (cx, oy), (cx, oy + GRID_H))
    pygame.draw.line(surface, (80, 100, 80), (ox, cy), (ox + GRID_W, cy))

    # Ghost lines — visión doble
    if ghost_offset > 0:
        osc = ghost_offset * math.sin(time_ms * 0.003)
        gx = int(osc)
        gy = int(osc * 0.7)
        ghost_surf = pygame.Surface((GRID_W + 20, GRID_H + 20), pygame.SRCALPHA)
        ghost_color = (80, 100, 80, 50)
        for x in range(0, GRID_W + 1, CELL_SIZE):
            pygame.draw.line(ghost_surf, ghost_color,
                             (10 + x + gx, 10 + gy), (10 + x + gx, 10 + GRID_H + gy))
        for y in range(0, GRID_H + 1, CELL_SIZE):
            pygame.draw.line(ghost_surf, ghost_color,
                             (10 + gx, 10 + y + gy), (10 + GRID_W + gx, 10 + y + gy))
        surface.blit(ghost_surf, (ox - 10, oy - 10))


# ─── Silueta ──────────────────────────────────────────────

def draw_silhouette(surface, sx=0, sy=0):
    cx = GRID_ORIGIN_X + int(sx) + GRID_W // 2
    base_y = GRID_ORIGIN_Y + int(sy) + 30
    head_r = 22
    neck_w, neck_h = 12, 12
    shoulder_w = 70
    torso_w, torso_h = 55, 120
    arm_w, arm_h = 22, 130
    hip_w, hip_h = 48, 30
    leg_w, leg_h = 28, 160
    color = SILHOUETTE_COLOR

    head_cy = base_y + head_r
    pygame.draw.circle(surface, color, (cx, head_cy), head_r)

    neck_top = head_cy + head_r
    pygame.draw.rect(surface, color, (cx - neck_w // 2, neck_top, neck_w, neck_h))

    shoulder_top = neck_top + neck_h
    pygame.draw.polygon(surface, color, [
        (cx - neck_w // 2, shoulder_top), (cx + neck_w // 2, shoulder_top),
        (cx + shoulder_w, shoulder_top + 15), (cx - shoulder_w, shoulder_top + 15),
    ])

    torso_top = shoulder_top + 15
    pygame.draw.rect(surface, color,
                     pygame.Rect(cx - torso_w, torso_top, torso_w * 2, torso_h))

    arm_top = shoulder_top + 5
    pygame.draw.rect(surface, color,
                     pygame.Rect(cx - shoulder_w - arm_w + 5, arm_top, arm_w, arm_h),
                     border_radius=8)
    pygame.draw.rect(surface, color,
                     pygame.Rect(cx + shoulder_w - 5, arm_top, arm_w, arm_h),
                     border_radius=8)

    hip_top = torso_top + torso_h
    pygame.draw.polygon(surface, color, [
        (cx - torso_w, hip_top), (cx + torso_w, hip_top),
        (cx + hip_w, hip_top + hip_h), (cx - hip_w, hip_top + hip_h),
    ])

    leg_top = hip_top + hip_h
    pygame.draw.rect(surface, color,
                     pygame.Rect(cx - hip_w, leg_top, leg_w, leg_h), border_radius=6)
    pygame.draw.rect(surface, color,
                     pygame.Rect(cx + hip_w - leg_w, leg_top, leg_w, leg_h), border_radius=6)


# ─── Hitboxes ─────────────────────────────────────────────

def get_hitboxes():
    cx = GRID_ORIGIN_X + GRID_W // 2
    base_y = GRID_ORIGIN_Y + 30
    head_r = 22
    head_cy = base_y + head_r
    neck_h = 12
    shoulder_top = head_cy + head_r + neck_h
    torso_top = shoulder_top + 15
    torso_h, torso_w = 120, 55
    shoulder_w, arm_w, arm_h = 70, 22, 130
    hip_h, hip_w = 30, 48
    leg_w, leg_h = 28, 160

    zones = {
        "CABEZA": {
            "rect": pygame.Rect(cx - 25, head_cy - 25, 50, 50),
            "color": HEAD_COLOR, "dmg_mult": 2.0,
            "effect": "CRITICO / STUN",
        },
        "BRAZO IZQ": {
            "rect": pygame.Rect(cx - shoulder_w - arm_w + 5, shoulder_top + 5, arm_w + 3, arm_h),
            "color": ARM_COLOR, "dmg_mult": 0.7,
            "effect": "-PRECISION ENEMIGO",
        },
        "BRAZO DER": {
            "rect": pygame.Rect(cx + shoulder_w - 5, shoulder_top + 5, arm_w + 3, arm_h),
            "color": ARM_COLOR, "dmg_mult": 0.7,
            "effect": "-PRECISION ENEMIGO",
        },
        "TORSO": {
            "rect": pygame.Rect(cx - torso_w, head_cy + 25, torso_w * 2, torso_top + torso_h + hip_h - (head_cy + 25)),
            "color": TORSO_COLOR, "dmg_mult": 1.0,
            "effect": "DAÑO ESTABLE",
        },
        "PIERNA IZQ": {
            "rect": pygame.Rect(cx - hip_w, torso_top + torso_h + hip_h, leg_w + 4, leg_h),
            "color": LEG_COLOR, "dmg_mult": 0.6,
            "effect": "-VELOCIDAD ENEMIGO",
        },
        "PIERNA DER": {
            "rect": pygame.Rect(cx + hip_w - leg_w, torso_top + torso_h + hip_h, leg_w + 4, leg_h),
            "color": LEG_COLOR, "dmg_mult": 0.6,
            "effect": "-VELOCIDAD ENEMIGO",
        },
    }
    return zones


def draw_hitboxes(surface, zones, show_zones=True, sx=0, sy=0):
    if not show_zones:
        return
    for name, zone in zones.items():
        zone_surf = pygame.Surface(
            (zone["rect"].width, zone["rect"].height), pygame.SRCALPHA)
        r, g, b = zone["color"]
        zone_surf.fill((r, g, b, ZONE_ALPHA))
        surface.blit(zone_surf, (zone["rect"].x + int(sx), zone["rect"].y + int(sy)))
        shifted = zone["rect"].move(int(sx), int(sy))
        pygame.draw.rect(surface, zone["color"], shifted, 1)


def get_armor_hitboxes(body_zones, armor_config_index):
    """Compute armor hitbox rects relative to body zone rects for the given config.
    Returns list of armor piece dicts with computed pygame.Rect."""
    config = ARMOR_CONFIGS[armor_config_index]
    armor_list = []
    for piece_key in config["pieces"]:
        piece = ARMOR_TYPES[piece_key]
        base = body_zones[piece["base_zone"]]["rect"]
        rx, ry, rw, rh = piece["coverage"]
        armor_list.append({
            "rect": pygame.Rect(
                base.x + int(base.width * rx),
                base.y + int(base.height * ry),
                int(base.width * rw),
                int(base.height * rh)),
            "dmg_reduction": piece["dmg_reduction"],
            "armor_type": piece_key,
            "label": piece["label"],
            "color": piece["color"],
        })
    return armor_list


def draw_armor_hitboxes(surface, armor_hitboxes, sx=0, sy=0):
    """Draw armor overlay rects with diagonal hatching to distinguish from body hitboxes."""
    if not armor_hitboxes:
        return
    for armor in armor_hitboxes:
        r = armor["rect"]
        arm_surf = pygame.Surface((r.width, r.height), pygame.SRCALPHA)
        cr, cg, cb = armor["color"]
        arm_surf.fill((cr, cg, cb, ARMOR_ZONE_ALPHA))
        # Diagonal hatching lines for visual distinction
        hatch_color = (min(255, cr + 40), min(255, cg + 40), min(255, cb + 40), 60)
        for i in range(-r.height, r.width, 8):
            pygame.draw.line(arm_surf, hatch_color, (i, 0), (i + r.height, r.height), 1)
        surface.blit(arm_surf, (r.x + int(sx), r.y + int(sy)))
        shifted = r.move(int(sx), int(sy))
        pygame.draw.rect(surface, armor["color"], shifted, 2)


# ─── Vibración tipo latido ───────────────────────────────

def _heartbeat_phase_intensity(time_ms, hp_ratio):
    """Calcula la intensidad normalizada (0.0-1.0) del latido en el instante actual.
    Retorna (intensity, damage_ratio). intensity=0 en silencio, 1.0 en pico lub."""
    damage_ratio = 1.0 - hp_ratio
    if damage_ratio < 0.05:
        return 0.0, damage_ratio

    bpm = HEARTBEAT_BPM_MIN + (HEARTBEAT_BPM_MAX - HEARTBEAT_BPM_MIN) * damage_ratio
    beat_period_ms = 60000.0 / bpm
    phase = (time_ms % beat_period_ms) / beat_period_ms

    # Lub: pico fuerte en phase 0.0-0.12
    # Dub: pico menor en phase 0.18-0.28
    # Silencio: 0.28-1.0
    if phase < 0.06:
        intensity = phase / 0.06
    elif phase < 0.12:
        intensity = 1.0 - (phase - 0.06) / 0.06
    elif phase < 0.18:
        intensity = 0.0
    elif phase < 0.23:
        intensity = 0.6 * (phase - 0.18) / 0.05
    elif phase < 0.28:
        intensity = 0.6 * (1.0 - (phase - 0.23) / 0.05)
    else:
        intensity = 0.0

    return intensity, damage_ratio


def heartbeat_offset(time_ms, hp_ratio):
    """Calcula offset de vibración con patrón de latido cardíaco.
    Retorna offset en px."""
    intensity, damage_ratio = _heartbeat_phase_intensity(time_ms, hp_ratio)
    amp = VIBRATION_MAX_PX * damage_ratio
    return amp * intensity


def heartbeat_visual_t(time_ms, hp_ratio):
    """Retorna intensidad visual del latido (0.0-1.0) para feedback en barras.
    Más agresivo que el offset: arranca visible desde poca vida perdida."""
    intensity, damage_ratio = _heartbeat_phase_intensity(time_ms, hp_ratio)
    # Escalar para que sea visible incluso con poco daño
    visual_scale = min(1.0, damage_ratio * 1.8)
    return intensity * visual_scale


# ─── Distracciones visuales ──────────────────────────────

def get_screen_shake(time_ms, hp_ratio):
    """Sacudida de la grilla sincronizada con el heartbeat. Retorna (dx, dy) en px."""
    intensity, damage_ratio = _heartbeat_phase_intensity(time_ms, hp_ratio)
    if damage_ratio < 0.1 or intensity < 0.01:
        return 0, 0
    shake = SHAKE_MAX_PX * damage_ratio * intensity
    return random.uniform(-shake, shake), random.uniform(-shake, shake)


def draw_blood_vignette(surface, hp_ratio, time_ms):
    """Overlay rojo semitransparente desde los bordes de la grilla."""
    damage_ratio = 1.0 - hp_ratio
    if damage_ratio < 0.15:
        return
    intensity, _ = _heartbeat_phase_intensity(time_ms, hp_ratio)
    pulse = 0.7 + 0.3 * intensity
    depth = int(VIGNETTE_MAX_DEPTH * damage_ratio)
    base_alpha = int(VIGNETTE_MAX_ALPHA * damage_ratio * pulse)
    vig_surf = pygame.Surface((GRID_W, GRID_H), pygame.SRCALPHA)
    # 4 franjas desde cada borde con alpha decreciente
    strips = 4
    for i in range(strips):
        frac = 1.0 - i / strips  # 1.0 en borde, 0.25 en interior
        a = int(base_alpha * frac)
        w = max(1, depth // strips)
        # Top
        vig_surf.fill((120, 0, 0, a), (0, i * w, GRID_W, w))
        # Bottom
        vig_surf.fill((120, 0, 0, a), (0, GRID_H - (i + 1) * w, GRID_W, w))
        # Left
        vig_surf.fill((120, 0, 0, a), (i * w, 0, w, GRID_H))
        # Right
        vig_surf.fill((120, 0, 0, a), (GRID_W - (i + 1) * w, 0, w, GRID_H))
    surface.blit(vig_surf, (GRID_ORIGIN_X, GRID_ORIGIN_Y))


def draw_static_noise(surface, hp_ratio, time_ms):
    """Píxeles aleatorios parpadeantes sobre la grilla."""
    damage_ratio = 1.0 - hp_ratio
    if damage_ratio < 0.25:
        return
    density = int(NOISE_MAX_DENSITY * damage_ratio)
    base_alpha = int(100 * damage_ratio)
    noise_surf = pygame.Surface((GRID_W, GRID_H), pygame.SRCALPHA)
    for _ in range(density):
        nx = random.randint(0, GRID_W - 3)
        ny = random.randint(0, GRID_H - 3)
        grey = random.randint(150, 255)
        a = random.randint(base_alpha // 2, base_alpha)
        noise_surf.fill((grey, grey, grey, a), (nx, ny, 3, 3))
    surface.blit(noise_surf, (GRID_ORIGIN_X, GRID_ORIGIN_Y))


def should_flicker_silhouette(time_ms, hp_ratio):
    """Determina si la silueta debe desaparecer este frame. Solo a vida baja."""
    damage_ratio = 1.0 - hp_ratio
    if damage_ratio < FLICKER_THRESHOLD:
        return False
    # Flicker proporcional al daño
    flicker_chance = 0.15 * (damage_ratio - FLICKER_THRESHOLD) / (1.0 - FLICKER_THRESHOLD)
    # Período se acorta con más daño: 800ms a 400ms
    period = int(800 - 400 * (damage_ratio - FLICKER_THRESHOLD) / (1.0 - FLICKER_THRESHOLD))
    invisible_window = int(period * flicker_chance)
    return (time_ms % period) < invisible_window


# ─── Dispersión ──────────────────────────────────────────

def apply_three_layer_dispersion(center_x, center_y, weapon, hp_ratio, consecutive_shots, is_first_shot=True, hand=1):
    """
    Aplica capas de dispersión al punto de intención del QTE.
    Layer 1: Dispersión base (HP) — SOLO en el primer disparo de la ráfaga
    Layer 2: Desviación mecánica del arma — imperfecciones (siempre)
    Layer 3: Recoil por patrón — desplazamiento predefinido (desde 2do disparo)
    hand: 1 = diestro, -1 = zurdo (invierte componente X del recoil)
    Retorna (final_x, final_y, layer_data)
    """
    hp_ratio = max(0.0, min(1.0, hp_ratio))

    # --- Layer 1: Dispersión base (HP) — solo primer disparo ---
    l1_radius = weapon["dispersion_base"] * (1.0 + (1.0 - hp_ratio) * (DISPERSION_HP_FACTOR - 1.0))
    if is_first_shot:
        angle = random.uniform(0, 2 * math.pi)
        r = l1_radius * math.sqrt(random.random())
        l1_x = center_x + r * math.cos(angle)
        l1_y = center_y + r * math.sin(angle)
    else:
        l1_x = center_x
        l1_y = center_y

    # --- Layer 2: Desviación mecánica del arma ---
    dev = weapon["weapon_deviation"]
    l2_x = l1_x + random.uniform(-dev, dev)
    l2_y = l1_y + random.uniform(-dev, dev)

    # --- Layer 3: Recoil por patrón predefinido (0 en primer disparo) ---
    pattern = weapon.get("recoil_pattern", [])
    pattern_idx = consecutive_shots - 1  # Disparo 0 no tiene recoil
    if pattern_idx >= 0 and pattern_idx < len(pattern):
        pattern_dx, pattern_dy = pattern[pattern_idx]
    elif pattern_idx >= len(pattern) and len(pattern) > 0:
        # Si se excede el patrón, repetir el último punto
        pattern_dx, pattern_dy = pattern[-1]
    else:
        pattern_dx, pattern_dy = 0, 0

    # Spread aleatorio alrededor del punto del patrón
    # hand invierte el componente X del recoil: diestro (1) = patrón normal, zurdo (-1) = espejado
    spread = weapon.get("pattern_spread", 0)
    horizontal_recoil = (pattern_dx * hand) + random.uniform(-spread, spread)
    vertical_recoil = -pattern_dy  # Invertir: en patrón dy negativo = arriba, en pantalla Y+ = abajo

    l3_x = l2_x + horizontal_recoil
    l3_y = l2_y + pattern_dy + random.uniform(-spread, spread)

    # Clampear dentro de la grilla
    final_x = max(GRID_ORIGIN_X, min(GRID_ORIGIN_X + GRID_W, int(l3_x)))
    final_y = max(GRID_ORIGIN_Y, min(GRID_ORIGIN_Y + GRID_H, int(l3_y)))

    layer_data = {
        "aim": (center_x, center_y),
        "l1_point": (int(l1_x), int(l1_y)),
        "l1_radius": l1_radius,
        "l2_point": (int(l2_x), int(l2_y)),
        "vertical_recoil": vertical_recoil,
        "horizontal_recoil": horizontal_recoil,
        "pattern_offset": (pattern_dx, pattern_dy),
        "consecutive_shots": consecutive_shots,
    }

    return final_x, final_y, layer_data


# ─── Cálculo de impacto ──────────────────────────────────

def calc_precision_mult(hit_x, hit_y, zone_rect):
    cx = zone_rect.centerx
    cy = zone_rect.centery
    dx = abs(hit_x - cx) / (zone_rect.width / 2)
    dy = abs(hit_y - cy) / (zone_rect.height / 2)
    dist = math.sqrt(dx * dx + dy * dy) / math.sqrt(2)

    if dist < 0.2:
        return 1.5, "CENTRO PERFECTO"
    elif dist < 0.6:
        return 1.0, "IMPACTO SOLIDO"
    else:
        return 0.75, "BORDE DE ZONA"


def resolve_hit(hit_x, hit_y, zones, base_damage, armor_hitboxes=None, ammo_type=None):
    """Determina zona impactada, evalua armor layer y calcula daño. El disparo SIEMPRE ocurre."""
    for name, zone in zones.items():
        if zone["rect"].collidepoint(hit_x, hit_y):
            prec_mult, prec_label = calc_precision_mult(hit_x, hit_y, zone["rect"])

            # Apply ammo flesh multiplier to effective base damage
            eff_base = base_damage
            if ammo_type and ammo_type in AMMO_TYPES:
                eff_base = base_damage * AMMO_TYPES[ammo_type]["flesh_mult"]

            damage = int(eff_base * zone["dmg_mult"] * prec_mult)

            # Check armor layer
            armor_absorbed = False
            armor_label = None
            if armor_hitboxes:
                for armor in armor_hitboxes:
                    if armor["rect"].collidepoint(hit_x, hit_y):
                        if ammo_type and ammo_type in AMMO_TYPES:
                            at = AMMO_TYPES[ammo_type]
                            armor_mult = at["vs_placas"] if "placas" in armor["armor_type"] else at["vs_chaleco"]
                        else:
                            # Generic reduction for rifle/shotgun
                            armor_mult = 1.0 - armor["dmg_reduction"]
                        damage = int(damage * armor_mult)
                        armor_absorbed = True
                        armor_label = armor["label"]
                        break

            return {
                "zone": name,
                "damage": damage,
                "dmg_mult": zone["dmg_mult"],
                "prec_mult": prec_mult,
                "prec_label": prec_label,
                "effect": zone["effect"],
                "color": zone["color"],
                "hit_x": hit_x,
                "hit_y": hit_y,
                "armor_absorbed": armor_absorbed,
                "armor_label": armor_label,
            }

    return {
        "zone": "MISS",
        "damage": 0,
        "dmg_mult": 0.0,
        "prec_mult": 0.0,
        "prec_label": "MISS",
        "effect": "FALLO TOTAL",
        "color": GRAZE_COLOR,
        "hit_x": hit_x,
        "hit_y": hit_y,
        "armor_absorbed": False,
        "armor_label": None,
    }


def fire_single_shot(aim_x, aim_y, weapon, hp_ratio, zones, consecutive_shots, is_first_shot=True, hand=1, armor_hitboxes=None, ammo_type=None):
    """Ejecuta un disparo: dispersión de 3 capas + resolución.
    L1 solo se aplica en el primer disparo de la ráfaga.
    Si el arma tiene pellets, genera múltiples impactos por perdigones.
    hand: 1 = diestro, -1 = zurdo (invierte X del recoil).
    Retorna result dict con hit_x, hit_y como punto de impacto principal."""
    pellet_count = weapon.get("pellets", 1)
    base_damage = weapon["base_damage"]

    # Para escopeta: recoil compartido entre perdigones del mismo disparo
    pattern = weapon.get("recoil_pattern", [])
    pattern_idx = consecutive_shots - 1
    if 0 <= pattern_idx < len(pattern):
        pat_dx, pat_dy = pattern[pattern_idx]
    elif pattern_idx >= len(pattern) and len(pattern) > 0:
        pat_dx, pat_dy = pattern[-1]
    else:
        pat_dx, pat_dy = 0, 0
    pellets = []
    first_layer_data = None
    # Para armas con perdigones, L1 siempre se aplica (es la dispersión del disparo, no la puntería)
    pellet_l1 = True if pellet_count > 1 else is_first_shot
    for i in range(pellet_count):
        fx, fy, layer_data = apply_three_layer_dispersion(
            aim_x, aim_y, weapon, hp_ratio, consecutive_shots, pellet_l1, hand
        )
        if pellet_count > 1 and i > 0:
            # Perdigones adicionales: usar L1+L2 propias, pero recoil compartido del patrón
            l2_x, l2_y = layer_data["l2_point"]
            spread = weapon.get("pattern_spread", 0)
            fx = max(GRID_ORIGIN_X, min(GRID_ORIGIN_X + GRID_W,
                     int(l2_x + (pat_dx * hand) + random.uniform(-spread, spread))))
            fy = max(GRID_ORIGIN_Y, min(GRID_ORIGIN_Y + GRID_H,
                     int(l2_y + pat_dy + random.uniform(-spread, spread))))

        hit = resolve_hit(fx, fy, zones, base_damage, armor_hitboxes, ammo_type)
        pellets.append(hit)
        if first_layer_data is None:
            first_layer_data = layer_data

    total_damage = sum(p["damage"] for p in pellets)
    best = max(pellets, key=lambda p: p["damage"])

    result = {
        "zone": best["zone"],
        "damage": total_damage,
        "dmg_mult": best["dmg_mult"],
        "prec_mult": best["prec_mult"],
        "prec_label": best["prec_label"],
        "effect": best["effect"],
        "color": best["color"],
        "hit_x": best["hit_x"],
        "hit_y": best["hit_y"],
        "aim_x": aim_x,
        "aim_y": aim_y,
        "disp_radius": first_layer_data["l1_radius"],
        "layer_data": first_layer_data,
        "pellets": pellets if pellet_count > 1 else None,
        "pellet_count": pellet_count,
        "armor_absorbed": best.get("armor_absorbed", False),
        "armor_label": best.get("armor_label"),
    }
    return result


def fire_burst(aim_x, aim_y, weapon, hp_ratio, zones, num_shots, hand=1, armor_hitboxes=None, ammo_type=None):
    """Ejecuta una ráfaga de num_shots disparos encadenados.
    El primer disparo parte del punto QTE (sin recoil).
    Cada disparo posterior parte del punto de impacto anterior con recoil creciente.
    hand: 1 = diestro, -1 = zurdo (invierte X del recoil).
    Retorna lista de results y daño total."""
    burst_results = []
    current_x, current_y = aim_x, aim_y

    for shot_idx in range(num_shots):
        result = fire_single_shot(current_x, current_y, weapon, hp_ratio, zones, shot_idx,
                                  is_first_shot=(shot_idx == 0), hand=hand,
                                  armor_hitboxes=armor_hitboxes, ammo_type=ammo_type)
        result["shot_number"] = shot_idx + 1
        burst_results.append(result)
        # El siguiente disparo parte del impacto de este
        current_x = result["hit_x"]
        current_y = result["hit_y"]

    return burst_results


# ─── Dibujo de barras QTE (externas) ─────────────────────

X_TRACK_Y = GRID_ORIGIN_Y - 28
X_TRACK_H = 16
Y_TRACK_X = GRID_ORIGIN_X + GRID_W + 12
Y_TRACK_W = 16


def _heartbeat_visual(base_color, beat_t):
    """Mezcla color base con rojo según intensidad del latido (0.0-1.0)."""
    if beat_t <= 0:
        return base_color
    r = int(base_color[0] + (220 - base_color[0]) * beat_t)
    g = int(base_color[1] * (1.0 - beat_t * 0.7))
    b = int(base_color[2] * (1.0 - beat_t * 0.7))
    return (min(255, r), max(0, g), max(0, b))


def draw_bar_x(surface, bar_x, locked=False, beat_t=0.0):
    x = int(bar_x)
    color = BAR_LOCKED_COLOR if locked else _heartbeat_visual(BAR_COLOR, beat_t)

    # Track pulsa rojo con el latido
    track_border = _heartbeat_visual((70, 90, 70), beat_t)
    track_bg = _heartbeat_visual((25, 30, 25), beat_t * 0.3)
    border_w = 1 + int(2 * beat_t)  # Borde se engrosa con el latido
    track_rect = pygame.Rect(GRID_ORIGIN_X, X_TRACK_Y, GRID_W, X_TRACK_H)
    pygame.draw.rect(surface, track_bg, track_rect, border_radius=3)
    pygame.draw.rect(surface, track_border, track_rect, border_w, border_radius=3)

    for i in range(0, GRID_W + 1, CELL_SIZE * 2):
        tick_x = GRID_ORIGIN_X + i
        pygame.draw.line(surface, (50, 60, 50),
                         (tick_x, X_TRACK_Y + X_TRACK_H - 3),
                         (tick_x, X_TRACK_Y + X_TRACK_H))

    # Marcador crece mucho con el latido
    grow = int(10 * beat_t)
    marker_w = 6 + grow
    marker_h = X_TRACK_H + grow
    marker_rect = pygame.Rect(x - marker_w // 2, X_TRACK_Y - grow // 2, marker_w, marker_h)
    pygame.draw.rect(surface, color, marker_rect, border_radius=2)

    # Glow rojo grande
    if beat_t > 0.05:
        glow_r = int(20 + 30 * beat_t)
        glow_surf = pygame.Surface((glow_r * 2, glow_r * 2), pygame.SRCALPHA)
        glow_alpha = int(160 * beat_t)
        pygame.draw.circle(glow_surf, (255, 30, 30, glow_alpha), (glow_r, glow_r), glow_r)
        surface.blit(glow_surf, (x - glow_r, X_TRACK_Y + X_TRACK_H // 2 - glow_r))

    if locked:
        ref_surf = pygame.Surface((2, GRID_H), pygame.SRCALPHA)
        ref_surf.fill((50, 255, 100, 50))
        surface.blit(ref_surf, (x - 1, GRID_ORIGIN_Y))


def draw_bar_y(surface, bar_y, locked=False, beat_t=0.0):
    y = int(bar_y)
    color = BAR_LOCKED_COLOR if locked else _heartbeat_visual(BAR_COLOR, beat_t)

    # Track pulsa rojo con el latido
    track_border = _heartbeat_visual((70, 90, 70), beat_t)
    track_bg = _heartbeat_visual((25, 30, 25), beat_t * 0.3)
    border_w = 1 + int(2 * beat_t)
    track_rect = pygame.Rect(Y_TRACK_X, GRID_ORIGIN_Y, Y_TRACK_W, GRID_H)
    pygame.draw.rect(surface, track_bg, track_rect, border_radius=3)
    pygame.draw.rect(surface, track_border, track_rect, border_w, border_radius=3)

    for i in range(0, GRID_H + 1, CELL_SIZE * 2):
        tick_y = GRID_ORIGIN_Y + i
        pygame.draw.line(surface, (50, 60, 50),
                         (Y_TRACK_X + Y_TRACK_W - 3, tick_y),
                         (Y_TRACK_X + Y_TRACK_W, tick_y))

    # Marcador crece mucho con el latido
    grow = int(10 * beat_t)
    marker_w = Y_TRACK_W + grow
    marker_h = 6 + grow
    marker_rect = pygame.Rect(Y_TRACK_X - grow // 2, y - marker_h // 2, marker_w, marker_h)
    pygame.draw.rect(surface, color, marker_rect, border_radius=2)

    # Glow rojo grande
    if beat_t > 0.05:
        glow_r = int(20 + 30 * beat_t)
        glow_surf = pygame.Surface((glow_r * 2, glow_r * 2), pygame.SRCALPHA)
        glow_alpha = int(160 * beat_t)
        pygame.draw.circle(glow_surf, (255, 30, 30, glow_alpha), (glow_r, glow_r), glow_r)
        surface.blit(glow_surf, (Y_TRACK_X + Y_TRACK_W // 2 - glow_r, y - glow_r))

    if locked:
        ref_surf = pygame.Surface((GRID_W, 2), pygame.SRCALPHA)
        ref_surf.fill((50, 255, 100, 50))
        surface.blit(ref_surf, (GRID_ORIGIN_X, y - 1))


def draw_impact_marker(surface, result):
    """Marca de impacto con círculo de dispersión y línea de desvío."""
    aim_x, aim_y = result.get("aim_x", result["hit_x"]), result.get("aim_y", result["hit_y"])
    disp_radius = result.get("disp_radius", 0)
    pellets = result.get("pellets")

    t = pygame.time.get_ticks()
    pulse = 1.0 + 0.3 * math.sin(t * 0.01)

    # Círculo de dispersión (centrado en la intención)
    if disp_radius > 0:
        r_int = int(disp_radius)
        disp_surf = pygame.Surface((r_int * 2 + 4, r_int * 2 + 4), pygame.SRCALPHA)
        pygame.draw.circle(disp_surf, (255, 255, 100, 30),
                           (r_int + 2, r_int + 2), r_int)
        pygame.draw.circle(disp_surf, (255, 255, 100, 80),
                           (r_int + 2, r_int + 2), r_int, 1)
        surface.blit(disp_surf, (aim_x - r_int - 2, aim_y - r_int - 2))

    # Punto de intención (cruz pequeña)
    pygame.draw.line(surface, (200, 200, 200), (aim_x - 4, aim_y), (aim_x + 4, aim_y), 1)
    pygame.draw.line(surface, (200, 200, 200), (aim_x, aim_y - 4), (aim_x, aim_y + 4), 1)

    if pellets:
        # Múltiples perdigones (escopeta)
        for p in pellets:
            px, py = p["hit_x"], p["hit_y"]
            # Punto de impacto pequeño
            pygame.draw.circle(surface, p["color"], (px, py), 4, 0)
            pygame.draw.circle(surface, IMPACT_COLOR, (px, py), 5, 1)
            # Línea desde intención
            pygame.draw.line(surface, (255, 100, 100, 100), (aim_x, aim_y), (px, py), 1)
    else:
        # Disparo único
        hit_x, hit_y = result["hit_x"], result["hit_y"]
        if aim_x != hit_x or aim_y != hit_y:
            pygame.draw.line(surface, (255, 100, 100, 150), (aim_x, aim_y), (hit_x, hit_y), 1)

        size = int(8 * pulse)
        pygame.draw.line(surface, IMPACT_COLOR,
                         (hit_x - size, hit_y - size), (hit_x + size, hit_y + size), 3)
        pygame.draw.line(surface, IMPACT_COLOR,
                         (hit_x - size, hit_y + size), (hit_x + size, hit_y - size), 3)
        pygame.draw.circle(surface, result["color"], (hit_x, hit_y), int(12 * pulse), 2)


# ─── Barra de tiempo ─────────────────────────────────────

def draw_timer_bar(surface, font, time_remaining, time_total):
    ratio = max(0.0, time_remaining / time_total)
    bar_w = GRID_W
    bar_h = 14
    bar_x = GRID_ORIGIN_X
    bar_y = GRID_ORIGIN_Y + GRID_H + 15

    pygame.draw.rect(surface, TIMER_BG_COLOR,
                     (bar_x, bar_y, bar_w, bar_h), border_radius=3)

    fill_color = TIMER_LOW_COLOR if ratio < 0.3 else TIMER_FILL_COLOR
    fill_w = int(bar_w * ratio)
    if fill_w > 0:
        pygame.draw.rect(surface, fill_color,
                         (bar_x, bar_y, fill_w, bar_h), border_radius=3)

    pygame.draw.rect(surface, (120, 120, 120),
                     (bar_x, bar_y, bar_w, bar_h), 1, border_radius=3)

    label = font.render(f"TIEMPO: {time_remaining / 1000:.1f}s", True,
                        TIMER_LOW_COLOR if ratio < 0.3 else TEXT_COLOR)
    surface.blit(label, (bar_x + bar_w // 2 - label.get_width() // 2, bar_y + bar_h + 2))


# ─── HUD ──────────────────────────────────────────────────

def draw_hud(surface, font, font_big, zones, state, result, weapon_name, weapon,
             player_hp, current_ammo, magazine_capacity, shots_fired,
             selecting_shots=0, burst_results=None, operator_hand=1,
             armor_config_index=0, ammo_type=None):
    hud_x = 10
    hud_y = GRID_ORIGIN_Y

    # Panel izquierdo
    pygame.draw.rect(surface, (20, 20, 30),
                     pygame.Rect(5, GRID_ORIGIN_Y - 5, 230, GRID_H + 10), border_radius=4)
    pygame.draw.rect(surface, (60, 60, 80),
                     pygame.Rect(5, GRID_ORIGIN_Y - 5, 230, GRID_H + 10), 1, border_radius=4)

    state_labels = {
        STATE_IDLE: ("LISTO", (100, 200, 100)),
        STATE_QTE_Y: ("vvv EJE Y vvv", BAR_COLOR),
        STATE_QTE_X: (">>> EJE X >>>", BAR_COLOR),
        STATE_RESULT: ("IMPACTO", IMPACT_COLOR),
        STATE_SELECT_SHOTS: ("SELECCIONAR BALAS", HIGHLIGHT_COLOR),
        STATE_RELOAD_SELECT: ("RECARGAR", (180, 180, 80)),
    }
    label, color = state_labels.get(state, ("", TEXT_COLOR))
    text = font_big.render(label, True, color)
    surface.blit(text, (hud_x + 5, hud_y))

    y = hud_y + 35

    # Barra de HP siempre visible
    hp_ratio = player_hp / PLAYER_MAX_HP
    hp_bar_w = 180
    hp_bar_h = 12
    pygame.draw.rect(surface, (40, 40, 40), (hud_x + 5, y, hp_bar_w, hp_bar_h), border_radius=2)
    hp_fill_w = int(hp_bar_w * hp_ratio)
    hp_color = HP_LOW_COLOR if hp_ratio < 0.3 else HP_COLOR
    if hp_fill_w > 0:
        pygame.draw.rect(surface, hp_color, (hud_x + 5, y, hp_fill_w, hp_bar_h), border_radius=2)
    pygame.draw.rect(surface, (100, 100, 100), (hud_x + 5, y, hp_bar_w, hp_bar_h), 1, border_radius=2)
    hp_text = font.render(f"HP: {player_hp}/{PLAYER_MAX_HP}", True, hp_color)
    surface.blit(hp_text, (hud_x + hp_bar_w + 10, y - 1))
    y += 22

    # Arma actual + mano dominante + tipo de municion
    hand_str = "D" if operator_hand == 1 else "Z"
    hand_color = (100, 200, 100) if operator_hand == 1 else (100, 180, 255)
    wep_text = font_big.render(weapon_name, True, (220, 200, 140))
    surface.blit(wep_text, (hud_x + 5, y))
    hand_tag = font.render(f"[{hand_str}]", True, hand_color)
    surface.blit(hand_tag, (hud_x + 10 + wep_text.get_width(), y + 4))
    if ammo_type and ammo_type in AMMO_TYPES:
        at = AMMO_TYPES[ammo_type]
        ammo_tag = font.render(f"[{at['short']}]", True, at["color"])
        surface.blit(ammo_tag, (hud_x + 14 + wep_text.get_width() + hand_tag.get_width(), y + 4))
    y += 25

    disp_radius = weapon["dispersion_base"] * (1.0 + (1.0 - hp_ratio) * (DISPERSION_HP_FACTOR - 1.0))
    stats_lines = [
        f"Daño base: {weapon['base_damage']}",
        f"Dispersion: {weapon['dispersion_base']}px (base)",
        f"Dispersion actual: {disp_radius:.0f}px",
        f"Velocidad: {weapon['bar_speed_x']}/{weapon['bar_speed_y']}",
    ]
    for line in stats_lines:
        text = font.render(line, True, TEXT_COLOR)
        surface.blit(text, (hud_x + 5, y))
        y += 16
    y += 4

    # --- Cargador visual ---
    ammo_color = HIGHLIGHT_COLOR if current_ammo > 0 else (220, 60, 60)
    ammo_text = font.render(f"CARGADOR: {current_ammo}/{magazine_capacity}", True, ammo_color)
    surface.blit(ammo_text, (hud_x + 5, y))
    y += 16

    # Balas como rectángulos — color según tipo de municion cargada
    bullet_w, bullet_h = 6, 14
    bullet_spacing = 8
    max_bullets_per_row = 20
    bullet_color = AMMO_TYPES[ammo_type]["color"] if ammo_type and ammo_type in AMMO_TYPES else (220, 200, 60)
    bullet_highlight = tuple(min(255, c + 35) for c in bullet_color)
    for i in range(magazine_capacity):
        row = i // max_bullets_per_row
        col = i % max_bullets_per_row
        bx = hud_x + 5 + col * bullet_spacing
        by = y + row * (bullet_h + 3)
        if i < current_ammo:
            pygame.draw.rect(surface, bullet_color, (bx, by, bullet_w, bullet_h), border_radius=1)
            pygame.draw.rect(surface, bullet_highlight, (bx, by, bullet_w, bullet_h), 1, border_radius=1)
        else:
            pygame.draw.rect(surface, (50, 50, 50), (bx, by, bullet_w, bullet_h), border_radius=1)
    bullet_rows = (magazine_capacity - 1) // max_bullets_per_row + 1
    y += bullet_rows * (bullet_h + 3) + 4

    # --- Indicador de recoil ---
    recoil_blocks = min(shots_fired, 10)
    recoil_label = "RECOIL: "
    for i in range(10):
        if i < recoil_blocks:
            recoil_label += "■"
        else:
            recoil_label += "□"
    # Color degrada de verde a rojo
    if shots_fired == 0:
        recoil_color = (100, 200, 100)
    elif shots_fired <= 3:
        recoil_color = (200, 200, 60)
    elif shots_fired <= 6:
        recoil_color = (220, 140, 40)
    else:
        recoil_color = (220, 60, 60)
    text = font.render(recoil_label, True, recoil_color)
    surface.blit(text, (hud_x + 5, y))
    y += 16

    # Indicador de proteccion del enemigo
    armor_name = ARMOR_CONFIGS[armor_config_index]["name"]
    armor_color = (150, 150, 200) if armor_name != "Sin proteccion" else (100, 100, 100)
    text = font.render(f"PROTECCION: {armor_name}", True, armor_color)
    surface.blit(text, (hud_x + 5, y))
    y += 16

    # Cargador vacío: mensaje pulsante
    if current_ammo == 0:
        t = pygame.time.get_ticks()
        pulse_alpha = int(180 + 75 * math.sin(t * 0.008))
        empty_color = (220, 60, 60)
        text = font_big.render("¡CARGADOR VACIO!", True, empty_color)
        text.set_alpha(pulse_alpha)
        surface.blit(text, (hud_x + 5, y))
        y += 22
        text = font.render("[R] RECARGAR", True, HIGHLIGHT_COLOR)
        surface.blit(text, (hud_x + 5, y))
        y += 16
    y += 4

    if state == STATE_IDLE:
        controls = [
            "[SPACE] Disparar  [R] Recargar",
            "[1] P229  [2] MP5  [3] M4  [4] Mk18",
            "[UP/DOWN] Vida +/-  [A] Armadura",
            "[H] Mano  [P] Patron Recoil  [Z] Zonas",
            "[ESC] Salir",
        ]
        for line in controls:
            text = font.render(line, True, (140, 140, 140))
            surface.blit(text, (hud_x + 5, y))
            y += 16

    elif state == STATE_RELOAD_SELECT:
        current_type = ammo_type if ammo_type else "RIP"
        at = AMMO_TYPES[current_type]
        text = font_big.render(f"MUNICION: < {at['short']} >", True, at["color"])
        surface.blit(text, (hud_x + 5, y))
        y += 28
        text = font.render("[LEFT/RIGHT] Tipo", True, (140, 140, 140))
        surface.blit(text, (hud_x + 5, y))
        y += 16
        text = font.render("[SPACE] Confirmar recarga", True, (140, 140, 140))
        surface.blit(text, (hud_x + 5, y))
        y += 16
        text = font.render("[ESC] Cancelar", True, (140, 140, 140))
        surface.blit(text, (hud_x + 5, y))

    elif state == STATE_SELECT_SHOTS:
        text = font_big.render(f"DISPAROS: < {selecting_shots} >", True, HIGHLIGHT_COLOR)
        surface.blit(text, (hud_x + 5, y))
        y += 28
        text = font.render("[LEFT/RIGHT] Cantidad", True, (140, 140, 140))
        surface.blit(text, (hud_x + 5, y))
        y += 16
        text = font.render("[SPACE] Confirmar", True, (140, 140, 140))
        surface.blit(text, (hud_x + 5, y))

    elif state == STATE_QTE_Y:
        text = font.render("[SPACE] fijar eje Y", True, HIGHLIGHT_COLOR)
        surface.blit(text, (hud_x + 5, y))

    elif state == STATE_QTE_X:
        text = font.render("[SPACE] fijar eje X", True, HIGHLIGHT_COLOR)
        surface.blit(text, (hud_x + 5, y))

    elif state == STATE_RESULT and burst_results:
        total_dmg = sum(br["damage"] for br in burst_results)
        num_shots = len(burst_results)

        # Título de ráfaga
        header = font_big.render(f"RAFAGA: {num_shots} DISPARO{'S' if num_shots > 1 else ''}", True, HIGHLIGHT_COLOR)
        surface.blit(header, (hud_x + 5, y))
        y += 24

        dmg_text = font_big.render(f"{total_dmg} DMG TOTAL", True, WHITE)
        surface.blit(dmg_text, (hud_x + 5, y))
        y += 24

        # Resumen por disparo
        for i, br in enumerate(burst_results):
            shot_num = br.get("shot_number", i + 1)
            zone_name = br["zone"]
            dmg = br["damage"]
            zcolor = br["color"]
            ld = br.get("layer_data")
            recoil_info = f" R:{ld['vertical_recoil']:.0f}px" if ld else ""
            text = font.render(f"  #{shot_num} {zone_name}: {dmg} DMG{recoil_info}", True, zcolor)
            surface.blit(text, (hud_x + 5, y))
            y += 15

        # Zonas impactadas
        y += 4
        zone_hits = {}
        for br in burst_results:
            z = br["zone"]
            zone_hits[z] = zone_hits.get(z, 0) + 1
        for z, count in zone_hits.items():
            zc = next((br["color"] for br in burst_results if br["zone"] == z), TEXT_COLOR)
            text = font.render(f"  {z}: {count}x", True, zc)
            surface.blit(text, (hud_x + 5, y))
            y += 15

    # Panel derecho: zonas de impacto
    panel_x = Y_TRACK_X + Y_TRACK_W + 10
    panel_y = GRID_ORIGIN_Y
    pygame.draw.rect(surface, (20, 20, 30),
                     pygame.Rect(panel_x - 5, panel_y - 5, 225, 280), border_radius=4)
    pygame.draw.rect(surface, (60, 60, 80),
                     pygame.Rect(panel_x - 5, panel_y - 5, 225, 280), 1, border_radius=4)

    title = font.render("ZONAS DE IMPACTO", True, WHITE)
    surface.blit(title, (panel_x, panel_y))

    zy = panel_y + 25
    for name, zone in zones.items():
        pygame.draw.rect(surface, zone["color"], (panel_x, zy + 2, 10, 10))
        text = font.render(f"{name} ({zone['dmg_mult']}x)", True, zone["color"])
        surface.blit(text, (panel_x + 15, zy))
        zy += 20

    pygame.draw.rect(surface, GRAZE_COLOR, (panel_x, zy + 2, 10, 10))
    text = font.render("FUERA (0.5x)", True, GRAZE_COLOR)
    surface.blit(text, (panel_x + 15, zy))
    zy += 30

    title2 = font.render("PRECISION", True, WHITE)
    surface.blit(title2, (panel_x, zy))
    zy += 20
    for line in ["Centro perfecto: 1.5x", "Impacto solido:  1.0x",
                 "Borde de zona:   0.75x", "Grazing shot:    0.5x"]:
        text = font.render(line, True, (140, 140, 140))
        surface.blit(text, (panel_x, zy))
        zy += 16


def simulate_recoil_heatmap(weapon, num_magazines=100, hand=1):
    """Simula num_magazines cargadores completos encadenando disparos.
    Retorna lista de (dx, dy) offsets relativos al origen para cada disparo.
    Usa las 3 capas de dispersión a 100% HP.
    hand: 1 = diestro, -1 = zurdo (invierte X del recoil)."""
    points_per_shot = {}  # shot_idx -> list of (dx, dy)
    magazine_cap = weapon["magazine_capacity"]
    pattern = weapon.get("recoil_pattern", [])
    spread = weapon.get("pattern_spread", 0)
    dev = weapon["weapon_deviation"]
    disp_base = weapon["dispersion_base"]  # L1 a 100% HP = radio base (mínimo)

    for _ in range(num_magazines):
        # Cada cargador: disparos encadenados desde el punto anterior
        cum_x, cum_y = 0.0, 0.0
        for shot_idx in range(magazine_cap):
            # L1: dispersión base — SOLO primer disparo
            if shot_idx == 0:
                angle = random.uniform(0, 2 * math.pi)
                r = disp_base * math.sqrt(random.random())
                l1_dx = r * math.cos(angle)
                l1_dy = r * math.sin(angle)
            else:
                l1_dx, l1_dy = 0.0, 0.0

            # L2: desviación mecánica (siempre)
            l2_dx = random.uniform(-dev, dev)
            l2_dy = random.uniform(-dev, dev)

            # L3: patrón + spread
            pat_idx = shot_idx - 1  # Disparo 0 no tiene recoil
            if 0 <= pat_idx < len(pattern):
                pat_dx, pat_dy = pattern[pat_idx]
            elif pat_idx >= len(pattern) and len(pattern) > 0:
                pat_dx, pat_dy = pattern[-1]
            else:
                pat_dx, pat_dy = 0, 0

            recoil_dx = (pat_dx * hand) + random.uniform(-spread, spread)
            recoil_dy = pat_dy + random.uniform(-spread, spread)

            # Acumular (encadenando desde impacto anterior)
            cum_x += l1_dx + l2_dx + recoil_dx
            cum_y += l1_dy + l2_dy + recoil_dy

            if shot_idx not in points_per_shot:
                points_per_shot[shot_idx] = []
            points_per_shot[shot_idx].append((cum_x, cum_y))

    return points_per_shot


# Caché para no recalcular cada frame (se invalida al cambiar patrones o presionar F5)
_heatmap_cache = {}


def get_heatmap_data(weapon_name, hand=1):
    """Obtiene datos de heatmap cacheados. Genera si no existen."""
    cache_key = (weapon_name, hand)
    if cache_key not in _heatmap_cache:
        _heatmap_cache[cache_key] = simulate_recoil_heatmap(WEAPONS[weapon_name], 100, hand)
    return _heatmap_cache[cache_key]


def invalidate_heatmap_cache():
    """Limpia la caché para regenerar datos."""
    _heatmap_cache.clear()


def draw_recoil_pattern_panel(surface, font, font_big, current_weapon_idx, hand=1):
    """Panel overlay con mapa de calor de 100 cargadores simulados por arma."""
    # Fondo semi-transparente
    overlay = pygame.Surface((SCREEN_W, SCREEN_H), pygame.SRCALPHA)
    overlay.fill((0, 0, 0, 210))
    surface.blit(overlay, (0, 0))

    # Título
    hand_label = "DIESTRO" if hand == 1 else "ZURDO"
    title = font_big.render(f"PATRONES DE RECOIL — 100 cargadores ({hand_label})", True, HIGHLIGHT_COLOR)
    surface.blit(title, (SCREEN_W // 2 - title.get_width() // 2, 10))
    subtitle = font.render("[P] Cerrar    [F5] Regenerar    [H] Cambiar mano", True, (140, 140, 140))
    surface.blit(subtitle, (SCREEN_W // 2 - subtitle.get_width() // 2, 32))

    # Cuatro columnas
    col_w = SCREEN_W // 4
    panel_top = 50
    panel_h = SCREEN_H - 65

    weapon_colors = [
        (220, 200, 140),  # P229
        (100, 220, 180),  # MP5
        (255, 120, 80),   # Benelli M4
        (100, 180, 255),  # Mk18
    ]

    for w_idx, w_name in enumerate(WEAPON_NAMES):
        weapon = WEAPONS[w_name]
        pattern = weapon.get("recoil_pattern", [])
        col_x = w_idx * col_w
        cx = col_x + col_w // 2
        wcolor = weapon_colors[w_idx]

        # Fondo de columna
        col_rect = pygame.Rect(col_x + 4, panel_top, col_w - 8, panel_h)
        border_color = wcolor if w_idx == current_weapon_idx else (50, 50, 60)
        border_w = 2 if w_idx == current_weapon_idx else 1
        pygame.draw.rect(surface, (12, 12, 18), col_rect, border_radius=4)
        pygame.draw.rect(surface, border_color, col_rect, border_w, border_radius=4)

        # Nombre + stats
        name_color = wcolor if w_idx == current_weapon_idx else (140, 140, 140)
        name_text = font_big.render(w_name, True, name_color)
        surface.blit(name_text, (cx - name_text.get_width() // 2, panel_top + 6))

        info = f"Spread:{weapon.get('pattern_spread', 0)}  Dev:{weapon['weapon_deviation']}  DispBase:{weapon['dispersion_base']}  Mag:{weapon['magazine_capacity']}"
        info_text = font.render(info, True, (100, 100, 100))
        surface.blit(info_text, (cx - info_text.get_width() // 2, panel_top + 28))

        # Obtener datos simulados
        points_per_shot = get_heatmap_data(w_name, hand)

        # Recolectar todos los puntos para calcular bounds
        all_points = []
        for shot_pts in points_per_shot.values():
            all_points.extend(shot_pts)

        if not all_points:
            continue

        # Calcular rango
        all_x = [p[0] for p in all_points]
        all_y = [p[1] for p in all_points]
        min_x, max_x = min(all_x), max(all_x)
        min_y, max_y = min(all_y), max(all_y)
        range_x = max(max_x - min_x, 1)
        range_y = max(max_y - min_y, 1)

        # Área de dibujo del heatmap
        draw_area_x = col_x + 15
        draw_area_y = panel_top + 44
        draw_area_w = col_w - 30
        draw_area_h = panel_h - 60

        # Escala para mapear puntos al área de dibujo
        scale_x = draw_area_w / range_x
        scale_y = draw_area_h / range_y
        scale = min(scale_x, scale_y) * 0.85  # Margen

        # Centro del área de dibujo
        area_cx = draw_area_x + draw_area_w // 2
        area_cy = draw_area_y + draw_area_h // 2

        # El centroide del patrón (para centrar correctamente)
        center_data_x = (min_x + max_x) / 2
        center_data_y = (min_y + max_y) / 2

        # Construir grilla de densidad para heatmap
        cell = 4  # Tamaño de celda del heatmap en píxeles
        grid_cols = draw_area_w // cell + 1
        grid_rows = draw_area_h // cell + 1
        density = {}

        for dx, dy in all_points:
            # Mapear a posición en pantalla
            sx = area_cx + (dx - center_data_x) * scale
            sy = area_cy + (dy - center_data_y) * scale  # Ya en coords de pantalla

            # Coordenada de celda
            gc = int((sx - draw_area_x) / cell)
            gr = int((sy - draw_area_y) / cell)
            if 0 <= gc < grid_cols and 0 <= gr < grid_rows:
                key = (gc, gr)
                density[key] = density.get(key, 0) + 1

        # Encontrar densidad máxima para normalizar
        max_density = max(density.values()) if density else 1

        # Dibujar celdas del heatmap
        heatmap_surf = pygame.Surface((draw_area_w, draw_area_h), pygame.SRCALPHA)
        for (gc, gr), count in density.items():
            # Normalizar 0-1
            t = count / max_density
            # Gradiente: negro → azul → cian → amarillo → blanco
            if t < 0.25:
                s = t / 0.25
                r, g, b = 0, 0, int(120 * s)
            elif t < 0.5:
                s = (t - 0.25) / 0.25
                r, g, b = 0, int(180 * s), 120 + int(60 * s)
            elif t < 0.75:
                s = (t - 0.5) / 0.25
                r, g, b = int(255 * s), 180 + int(40 * s), int(180 * (1 - s))
            else:
                s = (t - 0.75) / 0.25
                r, g, b = 255, 220 + int(35 * s), int(200 * s)
            alpha = int(60 + 195 * t)
            pygame.draw.rect(heatmap_surf, (r, g, b, alpha),
                             (gc * cell, gr * cell, cell, cell))

        surface.blit(heatmap_surf, (draw_area_x, draw_area_y))

        # Dibujar el patrón ideal (línea central) encima del heatmap
        origin_sx = area_cx + (0 - center_data_x) * scale
        origin_sy = area_cy + (0 - center_data_y) * scale

        # Cruz de origen (disparo 1)
        ox, oy = int(origin_sx), int(origin_sy)
        pygame.draw.line(surface, (100, 255, 100), (ox - 6, oy), (ox + 6, oy), 2)
        pygame.draw.line(surface, (100, 255, 100), (ox, oy - 6), (ox, oy + 6), 2)

        # Línea del patrón ideal (hand invierte X)
        prev_px, prev_py = ox, oy
        cum_dx, cum_dy = 0, 0
        for p_idx, (pdx, pdy) in enumerate(pattern):
            cum_dx += pdx * hand
            cum_dy += pdy
            px = int(area_cx + (cum_dx - center_data_x) * scale)
            py = int(area_cy + (cum_dy - center_data_y) * scale)

            # Línea de conexión (blanca semi-transparente)
            pygame.draw.line(surface, (255, 255, 255), (prev_px, prev_py), (px, py), 1)

            # Punto del patrón ideal
            pygame.draw.circle(surface, (255, 255, 255), (px, py), 3)

            # Número de disparo
            shot_label = font.render(str(p_idx + 2), True, (255, 255, 255))
            surface.blit(shot_label, (px + 5, py - 5))

            prev_px, prev_py = px, py

        # Etiqueta del disparo 1
        lbl1 = font.render("1", True, (100, 255, 100))
        surface.blit(lbl1, (ox + 8, oy - 5))


def draw_title_bar(surface, font, weapon_name):
    title_bg = pygame.Rect(0, 0, SCREEN_W, 35)
    pygame.draw.rect(surface, (15, 15, 25), title_bg)
    title = font.render(f"CRIMSON DRAFT — QTE Prototype — {weapon_name}", True, (180, 60, 60))
    surface.blit(title, (10, 8))


# ─── Main ─────────────────────────────────────────────────

def main():
    pygame.init()
    screen = pygame.display.set_mode((SCREEN_W, SCREEN_H))
    pygame.display.set_caption("Crimson Draft - QTE Bidimensional")
    clock = pygame.time.Clock()

    font = pygame.font.SysFont("Consolas", 14)
    font_big = pygame.font.SysFont("Consolas", 20, bold=True)

    # SFX
    sounds = init_sounds()

    zones = get_hitboxes()
    show_zones = True

    # Arma y vida
    weapon_idx = 0
    player_hp = PLAYER_MAX_HP
    show_pattern_panel = False

    # Cargador
    ammo = {name: WEAPONS[name]["magazine_capacity"] for name in WEAPON_NAMES}

    # Mano dominante del operador: 1 = diestro, -1 = zurdo
    operator_hand = 1

    # Proteccion del enemigo
    armor_config_index = 0
    current_armor_hitboxes = get_armor_hitboxes(zones, armor_config_index)

    # Tipo de municion de la pistola
    nine_mm_ammo_type = "RIP"

    # Ráfaga
    selected_shots = 1       # Cantidad seleccionada por el jugador
    burst_results = []       # Resultados de todos los disparos de la ráfaga

    # Estado QTE
    heartbeat_playing = False
    state = STATE_IDLE
    bar_x = float(GRID_ORIGIN_X)
    bar_y = float(GRID_ORIGIN_Y)
    bar_dir_x = 1
    bar_dir_y = 1
    locked_x = None
    locked_y = None
    result = None
    result_time = 0
    qte_start_time = 0

    running = True
    while running:
        weapon_name = WEAPON_NAMES[weapon_idx]
        weapon = WEAPONS[weapon_name]
        hp_ratio = player_hp / PLAYER_MAX_HP

        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                running = False

            elif event.type == pygame.KEYDOWN:
                if event.key == pygame.K_ESCAPE:
                    if state == STATE_RELOAD_SELECT:
                        state = STATE_IDLE
                    else:
                        running = False

                elif event.key == pygame.K_z:
                    show_zones = not show_zones

                elif event.key == pygame.K_p:
                    show_pattern_panel = not show_pattern_panel

                elif event.key == pygame.K_F5 and show_pattern_panel:
                    invalidate_heatmap_cache()

                elif event.key == pygame.K_h and state == STATE_IDLE:
                    operator_hand *= -1
                    invalidate_heatmap_cache()

                elif event.key == pygame.K_r and state == STATE_IDLE:
                    # Recargar: pistola 9mm entra en seleccion de tipo, otras recargan directo
                    if ammo[weapon_name] < weapon["magazine_capacity"]:
                        if weapon["caliber"] == "9mm":
                            state = STATE_RELOAD_SELECT
                        else:
                            ammo[weapon_name] = weapon["magazine_capacity"]
                            result = None
                            burst_results = []
                            sounds["reload"].play()

                elif event.key == pygame.K_a and state == STATE_IDLE:
                    # Ciclar configuracion de proteccion del enemigo
                    armor_config_index = (armor_config_index + 1) % len(ARMOR_CONFIGS)
                    current_armor_hitboxes = get_armor_hitboxes(zones, armor_config_index)
                    sounds["weapon_switch"].play()

                # Cambio de arma (solo en IDLE)
                elif event.key == pygame.K_1 and state == STATE_IDLE:
                    if weapon_idx != 0:
                        weapon_idx = 0
                        sounds["weapon_switch"].play()
                elif event.key == pygame.K_2 and state == STATE_IDLE:
                    if weapon_idx != 1:
                        weapon_idx = 1
                        sounds["weapon_switch"].play()
                elif event.key == pygame.K_3 and state == STATE_IDLE:
                    if weapon_idx != 2:
                        weapon_idx = 2
                        sounds["weapon_switch"].play()
                elif event.key == pygame.K_4 and state == STATE_IDLE:
                    if weapon_idx != 3:
                        weapon_idx = 3
                        sounds["weapon_switch"].play()

                # Cambio de HP
                elif event.key == pygame.K_UP:
                    player_hp = min(PLAYER_MAX_HP, player_hp + 10)
                elif event.key == pygame.K_DOWN:
                    player_hp = max(10, player_hp - 10)

                elif event.key == pygame.K_SPACE:
                    if state == STATE_IDLE and ammo[weapon_name] > 0:
                        # Entrar a selección de cantidad de disparos
                        selected_shots = 1
                        state = STATE_SELECT_SHOTS
                        result = None
                        burst_results = []
                    elif state == STATE_IDLE and ammo[weapon_name] <= 0:
                        sounds["empty"].play()

                    elif state == STATE_RELOAD_SELECT:
                        # Confirmar recarga con tipo de municion seleccionado
                        ammo[weapon_name] = weapon["magazine_capacity"]
                        result = None
                        burst_results = []
                        sounds["reload"].play()
                        state = STATE_IDLE

                    elif state == STATE_SELECT_SHOTS:
                        # Confirmar selección → iniciar QTE
                        state = STATE_QTE_Y
                        bar_y = float(GRID_ORIGIN_Y + GRID_H)  # Inicia abajo
                        bar_dir_y = -1
                        locked_x = None
                        locked_y = None
                        qte_start_time = pygame.time.get_ticks()

                    elif state == STATE_QTE_Y:
                        # Vibración tipo latido afecta la posición al bloquear
                        vib_off = heartbeat_offset(pygame.time.get_ticks(), hp_ratio)
                        locked_y = max(GRID_ORIGIN_Y, min(GRID_ORIGIN_Y + GRID_H, int(bar_y + vib_off)))
                        state = STATE_QTE_X
                        bar_x = float(GRID_ORIGIN_X)  # Inicia a la izquierda
                        bar_dir_x = 1
                        qte_start_time = pygame.time.get_ticks()
                        sounds["lock_y"].play()

                    elif state == STATE_QTE_X:
                        vib_off = heartbeat_offset(pygame.time.get_ticks(), hp_ratio)
                        locked_x = max(GRID_ORIGIN_X, min(GRID_ORIGIN_X + GRID_W, int(bar_x + vib_off)))
                        # Ejecutar ráfaga completa desde el punto QTE
                        ammo_t = nine_mm_ammo_type if weapon["caliber"] == "9mm" else None
                        burst_results = fire_burst(locked_x, locked_y, weapon, hp_ratio, zones, selected_shots, operator_hand,
                                                   armor_hitboxes=current_armor_hitboxes, ammo_type=ammo_t)
                        ammo[weapon_name] -= selected_shots
                        result = burst_results[-1]  # Último disparo como representativo
                        result_time = pygame.time.get_ticks()
                        state = STATE_RESULT
                        sounds["lock_x"].play()
                        sounds[weapon["sfx"]].play()
                        play_hit_sfx(sounds, result)

                # Selector de tipo de municion en STATE_RELOAD_SELECT
                elif state == STATE_RELOAD_SELECT:
                    if event.key in (pygame.K_LEFT, pygame.K_RIGHT):
                        nine_mm_ammo_type = "FMJ" if nine_mm_ammo_type == "RIP" else "RIP"
                        sounds["select_shot"].play()

                # Selector de cantidad en STATE_SELECT_SHOTS
                elif state == STATE_SELECT_SHOTS:
                    max_selectable = ammo[weapon_name]
                    if event.key == pygame.K_RIGHT:
                        prev = selected_shots
                        selected_shots = min(selected_shots + 1, max_selectable)
                        if selected_shots != prev:
                            sounds["select_shot"].play()
                    elif event.key == pygame.K_LEFT:
                        prev = selected_shots
                        selected_shots = max(1, selected_shots - 1)
                        if selected_shots != prev:
                            sounds["select_shot"].play()

        # --- Actualizar barras ---
        now = pygame.time.get_ticks()
        qte_elapsed = now - qte_start_time

        # Vibración tipo latido: amplitud y BPM escalan con vida perdida
        vibration_offset = heartbeat_offset(now, hp_ratio)
        beat_t = heartbeat_visual_t(now, hp_ratio)

        # SFX de heartbeat sincronizado con visual
        if beat_t > 0.5 and not heartbeat_playing:
            heartbeat_playing = True
            _, damage_ratio_sfx = _heartbeat_phase_intensity(now, hp_ratio)
            if damage_ratio_sfx > 0.1:
                vol = min(0.4, damage_ratio_sfx * 0.5)
                sounds["heartbeat_lub"].set_volume(vol)
                sounds["heartbeat_lub"].play()
        elif beat_t < 0.1:
            heartbeat_playing = False

        if state == STATE_QTE_Y:
            bar_y += weapon["bar_speed_y"] * bar_dir_y
            if bar_y >= GRID_ORIGIN_Y + GRID_H:
                bar_y = float(GRID_ORIGIN_Y + GRID_H)
                bar_dir_y = -1
            elif bar_y <= GRID_ORIGIN_Y:
                bar_y = float(GRID_ORIGIN_Y)
                bar_dir_y = 1

            if qte_elapsed >= QTE_TIME_LIMIT:
                locked_y = int(bar_y + vibration_offset)
                locked_y = max(GRID_ORIGIN_Y, min(GRID_ORIGIN_Y + GRID_H, locked_y))
                state = STATE_QTE_X
                bar_x = float(GRID_ORIGIN_X)
                bar_dir_x = 1
                qte_start_time = now
                sounds["timeout"].play()

        elif state == STATE_QTE_X:
            bar_x += weapon["bar_speed_x"] * bar_dir_x
            if bar_x >= GRID_ORIGIN_X + GRID_W:
                bar_x = float(GRID_ORIGIN_X + GRID_W)
                bar_dir_x = -1
            elif bar_x <= GRID_ORIGIN_X:
                bar_x = float(GRID_ORIGIN_X)
                bar_dir_x = 1

            if qte_elapsed >= QTE_TIME_LIMIT:
                locked_x = max(GRID_ORIGIN_X, min(GRID_ORIGIN_X + GRID_W, int(bar_x + vibration_offset)))
                ammo_t = nine_mm_ammo_type if weapon["caliber"] == "9mm" else None
                burst_results = fire_burst(locked_x, locked_y, weapon, hp_ratio, zones, selected_shots, operator_hand,
                                           armor_hitboxes=current_armor_hitboxes, ammo_type=ammo_t)
                ammo[weapon_name] -= selected_shots
                result = burst_results[-1]
                result_time = now
                state = STATE_RESULT
                sounds["timeout"].play()
                sounds[weapon["sfx"]].play()
                play_hit_sfx(sounds, result)

        burst_display_time = RESULT_DISPLAY_TIME + len(burst_results) * 500
        if state == STATE_RESULT and now - result_time > burst_display_time:
            state = STATE_IDLE

        # --- Render ---
        screen.fill(DARK_GRAY)

        draw_title_bar(screen, font, weapon_name)

        # Distracciones durante QTE
        in_qte = state in (STATE_QTE_Y, STATE_QTE_X)
        if in_qte:
            shake_x, shake_y = get_screen_shake(now, hp_ratio)
            damage_ratio = 1.0 - hp_ratio
            ghost_off = int(GHOST_MAX_OFFSET * damage_ratio) if damage_ratio >= 0.15 else 0
        else:
            shake_x, shake_y = 0, 0
            ghost_off = 0

        draw_grid(screen, shake_x, shake_y, ghost_off, now)

        # Silueta: flicker a vida baja durante QTE
        show_sil = not (in_qte and should_flicker_silhouette(now, hp_ratio))
        if show_sil:
            draw_silhouette(screen, shake_x, shake_y)
            draw_hitboxes(screen, zones, show_zones, shake_x, shake_y)
        else:
            draw_hitboxes(screen, zones, show_zones, shake_x, shake_y)

        # Armor hitboxes superpuestas sobre hitboxes corporales
        if show_zones:
            draw_armor_hitboxes(screen, current_armor_hitboxes, shake_x, shake_y)

        # Blood vignette + static noise (solo durante QTE)
        if in_qte:
            draw_blood_vignette(screen, hp_ratio, now)
            draw_static_noise(screen, hp_ratio, now)

        # Flash rojo en el borde de la grilla con cada latido
        if beat_t > 0.05 and in_qte:
            grid_rect = pygame.Rect(GRID_ORIGIN_X - 2, GRID_ORIGIN_Y - 2, GRID_W + 4, GRID_H + 4)
            flash_alpha = int(200 * beat_t)
            flash_w = 2 + int(3 * beat_t)
            flash_surf = pygame.Surface((GRID_W + 4, GRID_H + 4), pygame.SRCALPHA)
            pygame.draw.rect(flash_surf, (255, 30, 30, flash_alpha), (0, 0, GRID_W + 4, GRID_H + 4), flash_w, border_radius=2)
            screen.blit(flash_surf, grid_rect.topleft)

        # Barra Y — se mueve primero (vibra y pulsa rojo por vida baja)
        if state == STATE_QTE_Y:
            draw_bar_y(screen, bar_y + vibration_offset, locked=False, beat_t=beat_t)
        elif state in (STATE_QTE_X, STATE_RESULT) and locked_y is not None:
            draw_bar_y(screen, locked_y, locked=True)

        # Barra X — se mueve segundo (vibra y pulsa rojo por vida baja)
        if state == STATE_QTE_X:
            draw_bar_x(screen, bar_x + vibration_offset, locked=False, beat_t=beat_t)
        elif state == STATE_RESULT and locked_x is not None:
            draw_bar_x(screen, locked_x, locked=True)

        # Marcas de impacto de la ráfaga
        if state == STATE_RESULT and burst_results:
            t = pygame.time.get_ticks()
            pulse = 1.0 + 0.3 * math.sin(t * 0.01)

            # Dibujar línea encadenada entre todos los impactos
            for i in range(len(burst_results)):
                br = burst_results[i]
                hx, hy = br["hit_x"], br["hit_y"]
                shot_num = br.get("shot_number", i + 1)

                # Línea de cadena desde el punto anterior
                if i == 0:
                    # Primer disparo: línea desde punto QTE
                    ax, ay = br.get("aim_x", hx), br.get("aim_y", hy)
                    pygame.draw.line(screen, (255, 255, 100, 150), (ax, ay), (hx, hy), 1)
                    # Cruz en el punto QTE original
                    pygame.draw.line(screen, (200, 200, 200), (ax - 5, ay), (ax + 5, ay), 1)
                    pygame.draw.line(screen, (200, 200, 200), (ax, ay - 5), (ax, ay + 5), 1)
                    # Círculo de dispersión del primer disparo
                    disp_r = br.get("disp_radius", 0)
                    if disp_r > 0:
                        r_int = int(disp_r)
                        disp_surf = pygame.Surface((r_int * 2 + 4, r_int * 2 + 4), pygame.SRCALPHA)
                        pygame.draw.circle(disp_surf, (255, 255, 100, 30), (r_int + 2, r_int + 2), r_int)
                        pygame.draw.circle(disp_surf, (255, 255, 100, 80), (r_int + 2, r_int + 2), r_int, 1)
                        screen.blit(disp_surf, (ax - r_int - 2, ay - r_int - 2))
                else:
                    # Disparos encadenados: línea desde impacto anterior
                    prev = burst_results[i - 1]
                    px, py = prev["hit_x"], prev["hit_y"]
                    # Color más rojo cuanto mayor el recoil
                    chain_r = min(255, 150 + i * 25)
                    chain_g = max(50, 150 - i * 30)
                    pygame.draw.line(screen, (chain_r, chain_g, 50), (px, py), (hx, hy), 2)

                # Perdigones de escopeta
                pellets = br.get("pellets")
                if pellets:
                    for p in pellets:
                        px, py = p["hit_x"], p["hit_y"]
                        pygame.draw.circle(screen, p["color"], (px, py), 4)
                        pygame.draw.circle(screen, IMPACT_COLOR, (px, py), 5, 1)
                    # Número de disparo en el impacto principal
                    num_label = font.render(str(shot_num), True, (220, 220, 220))
                    screen.blit(num_label, (hx + 10, hy - 12))
                else:
                    # Marca de impacto normal (bala única)
                    size = int(6 * pulse) if i == len(burst_results) - 1 else 4
                    pygame.draw.line(screen, IMPACT_COLOR, (hx - size, hy - size), (hx + size, hy + size), 2)
                    pygame.draw.line(screen, IMPACT_COLOR, (hx - size, hy + size), (hx + size, hy - size), 2)
                    pygame.draw.circle(screen, br["color"], (hx, hy), size + 2, 1)

                    # Número de disparo
                    num_label = font.render(str(shot_num), True, (220, 220, 220))
                    screen.blit(num_label, (hx + 10, hy - 12))

                # Daño flotante del último disparo
                if i == len(burst_results) - 1:
                    dmg_label = font_big.render(f"{br['damage']}", True, br["color"])
                    screen.blit(dmg_label, (hx + 18, hy - 25))
                    if br.get("armor_absorbed"):
                        abs_label = font.render(f"ABSORBED ({br.get('armor_label', '')})", True, (150, 150, 200))
                        screen.blit(abs_label, (hx + 18, hy - 42))

            # Daño total de la ráfaga (arriba de la grilla)
            if len(burst_results) > 1:
                total_dmg = sum(br["damage"] for br in burst_results)
                total_label = font_big.render(f"RAFAGA: {total_dmg} DMG TOTAL ({len(burst_results)} disparos)", True, HIGHLIGHT_COLOR)
                screen.blit(total_label, (GRID_ORIGIN_X, GRID_ORIGIN_Y + GRID_H + 35))

        # Barra de tiempo
        if state in (STATE_QTE_X, STATE_QTE_Y):
            time_remaining = max(0, QTE_TIME_LIMIT - qte_elapsed)
            draw_timer_bar(screen, font, time_remaining, QTE_TIME_LIMIT)

        # HUD
        shots_fired = len(burst_results) if burst_results else 0
        hud_ammo_type = nine_mm_ammo_type if weapon["caliber"] == "9mm" else None
        draw_hud(screen, font, font_big, zones, state, result, weapon_name, weapon,
                 player_hp, ammo[weapon_name], weapon["magazine_capacity"], shots_fired,
                 selected_shots if state == STATE_SELECT_SHOTS else 0,
                 burst_results, operator_hand,
                 armor_config_index=armor_config_index, ammo_type=hud_ammo_type)

        # Panel de patrones de recoil (overlay)
        if show_pattern_panel:
            draw_recoil_pattern_panel(screen, font, font_big, weapon_idx, operator_hand)

        pygame.display.flip()
        clock.tick(60)

    pygame.quit()
    sys.exit()


if __name__ == "__main__":
    main()
