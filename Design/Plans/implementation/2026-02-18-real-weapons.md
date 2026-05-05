# Real Weapons Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the 3 generic weapons (9mm Pistola, Rifle, Escopeta) with 4 real weapons
(SIG P229, H&K MP5, Benelli M4, Mk18), adding the SMG as a new QTE category and
expanding the 9mm ammo type selection to cover both 9mm weapons.

**Architecture:** Minimal delta — update the WEAPONS dict, add caliber/sfx fields per
weapon, replace FIRE_SFX_KEY lookup with weapon["sfx"], rename pistol_ammo_type to
nine_mm_ammo_type, add [4] key, add fire_smg SFX. No structural changes to the QTE,
burst, or armor systems.

**Tech Stack:** Python 3, pygame, numpy (same as existing prototype)

---

## Task 1: Replace WEAPONS dict and remove FIRE_SFX_KEY

**File:** `Prototype/qte_prototype.py`

### Step 1: Replace lines 42-112 (WEAPONS dict + WEAPON_NAMES)

Replace the entire WEAPONS block:

```python
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
```

### Step 2: Remove FIRE_SFX_KEY dict at line 297

Delete this line entirely:
```python
FIRE_SFX_KEY = {"9mm Pistola": "fire_pistol", "Rifle": "fire_rifle", "Escopeta": "fire_shotgun"}
```

### Step 3: Verify syntax

Run:
```
python -c "import py_compile; py_compile.compile('Prototype/qte_prototype.py', doraise=True); print('OK')"
```
Expected: `OK`

---

## Task 2: Add fire_smg SFX to init_sounds()

**File:** `Prototype/qte_prototype.py`, line ~280

### Step 1: Add fire_smg after fire_pistol

In `init_sounds()`, after the `"fire_pistol"` line, insert:
```python
        "fire_smg": _make_gunshot(180, 70, 50, 0.28),
```

The full block after edit should read:
```python
        "fire_pistol": _make_gunshot(150, 100, 60, 0.3),
        "fire_smg":    _make_gunshot(180, 70,  50, 0.28),
        "fire_rifle":  _make_gunshot(100, 80,  40, 0.35),
        "fire_shotgun": _make_gunshot(80, 150, 100, 0.4),
```

### Step 2: Verify syntax

```
python -c "import py_compile; py_compile.compile('Prototype/qte_prototype.py', doraise=True); print('OK')"
```
Expected: `OK`

---

## Task 3: Rename pistol_ammo_type → nine_mm_ammo_type and fix caliber checks

**File:** `Prototype/qte_prototype.py`

Five changes in `main()` plus two in the event handler. Make them in order top-to-bottom.

### Step 1: Variable init (line ~1577)

```python
# OLD
pistol_ammo_type = "RIP"
# NEW
nine_mm_ammo_type = "RIP"
```

### Step 2: Reload check (line ~1629)

```python
# OLD
if weapon_name == "9mm Pistola":
# NEW
if weapon["caliber"] == "9mm":
```

### Step 3: ammo_t in QTE_X handler — SPACE path (line ~1704)

```python
# OLD
ammo_t = pistol_ammo_type if weapon_name == "9mm Pistola" else None
# NEW
ammo_t = nine_mm_ammo_type if weapon["caliber"] == "9mm" else None
```

### Step 4: fire_sfx in QTE_X SPACE handler (line ~1712)

```python
# OLD
sounds[FIRE_SFX_KEY[weapon_name]].play()
# NEW
sounds[weapon["sfx"]].play()
```
(There are two occurrences of this pattern — fix BOTH here and in the timeout path below.)

### Step 5: Ammo type toggle in RELOAD_SELECT handler (line ~1718)

```python
# OLD
pistol_ammo_type = "FMJ" if pistol_ammo_type == "RIP" else "RIP"
# NEW
nine_mm_ammo_type = "FMJ" if nine_mm_ammo_type == "RIP" else "RIP"
```

### Step 6: ammo_t in QTE timeout path (line ~1783)

```python
# OLD
ammo_t = pistol_ammo_type if weapon_name == "9mm Pistola" else None
# NEW
ammo_t = nine_mm_ammo_type if weapon["caliber"] == "9mm" else None
```

### Step 7: fire_sfx in QTE timeout path (line ~1791)

```python
# OLD
sounds[FIRE_SFX_KEY[weapon_name]].play()
# NEW
sounds[weapon["sfx"]].play()
```

### Step 8: hud_ammo_type (line ~1931)

```python
# OLD
hud_ammo_type = pistol_ammo_type if weapon_name == "9mm Pistola" else None
# NEW
hud_ammo_type = nine_mm_ammo_type if weapon["caliber"] == "9mm" else None
```

### Step 9: Verify syntax

```
python -c "import py_compile; py_compile.compile('Prototype/qte_prototype.py', doraise=True); print('OK')"
```
Expected: `OK`

---

## Task 4: Add [4] key handler and update HUD controls text

**File:** `Prototype/qte_prototype.py`

### Step 1: Add [4] key after existing [3] key handler (line ~1655)

```python
# After the existing [3] block:
elif event.key == pygame.K_4 and state == STATE_IDLE:
    if weapon_idx != 3:
        weapon_idx = 3
        sounds["weapon_switch"].play()
```

### Step 2: Update HUD controls string (line ~1189)

```python
# OLD
"[1] Pistola  [2] Rifle  [3] Escopeta",
# NEW
"[1] P229  [2] MP5  [3] M4  [4] Mk18",
```

### Step 3: Final verification — syntax check

```
python -c "import py_compile; py_compile.compile('Prototype/qte_prototype.py', doraise=True); print('OK')"
```
Expected: `OK`

### Step 4: Manual smoke test

Run the prototype and verify:
1. All 4 weapons selectable with [1]-[4]
2. P229 and MP5 show [RIP]/[FMJ] tag; Benelli M4 and Mk18 do not
3. Reloading P229 or MP5 enters ammo selection screen; M4 and Mk18 reload directly
4. Heatmap [P] cycles through all 4 weapon patterns without error
5. Armor configs [A] still work with all 4 weapons
6. P229 fires with pistol sound; MP5 fires with sharper SMG sound; M4 fires with
   shotgun sound; Mk18 fires with rifle sound
