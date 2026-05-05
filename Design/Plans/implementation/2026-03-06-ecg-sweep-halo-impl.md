# ECG Sweep + Halo Shader (UI) — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement a shader-driven ECG sweep (left→right) tied to BPM with a bright halo at the sweep tip.

**Architecture:** Add a UI unlit shader that masks the wave by a sweep value and adds a halo. Drive `_Sweep` from `OperatorEcgWaveGraphic` using BPM. Apply the material to the ECG wave prefab.

**Tech Stack:** Unity 6, URP/uGUI, ShaderLab/HLSL

---

### Task 1: Create ECG sweep shader

**Files:**
- Create: `Game/CrimsonDraft/Assets/Shaders/UI/ECGSweep.shader`

**Step 1: Write the shader**

Create a UI unlit shader with `_Sweep`, `_HaloWidth`, `_HaloIntensity` and UI clip support.

**Step 2: Save and import**

Let Unity import the shader.

**Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Shaders/UI/ECGSweep.shader
git commit -m "feat(ui-shader): add ECG sweep shader"
```

---

### Task 2: Create sweep material

**Files:**
- Create: `Game/CrimsonDraft/Assets/Materials/UI/ECGSweep.mat`

**Step 1: Create material**

Set shader to `CrimsonDraft/UI/ECGSweep`.

**Step 2: Set defaults**

`_HaloWidth = 0.02`, `_HaloIntensity = 0.6`

**Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Materials/UI/ECGSweep.mat
git commit -m "feat(ui-material): add ECG sweep material"
```

---

### Task 3: Drive sweep from ECG wave and assign material

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgWaveGraphic.cs`
- Modify: `Game/CrimsonDraft/Assets/Prefabs/UI/OperatorEcgWidget.prefab`

**Step 1: Update script**

Add a per-frame update that sets `_Sweep`:
```csharp
var phase = Mathf.Repeat(Time.unscaledTime * (this.bpm / 60f), 1f);
material.SetFloat(SweepId, phase);
```
Use a cached shader property ID and ensure a unique material instance if needed.

**Step 2: Assign material**

Set `ECGSweep.mat` on `OperatorEcgWaveGraphic` in the prefab.

**Step 3: Manual Play Mode check**

Verify sweep speed matches BPM and halo is visible.

**Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgWaveGraphic.cs Game/CrimsonDraft/Assets/Prefabs/UI/OperatorEcgWidget.prefab
git commit -m "feat(ui-hud): drive ECG sweep and apply material"
```

---

## Acceptance Criteria

1. Sweep advances left→right and is tied to BPM.
2. Halo appears at sweep tip.
3. ECG waveform remains stable (no jitter).
4. Material is configurable via `_HaloWidth` and `_HaloIntensity`.
