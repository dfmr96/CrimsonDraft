# ECG Pixelate Shader (UI) — Design

**Date:** 2026-03-06

## Goal
Add a configurable, screen-space pixelation shader for the ECG wave (UI `MaskableGraphic`) so the pixel size is consistent across resolutions and only affects the wave.

## Requirements
- Pixelation is applied in the shader on the ECG wave only.
- Pixel size is configurable via material (e.g., `PixelSize`).
- Pixelation is screen-space (stable at different resolutions/Canvas scales).
- Must work with URP UI rendering and `MaskableGraphic`.

## Non-Goals
- Pixelate the entire UI.
- Post-processing or camera-wide pixelation.
- Changes to ECG wave generation logic.

## Approach
Create a custom UI unlit shader `CrimsonDraft/UI/ECGPixelate` that quantizes fragment sampling to a screen-space grid. Expose `_PixelSize` in pixels as a material property. Apply the material to the ECG wave graphic in the prefab.

## Shader Design
**Shader Name:** `CrimsonDraft/UI/ECGPixelate`

**Properties**
- `_MainTex` (2D): required by UI pipeline
- `_Color` (Color): UI tint
- `_PixelSize` (Range 1..16): pixel size in screen pixels

**Core Pixelation**
- Use `SV_POSITION` in the fragment to get screen-space pixel coordinates.
- Quantize the screen position to a grid of `_PixelSize`.
- Sample using quantized screen position while preserving UI vertex color/alpha.

## Integration
- Create `Assets/Shaders/UI/ECGPixelate.shader`.
- Create `Assets/Materials/UI/ECGPixelate.mat` using the shader.
- Assign the material to `OperatorEcgWaveGraphic` on `OperatorEcgWidget.prefab`.

## Testing
Manual smoke test in Play Mode:
- Change `_PixelSize` (1, 2, 4, 6) and verify visible pixelation.
- Confirm consistent pixel size at different resolutions.
- Confirm color/alpha of the wave remains intact.

## Risks / Notes
- UI `MaskableGraphic` requires correct blend settings and UI tags for URP.
- Screen-space quantization requires correct use of `SV_POSITION` in URP UI pass.
