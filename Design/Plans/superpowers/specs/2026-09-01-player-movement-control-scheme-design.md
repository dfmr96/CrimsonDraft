# Player Movement Control Scheme (Modern / Classic) — Design Spec

**Date:** 2026-09-01
**Branch:** Development

## Overview

Player movement currently has one hardcoded behavior: camera-relative movement, inherited from the RE HD Remaster "Alternate" control scheme (see `2026-09-01` combat/navigation session — `ICameraRelativeMovementService`). The Settings menu already has a "Control" knob (`GeneralMenuController`, `ControlIndex = 2`) that is visually present but permanently locked (`Adjust()` no-ops on it).

This spec introduces a Strategy pattern so `PlayerController` delegates its per-frame direction/rotation decision to a swappable `IPlayerMovementStrategy`, adds a second strategy implementing classic Resident Evil tank controls, and wires the existing "Control" knob to a new persisted setting that picks between them live (including mid-gameplay, via the Pause menu).

## Components

### `IPlayerMovementStrategy` (new)

`Assets/Scripts/Navigation/Player/Movement/IPlayerMovementStrategy.cs`

```csharp
public interface IPlayerMovementStrategy
{
    PlayerMovementResult Tick(Transform playerTransform, Vector2 rawInput, InputDevice? lastDevice, bool isAiming, float deltaTime);
}
```

Called every `FixedUpdate`, unconditionally — including frames where movement is otherwise skipped (aiming, stick at rest). This preserves the existing requirement that `ModernPlayerMovementStrategy`'s internal camera-basis bookkeeping never misses a held-direction change (see the Modern control fix from this session).

### `PlayerMovementResult` (new)

`Assets/Scripts/Navigation/Player/Movement/PlayerMovementResult.cs`

```csharp
public readonly struct PlayerMovementResult
{
    public Vector3 Direction   { get; }  // unit horizontal vector, or Vector3.zero
    public bool    AllowSprint { get; }  // false forces walk speed regardless of Sprint held
}
```

### `ModernPlayerMovementStrategy` (new — extracted from current `PlayerController`)

`Assets/Scripts/Navigation/Player/Movement/ModernPlayerMovementStrategy.cs`

Wraps the existing `ICameraRelativeMovementService`. Behavior is unchanged from today:

