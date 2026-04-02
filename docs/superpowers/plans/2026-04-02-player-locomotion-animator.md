# Player Locomotion Animator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the 2D directional animator with a 3D 1D-BlendTree locomotion system (Idle/Walk/Run) driven by a `Speed` float, and add a Sprint input action to `PlayerController`.

**Architecture:** `PlayerController` reads `Sprint.IsPressed()` each `FixedUpdate` to pick `walkSpeed` or `runSpeed`, then writes 0 / 0.5 / 1.0 to the cached `speedHash` animator parameter. The `PlayerAnimator.controller` is rebuilt from scratch with a single `LocomotionBlend` blend tree state. Sprint is a new `Button` action in the `Gameplay` input map.

**Tech Stack:** Unity 6 · C# 9 · VContainer · Unity Input System · Unity Animator · Unity MCP

**Spec:** [docs/superpowers/specs/2026-04-02-player-locomotion-animator-design.md](../specs/2026-04-02-player-locomotion-animator-design.md)
**GDD:** [[Sistema de Movimiento]]

---

## File Map

| File | Change |
|------|--------|
| `Assets/Input/CrimsonDraftControls.inputactions` | Add `Sprint` action + 2 bindings to `Gameplay` map |
| `Assets/Scripts/Infrastructure/Input/IInputService.cs` | Add `InputAction Sprint { get; }` |
| `Assets/Scripts/Infrastructure/Input/InputService.cs` | Bind `Sprint = this.gameplayMap[nameof(Sprint)]` |
| `Assets/Scripts/Navigation/Player/PlayerController.cs` | Rename `moveSpeed`→`walkSpeed`, add `runSpeed`, `animator`, `speedHash`, update `FixedUpdate` |
| `Assets/Animations/Player/PlayerAnimator.controller` | Rebuild as 3D 1D-BlendTree via Unity MCP |
| `Prefabs/Characters/Player.prefab` | Wire `Animator` reference in `PlayerController` component |

---

## Task 1: Sprint Input Action

**Files:**
- Modify: `Assets/Input/CrimsonDraftControls.inputactions`

- [ ] **Step 1: Add Sprint action to Gameplay map**

In `CrimsonDraftControls.inputactions`, add to the `Gameplay` map's `"actions"` array (after `"Pause"`):

```json
{
    "name": "Sprint",
    "type": "Button",
    "id": "b1c2d3e4-f5a6-7890-abcd-ef1234567890",
    "expectedControlType": "",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
}
```

- [ ] **Step 2: Add Sprint bindings**

In the same `Gameplay` map's `"bindings"` array (after the `Pause` bindings), add:

```json
{
    "name": "",
    "id": "c2d3e4f5-a6b7-8901-bcde-f12345678901",
    "path": "<Keyboard>/v",
    "interactions": "",
    "processors": "",
    "groups": "",
    "action": "Sprint",
    "isComposite": false,
    "isPartOfComposite": false
},
{
    "name": "",
    "id": "d3e4f5a6-b7c8-9012-cdef-123456789012",
    "path": "<Gamepad>/buttonWest",
    "interactions": "",
    "processors": "",
    "groups": "",
    "action": "Sprint",
    "isComposite": false,
    "isPartOfComposite": false
}
```

- [ ] **Step 3: Verify Unity reimports the asset without errors**

Open Unity, check Console — no errors about CrimsonDraftControls.

- [ ] **Step 4: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Input/CrimsonDraftControls.inputactions"
git commit -m "feat(navigation): add Sprint input action (V / ButtonWest)"
```

---

## Task 2: IInputService + InputService

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Input/IInputService.cs`
- Modify: `Assets/Scripts/Infrastructure/Input/InputService.cs`

- [ ] **Step 1: Add Sprint property to IInputService**

In `IInputService.cs`, add after `InputAction Pause { get; }`:

```csharp
InputAction Sprint { get; }
```

Full updated interface:

```csharp
#nullable enable

using System;
using UnityEngine.InputSystem;

namespace CrimsonDraft.Infrastructure.Input
{
    public interface IInputService : IDisposable
    {
        InputAction Move { get; }
        InputAction Interact { get; }
        InputAction OpenInventory { get; }
        InputAction Pause { get; }
        InputAction Sprint { get; }

        InputAction CombatNavigate { get; }
        InputAction CombatConfirm { get; }
        InputAction CombatCancel { get; }
        InputAction CombatUseItem { get; }

        InputAction UINavigate { get; }
        InputAction UIConfirm  { get; }
        InputAction UICancel   { get; }

        void SwitchToGameplay();
        void SwitchToCombat();
        void SwitchToUI();
    }
}
```

