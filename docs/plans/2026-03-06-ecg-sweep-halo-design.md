# ECG Sweep + Halo Shader (UI) — Design

**Date:** 2026-03-06

## Goal
Add a shader-driven sweep (left→right) for the ECG wave, tied to BPM, with a bright halo at the sweep tip, while keeping the underlying waveform stable (no jitter).

## Requirements
- Sweep speed is driven by BPM.
- Sweep moves left→right.
- Halo is a small glow at the sweep tip (not a vertical bar).
- Works on the ECG wave `MaskableGraphic` only.
- Uses UI shader and material.

## Non-Goals
- Post-processing or full-UI sweep.
- Rewriting ECG waveform generation.
- CPU-based scroll of the waveform mesh.

## Approach
Create a UI unlit shader `CrimsonDraft/UI/ECGSweep` that masks fragments to the sweep position and adds a bright halo at the sweep tip. Drive `_Sweep` from the ECG wave component each frame based on BPM. Assign the material to the ECG wave prefab.

## Shader Design
**Shader Name:** `CrimsonDraft/UI/ECGSweep`

**Properties**
- `_MainTex` (2D), `_Color` (Color) — standard UI
- `_Sweep` (Range 0..1) — sweep position in UV space
- `_HaloWidth` (Range 0.001..0.1) — halo width in UV
- `_HaloIntensity` (Range 0..2) — additive brightness

**Core Logic**
- If `uv.x > _Sweep`, alpha = 0 (not yet swept).
- Halo intensity peaks at `uv.x == _Sweep` with smooth falloff:
  - `halo = saturate(1 - abs(uv.x - _Sweep) / _HaloWidth)`
  - `color.rgb += halo * _HaloIntensity`

## Integration
- Create `Assets/Shaders/UI/ECGSweep.shader`.
- Create `Assets/Materials/UI/ECGSweep.mat` with default halo params.
- Assign material to `OperatorEcgWaveGraphic` in `OperatorEcgWidget.prefab`.
- Update `OperatorEcgWaveGraphic` each frame to set `_Sweep`:
  - `phase = Mathf.Repeat(Time.unscaledTime * (bpm / 60f), 1f)`

## Testing
Manual smoke test:
- BPM 60/120/160 → sweep speed changes accordingly.
- Halo visible at sweep tip.
- Waveform stays stable (no “breathing”).

## Risks / Notes
- Requires per-instance material or `MaterialPropertyBlock` to avoid shared sweep across all ECGs. For now, single instance is fine.
