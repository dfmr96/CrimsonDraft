# ECG Pixelate Shader (UI) — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement a configurable screen-space pixelation shader for the ECG wave UI and apply it to the ECG prefab.

**Architecture:** Add a URP-compatible UI unlit shader that quantizes fragments in screen space using a `_PixelSize` material property. Create a material using this shader and assign it to the ECG wave `MaskableGraphic` in the prefab.

**Tech Stack:** Unity 6, URP, uGUI (`MaskableGraphic`), ShaderLab/HLSL

---

### Task 1: Create ECG pixelation shader

**Files:**
- Create: `Game/CrimsonDraft/Assets/Shaders/UI/ECGPixelate.shader`

**Step 1: Write a minimal shader**

Create a UI unlit shader with properties `_MainTex`, `_Color`, `_PixelSize`. Use `SV_POSITION` to quantize screen-space pixels.

**Step 2: Save shader asset**

Create the file and let Unity import it.

**Step 3: Manual check in Editor**

Inspect the shader in the Inspector to confirm properties appear.

**Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Shaders/UI/ECGPixelate.shader
git commit -m "feat(ui-shader): add ECG pixelate shader"
```

---

### Task 2: Create pixelate material

**Files:**
- Create: `Game/CrimsonDraft/Assets/Materials/UI/ECGPixelate.mat`

**Step 1: Create material in Unity**

In Project window, create a new material and set shader to `CrimsonDraft/UI/ECGPixelate`.

**Step 2: Set default `_PixelSize`**

Set `_PixelSize` to a default (e.g., 2).

**Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Materials/UI/ECGPixelate.mat
git commit -m "feat(ui-material): add ECG pixelate material"
```

---

### Task 3: Apply material to ECG prefab

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Prefabs/UI/OperatorEcgWidget.prefab`

**Step 1: Assign material**

Select `OperatorEcgWidget.prefab`, find `Wave` (`OperatorEcgWaveGraphic`) and set `Material` to `ECGPixelate.mat`.

**Step 2: Manual Play Mode check**

Play the scene and confirm the wave is pixelated; adjust `_PixelSize` to verify effect.

**Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Prefabs/UI/OperatorEcgWidget.prefab
git commit -m "feat(ui-hud): apply ECG pixelate material to wave graphic"
```

---

## Acceptance Criteria

1. ECG wave uses `CrimsonDraft/UI/ECGPixelate` shader.
2. Pixel size is configurable via `_PixelSize` on the material.
3. Pixelation is consistent across resolutions (screen-space).
4. Wave color/alpha remains intact.
