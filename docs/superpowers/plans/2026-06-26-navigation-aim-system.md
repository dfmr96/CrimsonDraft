# Navigation Aim System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a hold-to-aim mode to the player in navigation that auto-targets the nearest enemy, allows cycling targets with A/D, fires a raycast on C to start combat, and gives all operators a full ATB gauge when combat is triggered by a player shot.

**Architecture:** A new `PlayerAimController` MonoBehaviour sits on the same GameObject as `PlayerController`. It owns all aim logic — target list, rotation, cycling, and the shoot raycast. `PlayerController` exposes a minimal `IsAiming` flag and blocks movement when it is set. The ATB advantage is propagated through `StartCombatAsync → EncounterContext.Set → CombatOrchestrator.Initialize`.

**Tech Stack:** Unity 2022+, C# 9, VContainer (DI), MessagePipe (pub/sub), NavMeshAgent, Unity Input System, UniTask

## Global Constraints

- All files `#nullable enable`
- Serialized fields initialized with `null!`; injected fields with `null?` and set in `[Inject] Construct(...)`
- No `Co-Authored-By` trailers in commits
- Tests are EditMode only — no Play Mode required
- Run tests via Unity Test Runner or `mcp__UnityMCP__run_tests`
- Do not call `RegisterMessagePipe()` inside `NavigationScope` — reuse parent's `MessagePipeOptions`
- Use `[Preserve]` on constructors of pure-C# services to prevent IL2CPP stripping

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `Combat/ATBActorState.cs` | Modify | Add `FillGauge()` |
| `Combat/ATBSystem.cs` | Modify | Add `FillOperatorGauges()` |
| `Tests/EditMode/ATBSystemTests.cs` | Modify | Tests for new fill methods |
| `Infrastructure/Scenes/IEncounterContext.cs` | Modify | Add `OperatorsStartFull` property |
| `Infrastructure/Scenes/EncounterContext.cs` | Modify | Implement `OperatorsStartFull`; extend `Set()` |
| `Infrastructure/Scenes/ISceneTransitionService.cs` | Modify | Add `operatorsStartFull` param to `StartCombatAsync` |
| `Infrastructure/Scenes/SceneTransitionService.cs` | Modify | Forward param to `EncounterContext.Set()` |
| `Tests/EditMode/EncounterContextTests.cs` | Create | Tests for flag propagation |
| `Combat/CombatOrchestrator.cs` | Modify | Call `FillOperatorGauges()` when flag set |
| `Infrastructure/Input/IInputService.cs` | Modify | Add `AimFire` action |
| `Infrastructure/Input/InputService.cs` | Modify | Wire `AimFire` from `gameplayMap` |
| `Navigation/Player/PlayerController.cs` | Modify | Add `IsAiming` + `SetAiming()` + movement block |
| `Navigation/Enemy/EnemyNavAgent.cs` | Modify | Expose `public EncounterData? EncounterData` getter |
| `Navigation/Player/PlayerAimController.cs` | Create | Aim mode, target list, cycling, rotation, shoot |
| `Navigation/NavigationScope.cs` | Modify | Register `PlayerAimController` |

All paths are relative to `Game/CrimsonDraft/Assets/Scripts/` unless otherwise noted.

---

## Task 1: ATB gauge fill methods

**Files:**
- Modify: `Combat/ATBActorState.cs`
- Modify: `Combat/ATBSystem.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/ATBSystemTests.cs`

**Interfaces:**
- Produces: `ATBActorState.FillGauge()`, `ATBSystem.FillOperatorGauges()` — consumed by Task 3

- [ ] **Step 1: Write failing tests**

Add to `ATBSystemTests.cs` at the end of the class, before the closing `}`:

