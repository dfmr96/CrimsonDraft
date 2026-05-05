# Aim Minigame Design

**Date:** 2026-03-01
**Scope:** combat-ui

## Overview

When the player selects the Shoot command from the CommandPanel, a two-phase aim
minigame activates. The player confirms a vertical position, then a horizontal
position, and a shot marker is instantiated at the resulting coordinates inside
AimSpace.

## Scene Hierarchy (existing)

```
Canvas/AimView/QTE/
├── HortizontalSpace/
│   └── HorizontalSelector   (Image)
├── VerticalSpace/
│   └── VerticalSelector     (Image)
└── AimSpace                 (empty — receives shot marker)
```

## Architecture

Follows the same pattern as CommandPanelView / SubPanelView:

- `IAimView` — interface consumed by CombatMenuController
- `AimViewController` — MonoBehaviour on AimView, implements IAimView
- `CombatMenuController` — gains IAimView dependency and a new Aiming state

### State Machine Extension

```
OperatorSelection → CommandPanel → Aiming
                                      ↓ (OnShotFired)
                                 OperatorSelection
```

No cancel from Aiming: the player must complete the shot.

## IAimView

```csharp
event Action<Vector2> OnShotFired;   // normalised (0–1, 0–1) position in AimSpace
void Show();
void Hide();
```

## AimViewController

### Internal Phase Enum

```
None → VerticalAiming → HorizontalAiming → Complete
```

### Flow

1. `Show()` — activates GameObject, starts VerticalSelector oscillation,
   subscribes to `IInputService.CombatConfirm.performed`.
2. **VerticalAiming** — VerticalSelector oscillates in Y between
   `±(VerticalSpace.rect.height / 2)` using DOTween Yoyo + InOutSine.
   On Confirm: record `normalizedY`, dim VerticalSelector (DOFade),
   start HorizontalSelector oscillation, switch to HorizontalAiming.
3. **HorizontalAiming** — HorizontalSelector oscillates in X between
   `±(HortizontalSpace.rect.width / 2)`.
   On Confirm: record `normalizedX`, dim HorizontalSelector, instantiate
   shot marker in AimSpace at remapped local position, fire `OnShotFired(Vector2)`,
   unsubscribe from CombatConfirm.
4. `Hide()` — deactivates GameObject, kills all DOTween tweens, unsubscribes
   from CombatConfirm if still subscribed.

### Shot Marker Positioning

```
x = Lerp(aimSpaceRect.xMin, aimSpaceRect.xMax, normalizedX)
y = Lerp(aimSpaceRect.yMin, aimSpaceRect.yMax, normalizedY)
marker.localPosition = new Vector3(x, y, 0)
```

### Inspector Fields

| Field             | Type       | Description                              |
|-------------------|------------|------------------------------------------|
| `verticalSpace`   | RectTransform | VerticalSpace rect (bounds for Y)     |
| `verticalSelector`| RectTransform | VerticalSelector to animate           |
| `horizontalSpace` | RectTransform | HortizontalSpace rect (bounds for X)  |
| `horizontalSelector` | RectTransform | HorizontalSelector to animate      |
| `aimSpace`        | RectTransform | Parent for instantiated markers       |
| `shotMarkerPrefab`| GameObject | Prefab for the shot marker image         |
| `speed`           | float      | Half-cycle duration in seconds (default 0.8)|
| `dimmingAlpha`    | float      | Alpha of selector after confirm (default 0.3)|

## CombatMenuController Changes

### New Dependency

`IAimView aimView` added to both the production constructor (5-arg → 6-arg) and
the internal test constructor (4-arg → 5-arg).

### HandleCommandSelected(Shoot)

Replace the early `return` with:
```
commandPanel.SetDimmed(true)
aimView.Show()
aimView.OnShotFired += HandleShotFired
state = Aiming
```

### HandleShotFired(Vector2)

```
aimView.OnShotFired -= HandleShotFired
aimView.Hide()
commandPanel.Hide()
menuView.SetDimmed(false)
menuView.FocusOperator(selectedOperator)
state = OperatorSelection
```

## CombatScope Changes

Register AimViewController alongside the other views:
```csharp
builder.RegisterComponentInHierarchy<AimViewController>().AsImplementedInterfaces();
```

## Testability

`AimViewController` exposes `internal HandleConfirm()` and a test constructor
without `IInputService`. Tests verify:

1. First `HandleConfirm()` does not fire `OnShotFired`.
2. Second `HandleConfirm()` fires `OnShotFired` with a `Vector2`.
3. `CombatMenuController`: selecting Shoot shows AimView and sets state to Aiming.
4. `CombatMenuController`: after `OnShotFired`, CommandPanel hides and state
   returns to OperatorSelection.
