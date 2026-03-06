# ECG Halo + Gap (UI) — Design

**Date:** 2026-03-06

## Goal
Extend the ECG sweep shader so the waveform is colored in segments: lead color before the halo, halo color at the tip, a transparent gap after the halo, then lead color again.

## Requirements
- Configurable lead color (alpha 1).
- Configurable halo color and width.
- Configurable transparent gap width after the halo.
- Configurable halo X position.
- Works on ECG `MaskableGraphic` only.

## Approach
Extend `CrimsonDraft/UI/ECGSweep` with extra color and width parameters. In the fragment shader, choose color/alpha based on UV.x relative to `haloX`, `haloWidth`, and `gapWidth`.

## Shader Logic (UV.x)
- Region A (before halo start): `x < haloX - haloWidth/2` → `_LeadColor`, alpha 1
- Region B (halo): `abs(x - haloX) <= haloWidth/2` → `_HaloColor`, alpha 1 (optional extra brightness)
- Region C (gap): `x in (haloX + haloWidth/2, haloX + haloWidth/2 + gapWidth)` → alpha 0
- Region D (after gap): `x >= haloX + haloWidth/2 + gapWidth` → `_LeadColor`, alpha 1

## Integration
Update the `ECGSweep` shader and material defaults. Keep `_Sweep = 1` and `_HaloX` adjustable.

## Testing
Manual Play Mode:
- Move `_HaloX` and verify segments follow.
- Adjust `_HaloWidth` and `_GapWidth` to confirm behavior.