```csharp
[Test]
public void FillGauge_setsGaugeToOne()
{
    var state = new ATBActorState(new ATBActorConfig(0, ATBActorKind.Operator, 0.1f));
    state.FillGauge();
    Assert.AreEqual(1f, state.Gauge, 0.0001f);
}

[Test]
public void FillGauge_makesActorReady()
{
    var state = new ATBActorState(new ATBActorConfig(0, ATBActorKind.Operator, 0.1f));
    state.FillGauge();
    Assert.IsTrue(state.IsReady);
}

[Test]
public void FillOperatorGauges_setsAllOperatorsToReady()
{
    var sys = new ATBSystem();
    sys.Initialize(new[]
    {
        new ATBActorConfig(0, ATBActorKind.Operator, 0.1f),
        new ATBActorConfig(1, ATBActorKind.Operator, 0.2f),
    });
    sys.FillOperatorGauges();
    Assert.AreEqual(1f, sys.GetActor(0, ATBActorKind.Operator)!.Gauge, 0.0001f);
    Assert.AreEqual(1f, sys.GetActor(1, ATBActorKind.Operator)!.Gauge, 0.0001f);
}

[Test]
public void FillOperatorGauges_doesNotAffectEnemies()
{
    var sys = new ATBSystem();
    sys.Initialize(new[]
    {
        new ATBActorConfig(0, ATBActorKind.Operator, 0.1f),
        new ATBActorConfig(0, ATBActorKind.Enemy,    0.1f),
    });
    sys.FillOperatorGauges();
    Assert.AreEqual(1f, sys.GetActor(0, ATBActorKind.Operator)!.Gauge, 0.0001f);
    Assert.AreEqual(0f, sys.GetActor(0, ATBActorKind.Enemy)!.Gauge,    0.0001f);
}
```

- [ ] **Step 2: Run tests to verify they fail**

In Unity: Window → General → Test Runner → EditMode → filter `FillGauge` → Run.  
Expected: 4 failures with `CS0117: ATBActorState does not contain a definition for 'FillGauge'`.

Or via MCP: `run_tests(filter: "FillGauge")` — expect compile error or test failure.

- [ ] **Step 3: Add `FillGauge()` to `ATBActorState`**

In `Combat/ATBActorState.cs`, after the `MarkDead()` method (line 53), add:

```csharp
public void FillGauge() { this.Gauge = 1f; }
```

- [ ] **Step 4: Add `FillOperatorGauges()` to `ATBSystem`**

In `Combat/ATBSystem.cs`, after the `UpdateActorGaugeRate()` method, add:

```csharp
public void FillOperatorGauges()
{
    for (int i = 0; i < this.actors.Count; i++)
    {
        if (this.actors[i].Config.Kind == ATBActorKind.Operator)
            this.actors[i].FillGauge();
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Filter `FillGauge` in Test Runner. Expected: 4 PASS.

- [ ] **Step 6: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/ATBActorState.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/ATBSystem.cs
git add "Game/CrimsonDraft/Assets/Tests/EditMode/ATBSystemTests.cs"
git commit -m "feat(combat): add FillGauge and FillOperatorGauges for first-strike advantage"
```

---

## Task 2: EncounterContext first-strike flag

**Files:**
- Modify: `Infrastructure/Scenes/IEncounterContext.cs`
- Modify: `Infrastructure/Scenes/EncounterContext.cs`
- Modify: `Infrastructure/Scenes/ISceneTransitionService.cs`
- Modify: `Infrastructure/Scenes/SceneTransitionService.cs`
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/EncounterContextTests.cs`

**Interfaces:**
- Produces: `IEncounterContext.OperatorsStartFull`, `ISceneTransitionService.StartCombatAsync(..., bool operatorsStartFull = false)` — consumed by Tasks 3 and 7

- [ ] **Step 1: Write failing tests**

Create `Game/CrimsonDraft/Assets/Tests/EditMode/EncounterContextTests.cs`:

```csharp
using NUnit.Framework;
using CrimsonDraft.Infrastructure.Scenes;

