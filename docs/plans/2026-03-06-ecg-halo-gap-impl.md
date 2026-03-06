# ECG Halo + Gap (UI) — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add lead/halo/gap segment coloring to the ECG sweep shader with configurable parameters.

**Architecture:** Extend the `CrimsonDraft/UI/ECGSweep` shader to branch on UV.x relative to `_HaloX`, `_HaloWidth`, `_GapWidth`, and apply `_LeadColor`/`_HaloColor` with a transparent gap.

**Tech Stack:** Unity 6, uGUI, ShaderLab/HLSL

---

### Task 1: Extend ECGSweep shader properties and logic

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Shaders/UI/ECGSweep.shader`

**Step 1: Add properties**

Add `_LeadColor`, `_HaloColor`, `_GapWidth`.

**Step 2: Apply region logic**

Compute halo start/end and gap end; apply colors and alpha per region.

**Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Shaders/UI/ECGSweep.shader
git commit -m "feat(ui-shader): add lead/halo/gap regions to ECG sweep"
```

---

### Task 2: Update material defaults

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Materials/UI/ECGSweep.mat`

**Step 1: Set defaults**

`_LeadColor` white, `_HaloColor` white, `_GapWidth` 0.05

**Step 2: Commit**

```bash
git add Game/CrimsonDraft/Assets/Materials/UI/ECGSweep.mat
git commit -m "feat(ui-material): add ECG lead/halo/gap defaults"
```

---

## Acceptance Criteria
1. Lead/halo/gap segments render per UV.x.
2. Gap is transparent after halo.
3. Colors are configurable via material.