1. Always calls `cameraRelativeMovementService.Tick(direction)`, where `direction` is `raw.normalized` (Gamepad) or `Quantize8Way(raw)` (keyboard).
2. `Direction = Right * direction.x + Forward * direction.y`, normalized (or zero).
3. If `Direction != Vector3.zero`, sets `playerTransform.forward = Direction` (character always faces its movement direction).
4. `AllowSprint` is always `true`.
5. `isAiming` is accepted but not used — the camera-basis tick must keep running while aiming (that's the bug this session already fixed for Modern).

### `ClassicPlayerMovementStrategy` (new)

`Assets/Scripts/Navigation/Player/Movement/ClassicPlayerMovementStrategy.cs`

Resident Evil–style tank controls: rotate in place, walk/run only along the character's own current facing.

- `[SerializeField]`-equivalent constant/field: `turnSpeedDegPerSec` (default `180f`, tunable placeholder, no playtesting yet).
- Rotation: `playerTransform.Rotate(Vector3.up, rawInput.x * turnSpeedDegPerSec * deltaTime, Space.World)` — proportional to stick deflection on Gamepad, effectively ±1 (full rate) on keyboard. **Skipped entirely if `isAiming` is true** (aiming must not let the player spin their body).
- Direction: `playerTransform.forward` if `rawInput.y > 0.1f`; `-playerTransform.forward` if `rawInput.y < -0.1f`; `Vector3.zero` otherwise (turning in place with no Y input is valid and produces no translation). `0.1f` matches the existing "stick considered held" magnitude threshold already used in `PlayerController` (`sqrMagnitude >= 0.01f`).
- `AllowSprint`: `false` whenever `rawInput.y < -0.1f` (backpedal is always walk speed, matching the original REmake — running backward was never possible in the classic control scheme). `true` otherwise.
- No 8-way quantization needed — X (turn) and Y (forward/back) are independent scalars, not combined into a single world direction, so raw analog/digital values are used directly.

### `IControlSchemeService` / `ControlSchemeService` (new)

`Assets/Scripts/Infrastructure/Input/IControlSchemeService.cs`, `ControlSchemeService.cs`

Mirrors `IGraphicsSettingsService` / `IAudioSettingsService`:

```csharp
public enum ControlScheme { Modern, Classic }

public interface IControlSchemeService
{
    ControlScheme CurrentScheme { get; }
    void SetScheme(ControlScheme scheme);
}
```

- Persists via `PlayerPrefs` (`"Control.Scheme"`, int, default `0` = `Modern`).
- Registered as `Lifetime.Singleton` in `GameLifetimeScope` (not `NavigationScope`) so it survives scene transitions, matching Graphics/Audio settings.
- No change-event needed: `PlayerController` re-reads `CurrentScheme` every `FixedUpdate` (cheap enum compare) rather than subscribing, avoiding an `IDisposable` subscription lifecycle for a value that only changes via explicit menu interaction.

## `PlayerController` changes

`FixedUpdate` currently computes `direction` and `moveDir` inline via `ICameraRelativeMovementService`. It changes to:

1. Read `raw = inputService.Move.ReadValue<Vector2>()`.
2. Pick the active strategy: `this.controlSchemeService.CurrentScheme == ControlScheme.Classic ? this.classicStrategy : this.modernStrategy` (both held as constructed fields — no per-frame allocation).
3. `var result = strategy.Tick(transform, raw, lastDevice, IsAiming, Time.fixedDeltaTime);` — called unconditionally, same position in the method as today's `cameraRelativeMovementService.Tick(...)` call.
4. If `IsAiming` → zero velocity, return (unchanged).
5. If `result.Direction == Vector3.zero` → zero velocity, Idle trigger, return (unchanged, just reading `result.Direction` instead of the old local `moveDir`).
6. `isSprinting = inputService.Sprint.IsPressed() && result.AllowSprint`.
7. Everything downstream (health speed multiplier, `ResolveNavMeshDirection`, animator triggers, `rb.linearVelocity`) is unchanged, just consuming `result.Direction` instead of the old inline `moveDir`.

`ModernPlayerMovementStrategy` and `ClassicPlayerMovementStrategy` are constructed once (plain C# classes, not MonoBehaviours/DI-registered — `PlayerController` builds them directly in `Construct()`, injecting `ICameraRelativeMovementService` into the Modern one). `Quantize8Way` moves from `PlayerController` into `ModernPlayerMovementStrategy` (it's Modern-specific).

## Settings menu wiring

`GeneralMenuController.Adjust(int index, int direction)`:

```csharp
if (index == ControlIndex)
{
    var next = this.controlSchemeService.CurrentScheme == ControlScheme.Modern
        ? ControlScheme.Classic : ControlScheme.Modern;
    this.controlSchemeService.SetScheme(next);
    return;
}
```

A 2-state knob alternates regardless of `direction`'s sign. `ShowOutline`/`HideOutlines` already work index-generically — no change needed there.

**Visual polish caveat (called out, not blocking):** `GammaChannel` has a `knob` Transform that physically rotates to reflect its value; the `control` field is currently just a `LockedChannel` (`outline` only, no `knob`). This spec wires the *data* path (persisted scheme, `Adjust()` no longer a no-op, selection highlight via existing `outline`). Whether to add a rotating knob visual for the two Control positions depends on whether a knob mesh/Transform already exists on the "Control" GameObject in the scene — checked and decided during implementation; if absent, shipping without knob rotation (selection still visibly highlighted via `outline`) is acceptable for this pass.

## Testing

- `ClassicPlayerMovementStrategyTests` (new, EditMode): pure logic against a real `Transform` (no MonoBehaviour/Cinemachine dependency) — rotation direction/rate, forward vs backward `Direction`, `AllowSprint` false only when moving backward, no rotation while `isAiming`, turning in place with zero Y input.
- `ModernPlayerMovementStrategyTests` (new, EditMode): thin wrapper test using a fake `ICameraRelativeMovementService` — confirms `Tick` is always called, `Direction` combines `Right`/`Forward` correctly, `transform.forward` gets set, `AllowSprint` always true.
- `ControlSchemeServiceTests` (new, EditMode): default is `Modern`, `SetScheme` persists and is read back, mirrors existing (untested, but same shape as) `GraphicsSettingsService`/`AudioSettingsService` pattern — first test coverage for this settings family.
- No test coverage for `GeneralMenuController.Adjust` wiring specifically (the class has no existing tests — it's a MonoBehaviour driven by physical knob/outline `GameObject` references not easily faked; consistent with the rest of that file being untested today).

## Out of Scope

- Rotating knob visual for the Control setting (see caveat above) — data wiring only unless a knob Transform is trivially already present.
- Analog-sensitive walk speed in Classic mode (partial stick deflection just meets/misses a deadzone threshold today, same precision Modern already uses — no proportional speed scaling).
- Any change to combat movement/aiming controls — this spec is Navigation-mode movement only.
- Turn speed playtesting/tuning — `180f` deg/sec ships as a placeholder like other numeric values introduced this session (documented as such, not a final balance decision).