namespace CrimsonDraft.Tests
{
    public sealed class EncounterContextTests
    {
        [Test]
        public void Set_withOperatorsStartFull_storesTrue()
        {
            var ctx = new EncounterContext();
            ctx.Set("enc-01", null, operatorsStartFull: true);
            Assert.IsTrue(ctx.OperatorsStartFull);
        }

        [Test]
        public void Set_withoutAdvantage_defaultsFalse()
        {
            var ctx = new EncounterContext();
            ctx.Set("enc-01", null);
            Assert.IsFalse(ctx.OperatorsStartFull);
        }

        [Test]
        public void Set_secondCallWithoutAdvantage_resetsFlagToFalse()
        {
            var ctx = new EncounterContext();
            ctx.Set("enc-01", null, operatorsStartFull: true);
            ctx.Set("enc-02", null);
            Assert.IsFalse(ctx.OperatorsStartFull);
        }

        [Test]
        public void Set_storesEncounterId()
        {
            var ctx = new EncounterContext();
            ctx.Set("my-encounter", null);
            Assert.AreEqual("my-encounter", ctx.CurrentEncounterId);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Filter `EncounterContext` in Test Runner. Expected: compile error — `EncounterContext.Set` does not accept 3 parameters / `OperatorsStartFull` not defined.

- [ ] **Step 3: Add `OperatorsStartFull` to `IEncounterContext`**

Replace `Infrastructure/Scenes/IEncounterContext.cs` with:

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public interface IEncounterContext
    {
        string?          CurrentEncounterId { get; }
        ScriptableObject? EncounterAsset    { get; }
        bool             OperatorsStartFull { get; }
    }
}
```

- [ ] **Step 4: Update `EncounterContext`**

Replace `Infrastructure/Scenes/EncounterContext.cs` with:

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public sealed class EncounterContext : IEncounterContext
    {
        public string?           CurrentEncounterId { get; private set; }
        public ScriptableObject? EncounterAsset     { get; private set; }
        public bool              OperatorsStartFull { get; private set; }

        [Preserve]
        public EncounterContext() { }

        public void Set(string encounterId, ScriptableObject? asset, bool operatorsStartFull = false)
        {
            this.CurrentEncounterId = encounterId;
            this.EncounterAsset     = asset;
            this.OperatorsStartFull = operatorsStartFull;
        }
    }
}
```

- [ ] **Step 5: Update `ISceneTransitionService`**

Replace `Infrastructure/Scenes/ISceneTransitionService.cs` with:

```csharp
#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public interface ISceneTransitionService
    {
        bool IsInCombat { get; }
        UniTask StartCombatAsync(string encounterId, ScriptableObject? encounterAsset = null, bool operatorsStartFull = false);
    }
}
```

- [ ] **Step 6: Update `SceneTransitionService.StartCombatAsync`**

In `Infrastructure/Scenes/SceneTransitionService.cs`, replace the `StartCombatAsync` method signature and the `encounterContext.Set` call:

```csharp
public async UniTask StartCombatAsync(string encounterId, UnityEngine.ScriptableObject? encounterAsset = null, bool operatorsStartFull = false)
{
    if (this.isInCombat)
        return;

    this.isInCombat = true;
    this.encounterContext.Set(encounterId, encounterAsset, operatorsStartFull);
    this.inputService.SwitchToCombat();

    await this.screenFader.FadeOutAsync();
    await SceneManager.LoadSceneAsync(CombatSceneName, LoadSceneMode.Additive).ToUniTask();
    this.cameraService.ActivateCombatCamera();
    await this.screenFader.FadeInAsync();
}
```

- [ ] **Step 7: Run tests to verify they pass**

Filter `EncounterContext` in Test Runner. Expected: 4 PASS. Also run all tests to ensure no regressions.

- [ ] **Step 8: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/IEncounterContext.cs
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/EncounterContext.cs
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/ISceneTransitionService.cs
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/SceneTransitionService.cs
git add "Game/CrimsonDraft/Assets/Tests/EditMode/EncounterContextTests.cs"
git commit -m "feat(combat): propagate first-strike flag through StartCombatAsync and EncounterContext"
```

---

## Task 3: CombatOrchestrator applies first-strike advantage

**Files:**
- Modify: `Combat/CombatOrchestrator.cs`

**Interfaces:**
- Consumes: `IEncounterContext.OperatorsStartFull` (Task 2), `ATBSystem.FillOperatorGauges()` (Task 1)

This task has no EditMode unit test — `CombatOrchestrator` is a MonoBehaviour with Unity lifecycle dependencies. Manual verification is in the editor (see Step 3).

- [ ] **Step 1: Edit `CombatOrchestrator.Initialize()`**

In `Combat/CombatOrchestrator.cs`, find the `IInitializable.Initialize()` method. After the line `this.atbSystem.Initialize(configs);`, add the advantage check:

```csharp
void IInitializable.Initialize()
{
    this.encounter = this.encounterContext.EncounterAsset as EncounterData;
    if (this.encounter == null) return;

    var configs = BuildATBConfigs(this.encounter, this.roster, this.atbGaugeDivisor);
    this.atbSystem.Initialize(configs);

    if (this.encounterContext.OperatorsStartFull)
        this.atbSystem.FillOperatorGauges();

    for (int i = 0; i < this.roster.Count; i++)
        this.menuView.SetOperatorDimmed(i, true);

    this.knownAliveEnemySlots.Clear();
    for (int i = 0; i < this.encounter.EnemySlots.Length; i++)
    {
        if (this.encounter.EnemySlots[i] != null)
            this.knownAliveEnemySlots.Add(i);
    }

    this.ecgFeedback = ResolveEcgFeedback();
    SyncAllEcgStates();
    this.initialized = true;
}
```

- [ ] **Step 2: Check Unity console for compile errors**

In Unity Editor: check the Console window (or run `read_console` via MCP). Expected: no errors.

- [ ] **Step 3: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/CombatOrchestrator.cs
git commit -m "feat(combat): fill operator ATB gauges on first-strike combat start"
```

---

## Task 4: Input — AimFire action

**Files:**
- Modify: `Infrastructure/Input/IInputService.cs`
- Modify: `Infrastructure/Input/InputService.cs`

**Interfaces:**
- Produces: `IInputService.AimFire` — consumed by Task 7

Note: The `AimFire` action must also be created in the Unity Input Action Asset (editor step, covered in Task 8).

- [ ] **Step 1: Add `AimFire` to `IInputService`**

In `Infrastructure/Input/IInputService.cs`, add after the `Aim` property:

```csharp
InputAction Aim     { get; }
InputAction AimFire { get; }
```

Full updated interface (only the new line changes — add `AimFire` between `Aim` and `Pause`):

```csharp
InputAction Move          { get; }
InputAction Interact      { get; }
InputAction OpenInventory { get; }
InputAction OpenMap       { get; }
InputAction Aim           { get; }
InputAction AimFire       { get; }
InputAction Pause         { get; }
InputAction Sprint        { get; }
```

- [ ] **Step 2: Wire `AimFire` in `InputService`**

In `Infrastructure/Input/InputService.cs`:

Add the backing property declaration after `Aim`:
```csharp
public InputAction Aim     { get; }
public InputAction AimFire { get; }
```

In the constructor, after the `Aim` line:
```csharp
Aim     = this.gameplayMap[nameof(Aim)];
AimFire = this.gameplayMap[nameof(AimFire)];
```

- [ ] **Step 3: Check Unity console for compile errors**

Check the Console window or run `read_console` via MCP. If the Input Action Asset does not yet have an `AimFire` action, Unity will throw a runtime exception when the Gameplay map is initialized (`throwIfNotFound` is not set for individual actions, but the indexer will throw `KeyNotFoundException`).

**Important:** The exception will only appear at runtime. The compile step will succeed. Proceed to Task 8 to add the action in the editor before testing.

- [ ] **Step 4: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Input/IInputService.cs
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Input/InputService.cs
git commit -m "feat(input): add AimFire action to IInputService and InputService"
```

---

## Task 5: PlayerController — IsAiming flag

**Files:**
- Modify: `Navigation/Player/PlayerController.cs`

**Interfaces:**
- Produces: `PlayerController.IsAiming { get; }`, `PlayerController.SetAiming(bool)` — consumed by Task 7

- [ ] **Step 1: Edit `PlayerController`**

Replace `Navigation/Player/PlayerController.cs` with:

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
        [SerializeField] private Rigidbody rb       = null!;
        [SerializeField] private Animator  animator = null!;
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed  = 7f;

        private static readonly int SpeedHash    = Animator.StringToHash("Speed");
        private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");

        private IInputService inputService = null!;
        private InputDevice?  lastDevice;

        public bool IsAiming { get; private set; }

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

        internal void SetAiming(bool value)
        {
            this.IsAiming = value;
            this.animator.SetBool(IsAimingHash, value);
        }

        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            this.lastDevice = ctx.control.device;
        }

        private void FixedUpdate()
        {
            if (this.IsAiming)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetFloat(SpeedHash, 0f);
                return;
            }

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

- [ ] **Step 2: Check Unity console for compile errors**

Check the Console window or `read_console`. Expected: no errors.

- [ ] **Step 3: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerController.cs
git commit -m "feat(navigation): add IsAiming flag to PlayerController"
```

---

## Task 6: EnemyNavAgent — expose EncounterData

**Files:**
- Modify: `Navigation/Enemy/EnemyNavAgent.cs`

**Interfaces:**
- Produces: `EnemyNavAgent.EncounterData` — consumed by Task 7

- [ ] **Step 1: Add public `EncounterData` getter**

In `Navigation/Enemy/EnemyNavAgent.cs`, find the line:

```csharp
public string EncounterId => this.encounterId;
```

Add the property directly below it:

```csharp
public string        EncounterId  => this.encounterId;
public EncounterData? EncounterData => this.encounterData;
```

- [ ] **Step 2: Check Unity console for compile errors**

Check the Console or `read_console`. Expected: no errors.

- [ ] **Step 3: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyNavAgent.cs
git commit -m "feat(navigation): expose EncounterData property on EnemyNavAgent"
```

---

## Task 7: PlayerAimController + NavigationScope registration

**Files:**
- Create: `Navigation/Player/PlayerAimController.cs`
- Modify: `Navigation/NavigationScope.cs`

**Interfaces:**
- Consumes: `PlayerController.IsAiming`, `PlayerController.SetAiming(bool)` (Task 5), `EnemyNavAgent.EncounterData` (Task 6), `IInputService.AimFire` (Task 4), `ISceneTransitionService.StartCombatAsync(..., operatorsStartFull)` (Task 2)

- [ ] **Step 1: Create `PlayerAimController.cs`**

Create `Navigation/Player/PlayerAimController.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation.Enemy;

namespace CrimsonDraft.Navigation.Player
{
    public sealed class PlayerAimController : MonoBehaviour
    {
        [SerializeField] private float    aimTurnSpeed  = 180f;
        [SerializeField] private float    aimRange      = 20f;
        [SerializeField] private LayerMask obstaclesMask;
        [SerializeField] private LayerMask enemyMask;

