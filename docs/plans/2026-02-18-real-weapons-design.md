# Design: Real Weapons Implementation
**Date:** 2026-02-18
**Scope:** Prototype (`Prototype/qte_prototype.py`)
**Approach:** Option A — minimal delta

---

## Goal

Replace the 3 generic weapons (9mm Pistola, Rifle, Escopeta) with 4 real weapons
representing the game's weapon categories. Add H&K MP5 as the new SMG category.

---

## Weapon Stats

| Weapon | Caliber | Base Damage | Dispersion L1 | Bar Speed (X/Y) | Magazine |
|---|---|---|---|---|---|
| SIG P229 | 9mm | 28 | 12px | 4.0 / 4.5 | 13 |
| H&K MP5 | 9mm | 22 | 14px | 5.5 / 6.0 | 30 |
| Benelli M4 | 12ga | 12 × 6 pellets | 40px | 4.0 / 4.5 | 7 |
| Mk18 | 5.56 | 55 | 6px | 7.5 / 8.0 | 30 |

Each weapon entry in WEAPONS gets two new fields:
- `"caliber"`: `"9mm"` | `"12ga"` | `"5.56"`
- `"sfx"`: `"fire_pistol"` | `"fire_smg"` | `"fire_shotgun"` | `"fire_rifle"`

---

## Recoil Patterns

### SIG P229 — "7" shape (13 entries for 13-round magazine)
Gentle rise for first 3 shots, then progressive right drift. Last 5 shots nearly
pure horizontal. Player compensation: down-left.

### H&K MP5 — "I" shape with slight right lean (29 entries for 30-round magazine)
Very controlled vertical rise (~4-5px/shot), minimal lateral displacement.
Around shot 15 the rise stabilizes and a soft right drift begins.
Predictable and controllable to the end. Player compensation: soft down, almost no lateral.

### Benelli M4 — Inverted "V" with massive initial kick (6 entries for 7-round magazine)
Identical personality to current Escopeta. Massive vertical kick on shot 2 (-25px),
then drops and drifts right. Tuned to 7 rounds.

### Mk18 — Extended inverted "J" (29 entries for 30-round magazine)
Aggressive vertical rise on shots 2-4, then pronounced left curve. Based on current
Rifle pattern but extended: the left curve flattens progressively, and shots 15-30
drift left softly with little vertical rise. Player compensation: down-right.

---

## Ammo System Expansion

- `pistol_ammo_type` renamed to `nine_mm_ammo_type` throughout
- `STATE_RELOAD_SELECT` activates for any weapon where `weapon["caliber"] == "9mm"`
  (covers both P229 and MP5)
- Benelli M4 and Mk18 reload directly (no ammo type selection)
- HUD shows `[RIP]`/`[FMJ]` tag only for 9mm weapons
- Bullet color in magazine display changes with `nine_mm_ammo_type` only for 9mm weapons

---

## New SFX

`fire_smg`: square wave 180Hz, 70ms duration, white noise 50ms.
Sharper and drier than `fire_pistol` (150Hz, 100ms).

SFX assignment per weapon (via `weapon["sfx"]` field):
- P229 → `fire_pistol`
- MP5 → `fire_smg`
- Benelli M4 → `fire_shotgun`
- Mk18 → `fire_rifle`

---

## Key Navigation

`[1]` P229 · `[2]` MP5 · `[3]` Benelli M4 · `[4]` Mk18

HUD controls line updated accordingly.

---

## Variable Renames

| Old | New | Occurrences |
|---|---|---|
| `"9mm Pistola"` | `"P229"` | ~8 |
| `"Rifle"` | `"Mk18"` | ~6 |
| `"Escopeta"` | `"Benelli M4"` | ~6 |
| `pistol_ammo_type` | `nine_mm_ammo_type` | ~6 |

---

## Verification

1. `py_compile.compile("Prototype/qte_prototype.py")` — no syntax errors
2. Run prototype — all 4 weapons selectable with [1]-[4]
3. P229 fires with correct recoil, 13-round magazine, RIP/FMJ selection on reload
4. MP5 fires with controlled recoil, 30-round magazine, RIP/FMJ selection on reload
5. Benelli M4 fires 6 pellets, 7-round magazine, no ammo selection
6. Mk18 fires with left-curving recoil, 30-round magazine, no ammo selection
7. HUD shows [RIP]/[FMJ] for P229 and MP5, nothing for M4 and Mk18
8. SFX: P229 = pistol sound, MP5 = new SMG sound (sharper), M4 = shotgun, Mk18 = rifle
9. Armor system still works with all 4 weapons (retrocompatible)
10. Heatmap [P] works for all 4 weapons
