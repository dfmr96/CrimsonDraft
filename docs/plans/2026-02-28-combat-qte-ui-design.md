# Combat QTE UI — Design Document
**Date:** 2026-02-28
**Scope:** Combat action menu + QTE panel activation (placeholder sprint)

---

## Goal

Add a navigable combat action menu with two test buttons ("Disparar" y "Cerrar") to the Combat scene using UI Toolkit. Pressing "Disparar" activates the QTE panel (placeholder for now). Pressing "Cerrar" hides it. No mouse input — joystick/keyboard navigation only.

---

## Architecture

Two separate `UIDocument` GameObjects in `Combat.unity`:

```
Combat.unity
├── CombatActionMenuDocument (UIDocument)   ← always visible in combat
│   └── CombatActionMenu.uxml
│       ├── btn-disparar
│       └── btn-cerrar
│
└── QTEDocument (UIDocument)                ← disabled by default
    └── QTEPanel.uxml
        └── (placeholder, QTE minigame implemented in next sprint)
```

Separate UIDocuments allow independent lifecycle management. `QTEDocument` is toggled via `gameObject.SetActive(true/false)`.

---

## Files to Create

### UI Toolkit Assets
```
Assets/Art/UI/
├── CombatActionMenu.uxml
├── CombatActionMenu.uss
├── QTEPanel.uxml
└── QTEPanel.uss
```

### Scripts
```
Assets/Scripts/Combat/UI/
├── CombatActionMenuView.cs    — MonoBehaviour on CombatActionMenuDocument
│                                Exposes btn-disparar and btn-cerrar clicked callbacks
├── QTEView.cs                 — MonoBehaviour on QTEDocument
│                                Exposes Show() / Hide()
└── CombatMenuController.cs    — VContainer service registered in CombatScope
                                 Connects menu actions to QTE panel visibility
```

### Prefabs
```
Assets/Prefabs/UI/
├── CombatActionMenuDocument.prefab   — UIDocument + CombatActionMenuView
└── QTEDocument.prefab                — UIDocument + QTEView, starts disabled
```

---

## Joystick Navigation

- `InputSystemUIInputModule` on the EventSystem handles `NavigationMoveEvent` and `NavigationSubmitEvent` automatically for UI Toolkit Buttons.
- `CombatActionMenuView.OnEnable()` calls `btn-disparar.Focus()` so the first button is selected on scene load.
- Two buttons in a `flex-direction: row` container — UI Toolkit calculates left/right navigation automatically from layout.
- Submit (joystick South / Enter) maps to `btn.clicked` via the InputSystemUIInputModule.
- When QTE opens: focus transfers to `QTEDocument`. When QTE closes: focus returns to the action menu.

---

## VContainer Integration

`CombatMenuController` registered in `CombatScope.cs`:

```csharp
builder.Register<CombatMenuController>(Lifetime.Scoped)
    .AsSelf()
    .AsImplementedInterfaces();
```

`CombatActionMenuView` and `QTEView` registered as components:

```csharp
builder.RegisterComponentInHierarchy<CombatActionMenuView>();
builder.RegisterComponentInHierarchy<QTEView>();
```

`CombatMenuController` receives both views via constructor injection and wires up the button callbacks in `Initialize()`.

---

## Expected Runtime Result

1. Combat scene loads → action menu visible, QTE panel hidden
2. Player navigates to "Disparar" with D-pad/arrow keys → button highlights
3. Player presses Confirm → QTE panel appears (placeholder), menu stays visible
4. Player navigates to "Cerrar" → presses Confirm → QTE panel hides

---

## Out of Scope (Next Sprints)

- QTE minigame bars (X/Y axis targeting)
- Dispersion calculation (3-layer system from prototype)
- Hit zone resolution and damage
- Heartbeat visual effects
- Weapon/ammo data as ScriptableObjects