        private IInputService            inputService           = null!;
        private ISceneTransitionService  sceneTransitionService = null!;
        private EnemyNavAgent[]          cachedEnemies          = null!;
        private PlayerController         playerController       = null!;

        private readonly List<EnemyNavAgent> targets = new();
        private int   currentTargetIndex;
        private float cycleCooldown;
        private bool  previousAxisActive;

        [Inject]
        public void Construct(
            IInputService           inputService,
            ISceneTransitionService sceneTransitionService,
            EnemyNavAgent[]         cachedEnemies)
        {
            this.inputService           = inputService;
            this.sceneTransitionService = sceneTransitionService;
            this.cachedEnemies          = cachedEnemies;
        }

        private void Start()
        {
            this.playerController = GetComponent<PlayerController>();
        }

        private void Update()
        {
            if (this.cycleCooldown > 0f)
                this.cycleCooldown -= Time.deltaTime;

            if (this.inputService.Aim.WasPressedThisFrame())
                EnterAim();
            else if (this.inputService.Aim.WasReleasedThisFrame())
                ExitAim();

            if (!this.playerController.IsAiming) return;

            RotateTowardTarget();
            HandleCycle();
            HandleFire();
        }

        private void EnterAim()
        {
            BuildTargetList();
            this.currentTargetIndex = 0;
            this.playerController.SetAiming(true);
        }