- [ ] **Step 2: Bind Sprint in InputService**

In `InputService.cs`, add `public InputAction Sprint { get; }` field and bind it in the constructor alongside `Pause`. Full updated constructor region:

```csharp
#nullable enable

using System;
using VContainer;
using VContainer.Unity;
using UnityEngine.InputSystem;

namespace CrimsonDraft.Infrastructure.Input
{
    public sealed class InputService : IInputService, IInitializable, IDisposable
    {
        private const string GameplayMapName = "Gameplay";
        private const string CombatMapName   = "Combat";
        private const string UIMapName       = "UI";

        private const string NavigateAction = "Navigate";
        private const string ConfirmAction  = "Confirm";
        private const string CancelAction   = "Cancel";
        private const string UseItemAction  = "UseItem";

        private readonly InputActionAsset asset;
        private readonly InputActionMap gameplayMap;
        private readonly InputActionMap combatMap;
        private readonly InputActionMap uiMap;

        public InputAction Move { get; }
        public InputAction Interact { get; }
        public InputAction OpenInventory { get; }
        public InputAction Pause { get; }
        public InputAction Sprint { get; }
        public InputAction CombatNavigate { get; }
        public InputAction CombatConfirm { get; }
        public InputAction CombatCancel { get; }
        public InputAction CombatUseItem { get; }

        public InputAction UINavigate { get; }
        public InputAction UIConfirm  { get; }
        public InputAction UICancel   { get; }

        [Preserve]
        public InputService(InputActionAsset asset)
        {
            this.asset = asset;
            this.gameplayMap = asset.FindActionMap(GameplayMapName, throwIfNotFound: true);
            this.combatMap   = asset.FindActionMap(CombatMapName,   throwIfNotFound: true);
            this.uiMap       = asset.FindActionMap(UIMapName,        throwIfNotFound: true);

            Move          = this.gameplayMap[nameof(Move)];
            Interact      = this.gameplayMap[nameof(Interact)];
            OpenInventory = this.gameplayMap[nameof(OpenInventory)];
            Pause         = this.gameplayMap[nameof(Pause)];
            Sprint        = this.gameplayMap[nameof(Sprint)];

            CombatNavigate = this.combatMap[NavigateAction];
            CombatConfirm  = this.combatMap[ConfirmAction];
            CombatCancel   = this.combatMap[CancelAction];
            CombatUseItem  = this.combatMap[UseItemAction];

            UINavigate = this.uiMap[NavigateAction];
            UIConfirm  = this.uiMap["Submit"];
            UICancel   = this.uiMap[CancelAction];
        }

        void IInitializable.Initialize() => SwitchToGameplay();

        public void SwitchToGameplay()
        {
            DisableAll();
            this.gameplayMap.Enable();
        }

        public void SwitchToCombat()
        {
            DisableAll();
            this.combatMap.Enable();
        }

        public void SwitchToUI()
        {
            DisableAll();
            this.uiMap.Enable();
        }

        void IDisposable.Dispose()
        {
            DisableAll();
            this.asset.Disable();
        }

        private void DisableAll()
        {
            this.gameplayMap.Disable();
            this.combatMap.Disable();
            this.uiMap.Disable();
        }
    }
}
```

- [ ] **Step 3: Verify compilation**

Check Unity Console — no compilation errors. `InputService` should compile with `Sprint` bound.

