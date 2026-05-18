# Burst Pattern Editor — Design Spec

**Date:** 2026-05-13  
**Status:** Approved  
**Scope:** Standalone Unity EditorWindow for authoring weapon burst dispersion patterns, plus the ScriptableObject that stores them.

---

## Overview

A self-contained editor tool that lets the designer create and preview weapon burst patterns. Each pattern is a sequence of shots, where every shot has a predefined base position and an ellipse that controls the random spread. The tool is isolated from gameplay code.

---

## 1. Data — `BurstPatternData`

**File:** `Game/CrimsonDraft/Assets/Scripts/Combat/Data/BurstPatternData.cs`  
**Assembly:** `CrimsonDraft.Combat` (runtime, not editor-only — so gameplay can reference it later)  
**Type:** `ScriptableObject`  
**Menu path:** `CrimsonDraft/Combat/Burst Pattern`

### `BurstShotEntry` (serializable struct)

| Field | Type | Description |
|---|---|---|
| `center` | `Vector2` | Base position of the shot in abstract units. Forced to `(0,0)` for index 0. |
| `semiAxisX` | `float` | Horizontal semi-axis of the dispersion ellipse (a). Minimum: 1. |
| `semiAxisY` | `float` | Vertical semi-axis of the dispersion ellipse (b). Minimum: 1. |

`BurstPatternData` holds a `BurstShotEntry[]` array. Index 0 is always present and its `center` is always `(0,0)`.

### Random sampling formula (uniform distribution inside ellipse)

```
angle = Random.value * 2π
r     = sqrt(Random.value)
x     = center.x + semiAxisX * r * cos(angle)
y     = center.y + semiAxisY * r * sin(angle)
```

---

## 2. Editor Tool — `BurstPatternEditorWindow`

**File:** `Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs`  
**Assembly:** `CrimsonDraft.Editor`  
**Menu path:** `Tools > CrimsonDraft > Burst Pattern Editor`  
**Type:** `EditorWindow`

### 2.1 Layout

The window is split into two panels separated by a vertical divider:

- **Left panel** — fixed width 240px. Contains: asset picker, shot list, simulation controls.
- **Right panel** — fills the rest. Contains: the interactive grid canvas.

```
┌──────────────────────┬────────────────────────────────────────┐
│ [BurstPatternData ▼] │                                        │
│ [New Pattern][Save]  │                                        │
│                      │         (canvas)                       │
│ ── Disparos ──────── │                                        │
│  #0 (0,0) a=20 b=30  │   ·  ·  ·  +  ·  ·  ·                │
│  #1 (5,8) a=15 b=25  │         [#0]                          │
│  #2 (-3,4) a=10 b=20 │     ( ellipse )                       │
│                      │                                        │
│ [+ Agregar Disparo]  │                                        │
│ [− Eliminar Último]  │                                        │
│                      │                                        │
│ ── Simulación ─────  │                                        │
│  Delay: [===] 0.3s   │                                        │
│ [Probar Ráfaga]      │                                        │
│ [Limpiar Resultados] │                                        │
└──────────────────────┴────────────────────────────────────────┘
```

### 2.2 Grid Canvas

- Origin `(0,0)` at center of the canvas rect.
- Grid lines drawn with `Handles.DrawLine` every 1 unit; major lines every 5 units (slightly brighter).
- **Scale:** `pixelsPerUnit` float, default 8. Range: 4–32. Adjusted by mouse scroll wheel over the canvas.
- Each shot is drawn as a filled circle (radius 6px) with a number label. Colors cycle by index.
- The ellipse for each shot is drawn as a polyline approximation (24-segment loop) using the formula `(center + semiAxisX*cos(t), center + semiAxisY*sin(t))` in canvas space.
- Scatter dots from simulation are drawn as small filled circles (radius 3px) in a dimmer shade of the shot's color.

### 2.3 Handles

Handles are shown for the **selected shot** and any shot under mouse hover.

- **Right handle:** circle at `(center.x + semiAxisX, center.y)` in canvas space. Dragging horizontally updates `semiAxisX`.
- **Top handle:** circle at `(center.x, center.y + semiAxisY)` in canvas space. Dragging vertically updates `semiAxisY`.
- Both handles enforce minimum value of 1 unit.
- Handle circle radius: 5px in canvas space.

### 2.4 Interaction Rules

| Action | Behavior |
|---|---|
| Click shot circle | Selects that shot. |
| Drag shot circle (index ≥ 1) | Updates `center`. Shot #0 is locked. |
| Drag right handle | Updates `semiAxisX` (horizontal drag only). |
| Drag top handle | Updates `semiAxisY` (vertical drag only). |
| Scroll wheel over canvas | Adjusts `pixelsPerUnit` zoom. |
| Drag on empty canvas | No action. |

Drag priority (checked in order): handle right → handle top → shot circle → nothing.

A drag begins on `MouseDown`, updates on `MouseDrag`, and commits on `MouseUp`. The drag always consumes the event to prevent selection from firing simultaneously.

### 2.5 Shot List (Left Panel)

Each row shows: `#index | center | semiAxisX | semiAxisY`. Clicking a row selects the shot (same as clicking its circle on canvas). Shot #0 row shows `(locked)` next to center.

### 2.6 Save / Load

- **ObjectField:** selecting a `BurstPatternData` asset loads it into the editor. The current pattern is replaced (unsaved changes are lost without warning in this first version).
- **New Pattern:** opens `EditorUtility.SaveFilePanelInProject` → creates asset via `AssetDatabase.CreateAsset` with one default entry (#0 at origin, semiAxisX=20, semiAxisY=30).
- **Save:** calls `EditorUtility.SetDirty(asset)` + `AssetDatabase.SaveAssets()`. Disabled when no asset is loaded.

---

## 3. Simulation

### State machine

```
Idle
  → [Probar Ráfaga] → Playing
      EditorApplication.update ticks:
        if (now - lastShotTime >= delay):
          sample random point inside ellipse[currentIndex]
          append to scatterDots list
          currentIndex++
          if currentIndex >= shots.Length → Done
  → [Limpiar Resultados] (any state) → clears scatterDots, resets → Idle
Done
  → [Probar Ráfaga] → clears dots, restarts → Playing
```

- `scatterDots`: `List<(int shotIndex, Vector2 canvasPos)>` — stored in the EditorWindow, not serialized.
- Scatter dots persist on canvas until cleared.
- While `Playing`, the "Probar Ráfaga" button label changes to "Detener". Clicking it stops and goes to Idle without clearing dots.

### Delay

Float field + slider in the left panel. Range: 0.05s – 2.0s. Default: 0.3s.

---

## 4. Constraints and Edge Cases

- Minimum 1 shot (shot #0) — "Eliminar Último" is disabled when `shots.Length == 1`.
- Shot #0 center is clamped to `(0,0)` on every `OnGUI` call, regardless of stored value.
- `semiAxisX` and `semiAxisY` are clamped to `>= 1` on every draw and on drag commit.
- No undo support in this first version (Unity's Undo system is not wired).
- The tool does not reference `AimViewController`, `WeaponData`, or any other gameplay class.

---

## 5. File Locations

| Artifact | Path |
|---|---|
| Runtime data | `Game/CrimsonDraft/Assets/Scripts/Combat/Data/BurstPatternData.cs` |
| Editor window | `Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs` |
| Assembly (data) | `CrimsonDraft.Combat` (already exists) |
| Assembly (editor) | `CrimsonDraft.Editor` (already exists) |
