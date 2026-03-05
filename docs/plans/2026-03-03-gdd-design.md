# GDD Consolidation — Design Document

**Date:** 2026-03-03
**Scope:** docs / Obsidian vault

---

## Goal

Consolidate all loose Obsidian design notes into a single Game Design Bible (GDD.md) that serves as the authoritative personal reference for Crimson Draft. Three undocumented systems from the prototype are also formalized as new dedicated docs.

---

## Output Artifacts

### New files

| File | Purpose |
|------|---------|
| `GDD.md` | Single GDD document — each section summarizes a system and links to the detail doc |
| `Sistema de Dispersion y Apuntado.md` | Dispersion 3-layer system, per-weapon recoil patterns, HP–dispersion formula |
| `Distractores Visuales.md` | Screen shake, vignette, noise, ghost lines, heartbeat — thresholds and intensities |

### Modified files

| File | Change |
|------|--------|
| `Crimson Draft.md` | Updated to point to GDD.md as the primary entry point |

### Unchanged files

All existing design docs stay in place. GDD.md summarizes them and links via `[[Obsidian links]]`.

---

## GDD Structure

```
# Crimson Draft — Game Design Bible

## 1. Visión del Juego
## 2. Pilares de Diseño
## 3. Narrativa
## 4. Personajes y Party
## 5. El Barco (El Marinera)
## 6. Sistemas de Combate
   6.1 Loop de combate en tiempo real
   6.2 QTE Bidimensional
   6.3 Dispersión y apuntado          ← links to new doc
   6.4 Distractores visuales          ← links to new doc
   6.5 Armadura por capas
   6.6 Sistema de munición
   6.7 Armas y patrones de recoil     ← covered in new dispersion doc
## 7. Sistemas de Supervivencia
   7.1 Salud y presión arterial
   7.2 Krokonil (exposición)
   7.3 Inventario
   7.4 Recursos y escasez
## 8. Exploración
   8.1 Movimiento
   8.2 Guardado (Telégrafo Morse)
## 9. Progresión por Acto
## 10. Referentes e Influencias
## 11. Brechas de diseño pendientes
```

---

## New Doc: Sistema de Dispersion y Apuntado

Extracted from `qte_prototype.py`. Documents:

- **Layer 1 (HP):** `radius = base_dispersion * lerp(1.0, DISPERSION_HP_FACTOR, 1 - hp_pct)` where `DISPERSION_HP_FACTOR = 2.0`
- **Layer 2 (weapon base):** Per-weapon `dispersion_base` and `weapon_deviation`
- **Layer 3 (recoil):** Per-shot offset from `recoil_pattern[]` scaled by `pattern_spread / 100`
- Per-weapon recoil patterns (P229, MP5, Benelli M4, Mk18) with shape descriptions and player compensation notes
- How dispersion interacts with QTE result to determine final hit position

## New Doc: Distractores Visuales

Extracted from `qte_prototype.py`. Documents:

- **Screen shake:** max ±8px at 0% HP, scales linearly with `(1 - hp_pct)`
- **Vignette:** activates at any damage, max depth 40px, max alpha 180
- **Noise:** max 150 noise pixels, scales with damage
- **Ghost lines:** max ±5px offset, triggered by damage
- **Flicker (silhouette):** activates when shot damage > 35% of base (FLICKER_THRESHOLD)
- **Heartbeat:** 60–160 BPM range, affects QTE bar position (vibration up to ±14px)
- All thresholds and how they combine under simultaneous effects

---

## Constraints

- All files in Obsidian vault root (no new subfolders)
- Language: Spanish (design docs convention)
- Obsidian wikilinks format: `[[Doc Name]]`
- No duplication of content — GDD summarizes, detail docs expand