        private void ExitAim()
        {
            this.targets.Clear();
            this.playerController.SetAiming(false);
        }

        private void BuildTargetList()
        {
            this.targets.Clear();
            foreach (var enemy in this.cachedEnemies)
            {
                if (enemy == null) continue;
                if (!enemy.gameObject.activeSelf) continue;
                var nav = enemy.GetComponent<NavMeshAgent>();
                if (nav == null || !nav.enabled) continue;
                this.targets.Add(enemy);
            }

            var playerPos = transform.position;
            this.targets.Sort((a, b) =>
                (a.transform.position - playerPos).sqrMagnitude
                    .CompareTo((b.transform.position - playerPos).sqrMagnitude));
        }

        private void RotateTowardTarget()
        {
            if (this.targets.Count == 0) return;

            var target = this.targets[this.currentTargetIndex];
            if (target == null || !target.gameObject.activeSelf)
            {
                BuildTargetList();
                this.currentTargetIndex = 0;
                if (this.targets.Count == 0) return;
                target = this.targets[0];
            }

            var dir = target.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;

            var targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, this.aimTurnSpeed * Time.deltaTime);
        }

        private void HandleCycle()
        {
            if (this.targets.Count <= 1) return;
            if (this.cycleCooldown > 0f) return;

            var x          = this.inputService.Move.ReadValue<Vector2>().x;
            var axisActive = Mathf.Abs(x) > 0.5f;

            if (axisActive && !this.previousAxisActive)
            {
                if (x > 0f)
                    this.currentTargetIndex = (this.currentTargetIndex + 1) % this.targets.Count;
                else
                    this.currentTargetIndex = (this.currentTargetIndex - 1 + this.targets.Count) % this.targets.Count;

                this.cycleCooldown = 0.3f;
            }

            this.previousAxisActive = axisActive;
        }