- [ ] **Step 4: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/Input/IInputService.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/Input/InputService.cs"
git commit -m "feat(navigation): expose Sprint InputAction in IInputService"
```

---

## Task 3: Update PlayerController

**Files:**
- Modify: `Assets/Scripts/Navigation/Player/PlayerController.cs`

- [ ] **Step 1: Write the updated PlayerController**

Full file replacement:

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using VContainer;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Navigation.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Rigidbody rb = null!;
        [SerializeField] private Animator animator = null!;
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed = 7f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private IInputService inputService = null!;
        private InputDevice? lastDevice;

        [Inject]
        public void Construct(IInputService inputService)
        {
            this.inputService = inputService;
            this.inputService.Move.performed += OnMovePerformed;
        }

        private void OnDestroy()
        {
            if (this.inputService != null)
                this.inputService.Move.performed -= OnMovePerformed;
        }

        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            this.lastDevice = ctx.control.device;
        }

        private void FixedUpdate()
        {
            var raw = this.inputService.Move.ReadValue<Vector2>();

            if (raw.sqrMagnitude < 0.01f)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetFloat(SpeedHash, 0f);
                return;
            }

            var direction = this.lastDevice is Gamepad
                ? raw.normalized
                : Quantize8Way(raw);

            var moveDir = new Vector3(direction.x, 0f, direction.y);
            transform.forward = moveDir;

            var isSprinting = this.inputService.Sprint.IsPressed();
            var speed       = isSprinting ? this.runSpeed  : this.walkSpeed;
            var animSpeed   = isSprinting ? 1f             : 0.5f;

            this.rb.linearVelocity = moveDir * speed;
            this.animator.SetFloat(SpeedHash, animSpeed);
        }

        private static Vector2 Quantize8Way(Vector2 input)
        {
            return new Vector2(
                Mathf.Round(input.x),
                Mathf.Round(input.y)
            ).normalized;
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

Check Unity Console — no compilation errors. `SpeedHash` is `static readonly int`, no allocations in `FixedUpdate`.

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerController.cs"
git commit -m "feat(navigation): add sprint + animator Speed parameter to PlayerController"
```

---

## Task 4: Rebuild PlayerAnimator Controller (Unity MCP)

**Files:**
- Modify: `Assets/Animations/Player/PlayerAnimator.controller` (rebuilt via MCP)

The goal: one state (`LocomotionBlend`), one 1D BlendTree, parameter `Speed` (float), three clips at thresholds 0 / 0.5 / 1.0.

Animation clips (embedded in FBX files):
- Idle → `Assets/Art/Models/FBX_Export/HumanoidBase_Overlapping@Breathing Idle.fbx`
- Walk → `Assets/Art/Models/FBX_Export/HumanoidBase_Overlapping@Walking.fbx`
- Run  → `Assets/Art/Models/FBX_Export/HumanoidBase_Overlapping@Running (1).fbx`

- [ ] **Step 1: Use Unity MCP to rebuild the animator controller**

Call `manage_animation` MCP tool to configure `PlayerAnimator`:

```
action: "configure_animator"
controllerPath: "Animations/Player/PlayerAnimator.controller"
parameters:
  - name: "Speed", type: "Float", defaultValue: 0
states:
  - name: "LocomotionBlend"
    isDefault: true
    blendTree:
      type: "1D"
      parameter: "Speed"
      motions:
        - threshold: 0.0,  clip: "Art/Models/FBX_Export/HumanoidBase_Overlapping@Breathing Idle.fbx"
        - threshold: 0.5,  clip: "Art/Models/FBX_Export/HumanoidBase_Overlapping@Walking.fbx"
        - threshold: 1.0,  clip: "Art/Models/FBX_Export/HumanoidBase_Overlapping@Running (1).fbx"
```

If `manage_animation` does not support blend tree creation directly, use `execute_menu_item` to open the Animator window + `manage_asset` to create the controller, then wire clips manually.

- [ ] **Step 2: Verify controller in Unity**

Check Console for errors. Open the Animator window and confirm:
- Parameter `Speed` (float) exists
- Default state is `LocomotionBlend`
- Blend tree has 3 clips at thresholds 0 / 0.5 / 1.0

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Animations/Player/PlayerAnimator.controller"
git commit -m "feat(navigation): rebuild PlayerAnimator with 3D locomotion blend tree"
```

---

## Task 5: Wire Animator in Player Prefab (Unity MCP)

**Files:**
- Modify: `Prefabs/Characters/Player.prefab`

- [ ] **Step 1: Check current Animator component on Player prefab**

Use `find_gameobjects` or `manage_prefabs` MCP to inspect `Player.prefab` and confirm:
- There is an `Animator` component
- Its `Controller` field references a controller (may be the old 2D one or the FBX_Export one)

- [ ] **Step 2: Set PlayerAnimator.controller on the Animator component**

Use `manage_components` MCP to set the Animator's `runtimeAnimatorController` to `Assets/Animations/Player/PlayerAnimator.controller`.

- [ ] **Step 3: Set Animator reference on PlayerController component**

Use `manage_components` MCP to set `PlayerController.animator` to the `Animator` component on the same GameObject.

- [ ] **Step 4: Verify in Play Mode**

Enter Play Mode. Walk with WASD — character should play Walk animation. Hold V — character should play Run animation. Release movement — character should return to Idle.

Check Console — no errors.

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Prefabs/Characters/Player.prefab"
git commit -m "feat(navigation): wire PlayerAnimator and Animator ref in Player prefab"
```