        private void HandleFire()
        {
            if (!this.inputService.AimFire.WasPressedThisFrame()) return;
            if (this.targets.Count == 0) return;
            if (this.sceneTransitionService.IsInCombat) return;

            var target = this.targets[this.currentTargetIndex];
            if (target == null || !target.gameObject.activeSelf) return;

            var encounterData = target.EncounterData;
            if (encounterData == null) return;

            var origin  = transform.position + Vector3.up * 0.8f;
            var forward = transform.forward;

            if (!Physics.Raycast(origin, forward, out var hit, this.aimRange, this.obstaclesMask | this.enemyMask))
                return;

            if (hit.collider.GetComponentInParent<EnemyNavAgent>() != target) return;

            this.sceneTransitionService.StartCombatAsync(
                target.EncounterId,
                encounterData,
                operatorsStartFull: true).Forget();
        }
    }
}
```

- [ ] **Step 2: Register `PlayerAimController` in `NavigationScope`**

In `Navigation/NavigationScope.cs`, find the line:

```csharp
builder.RegisterComponentInHierarchy<PlayerController>();
```

Add directly below it:

```csharp
builder.RegisterComponentInHierarchy<PlayerController>();
builder.RegisterComponentInHierarchy<PlayerAimController>();
```

- [ ] **Step 3: Check Unity console for compile errors**

Check the Console or `read_console`. Expected: no errors. If the Input Action Asset is missing the `AimFire` action, a `KeyNotFoundException` will appear at Play Mode entry — that's fixed in Task 8.

- [ ] **Step 4: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerAimController.cs
git add Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs
git commit -m "feat(navigation): add PlayerAimController with auto-aim, cycling, and first-strike trigger"
```

---

## Task 8: Unity Editor — Input Action Asset + Animator

**Files (Editor):**
- Input Action Asset (`Assets/Settings/...` or wherever it lives in the project)
- Player Animator Controller

This task has no code changes — it is editor configuration only.

- [ ] **Step 1: Locate the Input Action Asset**

In the Unity Editor, open the Project window and search for `t:InputActionAsset`. Open the asset.

- [ ] **Step 2: Add `AimFire` to the Gameplay map**

In the Input Action Asset editor:
1. Select the **Gameplay** action map
2. Click **+** to add a new action, name it `AimFire`
3. Set Action Type to **Button**
4. Add a binding: **Path → Keyboard → C** (key)
5. Optionally add a gamepad binding

- [ ] **Step 3: Verify `Aim` has a binding**

In the same Gameplay map, find the `Aim` action. If it has no binding:
1. Add binding: **Path → Keyboard → X** (key)
2. Optionally add a gamepad binding

- [ ] **Step 4: Save the Input Action Asset**

Click **Save Asset** in the Input Action Asset editor toolbar.

- [ ] **Step 5: Add `IsAiming` parameter to the Animator**

Open the Player's Animator Controller (find it in the Project window — the asset referenced by the player's `Animator` component).

In the **Parameters** tab, click **+** → **Bool** → name it `IsAiming`.

Wire the `IsAiming` parameter to a transition from your locomotion state to your aiming animation state (if aiming animation exists). If the aiming animation is not yet ready, add the parameter now so `PlayerController.SetAiming()` does not throw — the transition can be added later.

- [ ] **Step 6: Configure `PlayerAimController` on the Player prefab/GameObject**

In the scene or prefab, select the Player GameObject (the one that has `PlayerController`):
1. Add `PlayerAimController` component (or verify it was already found by `RegisterComponentInHierarchy` — the component must physically exist on the GameObject)
2. Set **Aim Turn Speed**: `180`
3. Set **Aim Range**: `20`
4. Set **Obstacles Mask**: select the layers that represent geometry (walls, furniture — whatever the `EnemyDetectionSensor` uses for obstruction)
5. Set **Enemy Mask**: select the layer used on enemy colliders

- [ ] **Step 7: Enter Play Mode and verify no console errors**

Press Play. Check the Console. Expected: no errors or exceptions. If `KeyNotFoundException: AimFire` appears, the Input Action Asset was not saved correctly — repeat Steps 2–4.

- [ ] **Step 8: Commit**

```
git add Game/CrimsonDraft/Assets
git commit -m "feat(navigation): configure AimFire input binding and IsAiming animator parameter"
```

---

## Manual Verification Checklist

After all tasks are complete, verify in Play Mode:

- [ ] **Hold X** → player enters aiming pose (animator transitions to aiming state), movement is blocked
- [ ] **Release X** → player exits aiming pose, movement resumes normally
- [ ] **With enemies in the scene, hold X** → player auto-rotates toward the nearest enemy
- [ ] **Hold X, press D** → player rotates to next enemy in the list; press A → previous enemy (cycles)
- [ ] **Hold X, press C with clear LOS** → combat scene loads; all operators start with full ATB (their action buttons light up immediately)
- [ ] **Hold X, press C with wall between player and enemy** → nothing happens (raycast blocked)
- [ ] **Hold X, press C with no enemies** → nothing happens
- [ ] **Combat started normally by enemy catch** → operators start with empty ATB (no advantage)
- [ ] **`CombatTrigger` proximity trigger** → operators start with empty ATB (no advantage)
