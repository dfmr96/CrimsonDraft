# Player Movement Control Scheme (Modern / Classic) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract player movement into a Strategy pattern (`IPlayerMovementStrategy`) so `PlayerController` can switch between the existing camera-relative "Modern" scheme and a new "Classic" tank-control scheme, driven by a persisted setting wired to the previously-locked "Control" knob in the Settings menu.

**Architecture:** Two stateless-except-for-injected-deps strategy classes (`ModernPlayerMovementStrategy`, `ClassicPlayerMovementStrategy`) implement a shared interface that returns a small result struct (direction + sprint-allowed flag) each `FixedUpdate`. `PlayerController` holds both, constructed once, and picks between them each frame by reading a new `IControlSchemeService` (same shape as the existing `IGraphicsSettingsService`/`IAudioSettingsService`, `PlayerPrefs`-backed, registered as a `GameLifetimeScope` singleton so it survives scene loads). `GeneralMenuController`'s "Control" knob (currently a no-op) is wired to call `SetScheme`.

**Tech Stack:** Unity 6000.3.9f1, C# with `#nullable enable`, VContainer (DI), Unity Input System, NUnit EditMode tests (run via Unity Test Runner or the MCP `run_tests` tool — this project has no CLI test command).

**Spec:** [[Sistema de Movimiento]] (`Design/GDD/Sistema de Movimiento.md`) is the design source of truth for this feature ("Esquema Modern", "Esquema Classic — Tank Controls", "Selector de Esquema" sections). `Design/Plans/superpowers/specs/2026-09-01-player-movement-control-scheme-design.md` is the approved technical spec this plan translates into tasks — it contains the exact interface/struct shapes used below.

## Global Constraints

- All new/modified files use `#nullable enable` at the top (project-wide convention, see `CLAUDE.md`).
- Serialized fields use `null!`; injected fields start `null?` or are assigned in `[Inject]`-attributed `Construct(...)` methods.
- Plain C# service classes (not MonoBehaviours) get `[UnityEngine.Scripting.Preserve]` on their constructor.
- VContainer scope hierarchy: `IControlSchemeService` must be registered in `GameLifetimeScope` (root), never re-registered in a child scope — `NavigationScope` and `PlayerController` resolve it automatically through the parent, the same way `IInputService` already does.
- Tests live under `Game/CrimsonDraft/Assets/Tests/EditMode/` and are plain C# fakes, no mocking framework (see `CombatMenuControllerTests.cs`/`CombatOrchestratorTests.cs` for the established pattern in this codebase).
- **There is no CLI test runner.** "Run test" steps below mean: use the MCP `run_tests` tool (`mode: "EditMode"`, `test_names` filtered to the class/test) if the MCP-for-Unity bridge is connected, or Window → General → Test Runner in the Unity Editor otherwise. After every code change, also request a script compile (MCP `refresh_unity` with `compile: "request"`, or let the Editor auto-compile) and check the console for errors before running tests.
- Do not add `Co-Authored-By` trailers to commits (see `CLAUDE.md`).
- Commit after every task.

---

### Task 1: Movement strategy contracts

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/Movement/PlayerMovementResult.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/Movement/IPlayerMovementStrategy.cs`

**Interfaces:**
- Produces: `PlayerMovementResult` (struct: `Vector3 Direction`, `bool AllowSprint`, static `Idle` = `(Vector3.zero, true)`), `IPlayerMovementStrategy.Tick(Transform, Vector2, InputDevice?, bool, float) : PlayerMovementResult` — every later task depends on these two exact shapes.

This is a pure data/contract task (no behavior to unit-test) — the deliverable is "compiles cleanly and matches the shapes every other task depends on."

- [ ] **Step 1: Create `PlayerMovementResult.cs`**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Player.Movement
{
    // Returned by IPlayerMovementStrategy.Tick() every FixedUpdate. Direction is a unit
    // horizontal vector (or Vector3.zero when there's nothing to move toward this frame);
    // AllowSprint lets a strategy force walk speed regardless of the Sprint button (used by
    // ClassicPlayerMovementStrategy's backpedal, which is never a run in the source material).
    public readonly struct PlayerMovementResult
    {
        public Vector3 Direction   { get; }
        public bool    AllowSprint { get; }

        public PlayerMovementResult(Vector3 direction, bool allowSprint)
        {
            this.Direction   = direction;
            this.AllowSprint = allowSprint;
        }

        public static PlayerMovementResult Idle => new PlayerMovementResult(Vector3.zero, true);
    }
}
```

- [ ] **Step 2: Create `IPlayerMovementStrategy.cs`**

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;

namespace CrimsonDraft.Navigation.Player.Movement
{
    // Called every FixedUpdate, unconditionally -- including frames where the caller won't
    // act on the result (aiming, stick at rest). ModernPlayerMovementStrategy relies on this:
    // its internal camera-basis bookkeeping must never miss a held-direction change, even
    // while the player is aiming. A strategy may mutate playerTransform's rotation directly
    // (Modern snaps facing to the move direction; Classic turns gradually) as a side effect.
    public interface IPlayerMovementStrategy
    {
        PlayerMovementResult Tick(Transform playerTransform, Vector2 rawInput, InputDevice? lastDevice, bool isAiming, float deltaTime);
    }
}
```

- [ ] **Step 3: Compile check**

Request a script compile (MCP `refresh_unity` with `compile: "request"`, `scope: "all"`) and read the console for errors.
Expected: no errors referencing `PlayerMovementResult.cs` or `IPlayerMovementStrategy.cs`.

- [ ] **Step 4: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Player/Movement/PlayerMovementResult.cs" "Game/CrimsonDraft/Assets/Scripts/Navigation/Player/Movement/IPlayerMovementStrategy.cs"
git commit -m "feat(navigation): add IPlayerMovementStrategy contract"
```

---

### Task 2: `ModernPlayerMovementStrategy`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/Movement/ModernPlayerMovementStrategy.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/ModernPlayerMovementStrategyTests.cs`

**Interfaces:**
- Consumes: `IPlayerMovementStrategy`, `PlayerMovementResult` (Task 1); `ICameraRelativeMovementService` (existing, `Vector3 Forward { get; }`, `Vector3 Right { get; }`, `void Tick(Vector2 heldDirection)`, in `CrimsonDraft.Navigation.CamaraSystem`).
- Produces: `ModernPlayerMovementStrategy(ICameraRelativeMovementService)` constructor, used by Task 6.

This extracts today's `PlayerController.FixedUpdate` camera-relative logic verbatim, with one real behavior fix folded in: the original code never touched `transform.forward` while aiming (the aiming early-return happened *before* the direction/rotation computation). The extracted version must preserve that — only the `cameraRelativeMovementService.Tick(direction)` call runs unconditionally; everything else short-circuits on `isAiming`.

- [ ] **Step 1: Write the failing tests**

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Player.Movement;

namespace CrimsonDraft.Tests
{
    public sealed class ModernPlayerMovementStrategyTests
    {
        private FakeCameraRelativeMovementService cameraService = null!;
        private ModernPlayerMovementStrategy      strategy      = null!;
        private Transform                         playerTransform = null!;
        private Gamepad?                          gamepad;

        [SetUp]
        public void SetUp()
        {
            this.cameraService   = new FakeCameraRelativeMovementService();
            this.strategy        = new ModernPlayerMovementStrategy(this.cameraService);
            this.playerTransform = new GameObject("Player").transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (this.playerTransform != null) Object.DestroyImmediate(this.playerTransform.gameObject);
            if (this.gamepad != null) InputSystem.RemoveDevice(this.gamepad);
        }

        [Test]
        public void Tick_alwaysTicksCameraService_evenWhileAiming()
        {
            this.strategy.Tick(this.playerTransform, new Vector2(1f, 0f), null, isAiming: true, deltaTime: 0.02f);

            Assert.AreEqual(1, this.cameraService.TickCallCount);
        }

        [Test]
        public void Tick_whileAiming_returnsIdle()
        {
            var result = this.strategy.Tick(this.playerTransform, new Vector2(1f, 0f), null, isAiming: true, deltaTime: 0.02f);

            Assert.AreEqual(Vector3.zero, result.Direction);
        }

        [Test]
        public void Tick_whileAiming_doesNotRotateTransform()
        {
            this.playerTransform.forward = Vector3.back;

            this.strategy.Tick(this.playerTransform, new Vector2(1f, 0f), null, isAiming: true, deltaTime: 0.02f);

            Assert.AreEqual(Vector3.back, this.playerTransform.forward);
        }

        [Test]
        public void Tick_keyboardDevice_quantizesDiagonalInput_andCombinesRightForward()
        {
            this.cameraService.Right   = Vector3.right;
            this.cameraService.Forward = Vector3.forward;

            var result = this.strategy.Tick(this.playerTransform, new Vector2(0.6f, 0.6f), null, isAiming: false, deltaTime: 0.02f);

            var expected = new Vector3(1f, 0f, 1f).normalized;
            Assert.Less(Vector3.Distance(expected, result.Direction), 0.001f);
            Assert.AreEqual(new Vector2(1f, 1f).normalized, this.cameraService.LastTickDirection);
        }

        [Test]
        public void Tick_gamepadDevice_normalizesInput_andCombinesRightForward()
        {
            this.gamepad = InputSystem.AddDevice<Gamepad>();
            this.cameraService.Right   = Vector3.right;
            this.cameraService.Forward = Vector3.forward;

            var result = this.strategy.Tick(this.playerTransform, new Vector2(0.3f, 0.4f), this.gamepad, isAiming: false, deltaTime: 0.02f);

            var expected = new Vector3(0.6f, 0f, 0.8f);
            Assert.Less(Vector3.Distance(expected, result.Direction), 0.001f);
            Assert.Less(Vector2.Distance(new Vector2(0.6f, 0.8f), this.cameraService.LastTickDirection), 0.001f);
        }

        [Test]
        public void Tick_nonZeroDirection_setsTransformForward()
        {
            this.cameraService.Right   = Vector3.right;
            this.cameraService.Forward = Vector3.forward;

            var result = this.strategy.Tick(this.playerTransform, new Vector2(0f, 1f), null, isAiming: false, deltaTime: 0.02f);

            // transform.forward round-trips through a quaternion internally, so compare with a
            // tolerance rather than exact float equality.
            Assert.Less(Vector3.Distance(result.Direction, this.playerTransform.forward), 0.0001f);
        }

        [Test]
        public void Tick_zeroInput_returnsIdleDirection_andDoesNotRotateTransform()
        {
            this.playerTransform.forward = Vector3.back;
            this.cameraService.Right     = Vector3.right;
            this.cameraService.Forward   = Vector3.forward;

            var result = this.strategy.Tick(this.playerTransform, Vector2.zero, null, isAiming: false, deltaTime: 0.02f);

            Assert.AreEqual(Vector3.zero, result.Direction);
            Assert.AreEqual(Vector3.back, this.playerTransform.forward);
        }

        [Test]
        public void Tick_alwaysAllowsSprint()
        {
            this.cameraService.Right   = Vector3.right;
            this.cameraService.Forward = Vector3.forward;

            var result = this.strategy.Tick(this.playerTransform, new Vector2(0f, 1f), null, isAiming: false, deltaTime: 0.02f);

            Assert.IsTrue(result.AllowSprint);
        }

        private sealed class FakeCameraRelativeMovementService : ICameraRelativeMovementService
        {
            public int     TickCallCount     { get; private set; }
            public Vector2 LastTickDirection { get; private set; }
            public Vector3 Forward           { get; set; } = Vector3.forward;
            public Vector3 Right             { get; set; } = Vector3.right;

            public void Tick(Vector2 heldDirection)
            {
                this.TickCallCount++;
                this.LastTickDirection = heldDirection;
            }
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Compile (this will fail — `ModernPlayerMovementStrategy` doesn't exist yet) and confirm the console shows a `CS0246`-style "type or namespace could not be found" error referencing `ModernPlayerMovementStrategy`.

- [ ] **Step 3: Write `ModernPlayerMovementStrategy.cs`**

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using CrimsonDraft.Navigation.CamaraSystem;

namespace CrimsonDraft.Navigation.Player.Movement
{
    // Camera-relative movement -- extracted from PlayerController unchanged. See
    // ICameraRelativeMovementService for the "held direction survives a fixed-camera cut"
    // policy this wraps.
    public sealed class ModernPlayerMovementStrategy : IPlayerMovementStrategy
    {
        private readonly ICameraRelativeMovementService cameraRelativeMovementService;

        public ModernPlayerMovementStrategy(ICameraRelativeMovementService cameraRelativeMovementService)
        {
            this.cameraRelativeMovementService = cameraRelativeMovementService;
        }

        public PlayerMovementResult Tick(Transform playerTransform, Vector2 rawInput, InputDevice? lastDevice, bool isAiming, float deltaTime)
        {
            var direction = lastDevice is Gamepad
                ? rawInput.normalized
                : Quantize8Way(rawInput);

            // Always ticked, even while aiming or at rest -- a held direction that changes has
            // to be caught the instant it happens, whether or not the player can currently act
            // on it (see CameraRelativeMovementService.ShouldResampleBasis).
            this.cameraRelativeMovementService.Tick(direction);

            if (isAiming) return PlayerMovementResult.Idle;

            var moveDir = this.cameraRelativeMovementService.Right * direction.x
                        + this.cameraRelativeMovementService.Forward * direction.y;
            moveDir = moveDir.sqrMagnitude > 0.0001f ? moveDir.normalized : Vector3.zero;

            if (moveDir != Vector3.zero)
                playerTransform.forward = moveDir;

            return new PlayerMovementResult(moveDir, allowSprint: true);
        }

        private static Vector2 Quantize8Way(Vector2 input) =>
            new Vector2(Mathf.Round(input.x), Mathf.Round(input.y)).normalized;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run `ModernPlayerMovementStrategyTests` via the MCP `run_tests` tool or Test Runner.
Expected: 8 tests, all PASS.

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Player/Movement/ModernPlayerMovementStrategy.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/ModernPlayerMovementStrategyTests.cs"
git commit -m "feat(navigation): extract ModernPlayerMovementStrategy from PlayerController"
```

---

### Task 3: `ClassicPlayerMovementStrategy`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/Movement/ClassicPlayerMovementStrategy.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/ClassicPlayerMovementStrategyTests.cs`

**Interfaces:**
- Consumes: `IPlayerMovementStrategy`, `PlayerMovementResult` (Task 1).
- Produces: `ClassicPlayerMovementStrategy()` (parameterless constructor), used by Task 6.

Per [[Sistema de Movimiento]] "Esquema Classic — Tank Controls": horizontal stick rotates the character at a fixed rate (world Y axis, proportional to deflection, suppressed while aiming); vertical stick moves along the character's *current* facing (forward or backward, no strafe); backpedal always forces walk speed (`AllowSprint = false`); turning in place with no vertical input is valid.

- [ ] **Step 1: Write the failing tests**

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Player.Movement;

namespace CrimsonDraft.Tests
{
    public sealed class ClassicPlayerMovementStrategyTests
    {
        private ClassicPlayerMovementStrategy strategy        = null!;
        private Transform                     playerTransform = null!;

        [SetUp]
        public void SetUp()
        {
            this.strategy        = new ClassicPlayerMovementStrategy();
            this.playerTransform = new GameObject("Player").transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (this.playerTransform != null) Object.DestroyImmediate(this.playerTransform.gameObject);
        }

        [Test]
        public void Tick_positiveX_rotatesAroundWorldUp_atTurnSpeedTimesDeltaTime()
        {
            this.strategy.Tick(this.playerTransform, new Vector2(1f, 0f), null, isAiming: false, deltaTime: 0.5f);

            // turnSpeedDegPerSec (180) * x (1) * deltaTime (0.5) = 90 degrees around world up.
            var expected = Quaternion.AngleAxis(90f, Vector3.up) * Vector3.forward;
            Assert.Less(Vector3.Distance(expected, this.playerTransform.forward), 0.01f);
        }

        [Test]
        public void Tick_negativeX_rotatesTheOppositeWay()
        {
            this.strategy.Tick(this.playerTransform, new Vector2(-1f, 0f), null, isAiming: false, deltaTime: 0.5f);

            var expected = Quaternion.AngleAxis(-90f, Vector3.up) * Vector3.forward;
            Assert.Less(Vector3.Distance(expected, this.playerTransform.forward), 0.01f);
        }

        [Test]
        public void Tick_zeroX_doesNotRotate()
        {
            this.strategy.Tick(this.playerTransform, new Vector2(0f, 1f), null, isAiming: false, deltaTime: 0.5f);

            Assert.AreEqual(Vector3.forward, this.playerTransform.forward);
        }

        [Test]
        public void Tick_positiveY_returnsCurrentForward_allowsSprint()
        {
            this.playerTransform.forward = Vector3.right;

            var result = this.strategy.Tick(this.playerTransform, new Vector2(0f, 1f), null, isAiming: false, deltaTime: 0.02f);

            // transform.forward round-trips through a quaternion internally, so compare with a
            // tolerance rather than exact float equality.
            Assert.Less(Vector3.Distance(Vector3.right, result.Direction), 0.0001f);
            Assert.IsTrue(result.AllowSprint);
        }

        [Test]
        public void Tick_negativeY_returnsOppositeOfForward_disallowsSprint()
        {
            this.playerTransform.forward = Vector3.right;

            var result = this.strategy.Tick(this.playerTransform, new Vector2(0f, -1f), null, isAiming: false, deltaTime: 0.02f);

            Assert.Less(Vector3.Distance(-Vector3.right, result.Direction), 0.0001f);
            Assert.IsFalse(result.AllowSprint);
        }

        [Test]
        public void Tick_zeroY_returnsIdleDirection_turningInPlaceIsValid()
        {
            var result = this.strategy.Tick(this.playerTransform, new Vector2(1f, 0f), null, isAiming: false, deltaTime: 0.5f);

            Assert.AreEqual(Vector3.zero, result.Direction);
            // Rotation still happened even though there's no translation this frame.
            Assert.AreNotEqual(Vector3.forward, this.playerTransform.forward);
        }

        [Test]
        public void Tick_whileAiming_returnsIdle_andDoesNotRotate()
        {
            var result = this.strategy.Tick(this.playerTransform, new Vector2(1f, 1f), null, isAiming: true, deltaTime: 0.5f);

            Assert.AreEqual(Vector3.zero, result.Direction);
            Assert.AreEqual(Vector3.forward, this.playerTransform.forward);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Compile and confirm the console shows an error referencing the missing `ClassicPlayerMovementStrategy` type.

- [ ] **Step 3: Write `ClassicPlayerMovementStrategy.cs`**

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;

namespace CrimsonDraft.Navigation.Player.Movement
{
    // Resident Evil-style tank controls: rotate in place, walk/run only along the character's
    // own current facing. See [[Sistema de Movimiento]] "Esquema Classic -- Tank Controls".
    public sealed class ClassicPlayerMovementStrategy : IPlayerMovementStrategy
    {
        // Placeholders -- no feel pass yet, see Sistema de Movimiento "Pendiente".
        private const float TurnSpeedDegPerSec = 180f;
        private const float AxisThreshold      = 0.1f;

        public PlayerMovementResult Tick(Transform playerTransform, Vector2 rawInput, InputDevice? lastDevice, bool isAiming, float deltaTime)
        {
            if (isAiming) return PlayerMovementResult.Idle;

            if (Mathf.Abs(rawInput.x) > AxisThreshold)
                playerTransform.Rotate(Vector3.up, rawInput.x * TurnSpeedDegPerSec * deltaTime, Space.World);

            if (rawInput.y > AxisThreshold)
                return new PlayerMovementResult(playerTransform.forward, allowSprint: true);

            // Backpedal is always walk speed -- running backward was never possible in the
            // classic control scheme this is modeled on.
            if (rawInput.y < -AxisThreshold)
                return new PlayerMovementResult(-playerTransform.forward, allowSprint: false);

            return PlayerMovementResult.Idle;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run `ClassicPlayerMovementStrategyTests` via the MCP `run_tests` tool or Test Runner.
Expected: 7 tests, all PASS.

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Player/Movement/ClassicPlayerMovementStrategy.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/ClassicPlayerMovementStrategyTests.cs"
git commit -m "feat(navigation): add ClassicPlayerMovementStrategy (tank controls)"
```

---

### Task 4: `IControlSchemeService` / `ControlSchemeService`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Input/IControlSchemeService.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Input/ControlSchemeService.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/ControlSchemeServiceTests.cs`

**Interfaces:**
- Produces: `enum ControlScheme { Modern, Classic }`, `IControlSchemeService { ControlScheme CurrentScheme { get; } void SetScheme(ControlScheme scheme); }`, `ControlSchemeService : IControlSchemeService, VContainer.Unity.IInitializable` — Task 5 registers this, Task 6 and Task 7 consume the interface.

Mirrors `AudioSettingsService` (`Game/CrimsonDraft/Assets/Scripts/Infrastructure/Audio/AudioSettingsService.cs`): `PlayerPrefs`-backed, read in `IInitializable.Initialize()`, written in the setter.

- [ ] **Step 1: Write the failing tests**

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Tests
{
    public sealed class ControlSchemeServiceTests
    {
        private const string SchemeKey = "Control.Scheme";

        [SetUp]
        public void SetUp() => PlayerPrefs.DeleteKey(SchemeKey);

        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteKey(SchemeKey);

        private static ControlSchemeService BuildAndInit()
        {
            var service = new ControlSchemeService();
            ((IInitializable)service).Initialize();
            return service;
        }

        [Test]
        public void Initialize_noSavedValue_defaultsToModern()
        {
            var service = BuildAndInit();

            Assert.AreEqual(ControlScheme.Modern, service.CurrentScheme);
        }

        [Test]
        public void SetScheme_updatesCurrentScheme()
        {
            var service = BuildAndInit();

            service.SetScheme(ControlScheme.Classic);

            Assert.AreEqual(ControlScheme.Classic, service.CurrentScheme);
        }

        [Test]
        public void SetScheme_persistsAcrossNewInstances()
        {
            var first = BuildAndInit();
            first.SetScheme(ControlScheme.Classic);

            var second = BuildAndInit();

            Assert.AreEqual(ControlScheme.Classic, second.CurrentScheme);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Compile and confirm the console shows an error referencing the missing `ControlSchemeService`/`ControlScheme` types.

- [ ] **Step 3: Write `IControlSchemeService.cs`**

```csharp
#nullable enable

namespace CrimsonDraft.Infrastructure.Input
{
    public enum ControlScheme { Modern, Classic }

    public interface IControlSchemeService
    {
        ControlScheme CurrentScheme { get; }
        void SetScheme(ControlScheme scheme);
    }
}
```

- [ ] **Step 4: Write `ControlSchemeService.cs`**

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace CrimsonDraft.Infrastructure.Input
{
    public sealed class ControlSchemeService : IControlSchemeService, IInitializable
    {
        private const string       SchemeKey     = "Control.Scheme";
        private const ControlScheme DefaultScheme = ControlScheme.Modern;

        public ControlScheme CurrentScheme { get; private set; }

        [Preserve]
        public ControlSchemeService() { }

        void IInitializable.Initialize()
        {
            this.CurrentScheme = (ControlScheme)PlayerPrefs.GetInt(SchemeKey, (int)DefaultScheme);
        }

        public void SetScheme(ControlScheme scheme)
        {
            this.CurrentScheme = scheme;
            PlayerPrefs.SetInt(SchemeKey, (int)scheme);
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run `ControlSchemeServiceTests` via the MCP `run_tests` tool or Test Runner.
Expected: 3 tests, all PASS.

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/Input/IControlSchemeService.cs" "Game/CrimsonDraft/Assets/Scripts/Infrastructure/Input/ControlSchemeService.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/ControlSchemeServiceTests.cs"
git commit -m "feat(infrastructure): add persisted IControlSchemeService"
```

---

### Task 5: Register `ControlSchemeService`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `ControlSchemeService` (Task 4).
- Produces: `IControlSchemeService` resolvable via VContainer from `GameLifetimeScope` and every child scope (`NavigationScope`, `CombatScope`), used by Task 6 and Task 7.

`GameLifetimeScope.Configure` already registers `AudioSettingsService` and `GraphicsSettingsService` as `Lifetime.Singleton`, both `.AsImplementedInterfaces()`. Add `ControlSchemeService` the same way, right after `GraphicsSettingsService`. `CrimsonDraft.Infrastructure.Input` is already `using`'d in this file (for `InputService`), so no new using is needed.

- [ ] **Step 1: Modify `GameLifetimeScope.cs`**

Find:
```csharp
            builder.Register<GraphicsSettingsService>(Lifetime.Singleton).AsImplementedInterfaces();
```

Replace with:
```csharp
            builder.Register<GraphicsSettingsService>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.Register<ControlSchemeService>(Lifetime.Singleton).AsImplementedInterfaces();
```

- [ ] **Step 2: Compile and run the full EditMode suite as a regression baseline**

Compile, then run the full EditMode suite via the MCP `run_tests` tool (no `test_names` filter) or Test Runner.
Expected: no new compile errors; the same pre-existing failures as before this plan (3 in `CombatMenuControllerTests`, ~17 in `InventoryServiceTests` — both unrelated to this feature) and no others.

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs"
git commit -m "feat(infrastructure): register IControlSchemeService as a root singleton"
```

---

### Task 6: Wire `PlayerController` to the strategies

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerController.cs`

**Interfaces:**
- Consumes: `IPlayerMovementStrategy`, `ModernPlayerMovementStrategy`, `ClassicPlayerMovementStrategy` (Tasks 1-3), `IControlSchemeService`/`ControlScheme` (Task 4, resolvable per Task 5).

`PlayerController` has no existing unit tests (it's a `MonoBehaviour` with `Rigidbody`/`NavMesh`/`Animator` dependencies that nothing in this codebase currently fakes) — this task is a direct code change, verified by compilation + the existing test suite staying green + manual Play Mode verification (both control schemes are new/changed player-facing behavior that can only be felt by playing).

- [ ] **Step 1: Replace the whole file**

Replace `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerController.cs` with:

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Player.Movement;
using CrimsonDraft.Operators;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        private const int PlayerOperatorSlot = 0;

        [SerializeField] private Rigidbody rb       = null!;
        [SerializeField] private Animator  animator = null!;
        [SerializeField] private float walkSpeed         = 4f;
        [SerializeField] private float runSpeed          = 7f;
        [SerializeField] private float footOffset        = 1f;   // distancia del pivot del Rigidbody al suelo
        [SerializeField] private float navMeshTolerance  = 0.3f; // tolerancia horizontal para considerar "en NavMesh"

        [Header("Health Speed Steps (REmake-based)")]
        [SerializeField, Range(0f, 1f)] private float yellowCautionThreshold  = 0.75f;
        [SerializeField, Range(0f, 1f)] private float orangeCautionThreshold  = 0.50f;
        [SerializeField, Range(0f, 1f)] private float dangerThreshold         = 0.25f;
        [SerializeField, Range(0f, 1f)] private float yellowCautionSpeedRatio = 1.00f;
        [SerializeField, Range(0f, 1f)] private float orangeCautionSpeedRatio = 0.86f;
        [SerializeField, Range(0f, 1f)] private float dangerSpeedRatio        = 0.72f;

        private static readonly int ArmedHash = Animator.StringToHash("Armed");
        private static readonly int IdleHash  = Animator.StringToHash("Idle");
        private static readonly int WalkHash  = Animator.StringToHash("Walk");
        private static readonly int RunHash   = Animator.StringToHash("Run");

        private IInputService         inputService         = null!;
        private IInventoryService     inventoryService     = null!;
        private IControlSchemeService controlSchemeService = null!;
        private IPlayerMovementStrategy modernStrategy  = null!;
        private IPlayerMovementStrategy classicStrategy = null!;
        private IOperatorRoster?      roster;
        private InputDevice?          lastDevice;

        public bool IsAiming { get; private set; }

        // transform.position is the Rigidbody's pivot, not the ground — footOffset is the
        // vertical distance between them (see OnDrawGizmosSelected's "foot anchor" and
        // ResolveNavMeshDirection's sampleY). Anything that needs to place something at the
        // player's actual ground position (e.g. a dropped corpse) must use this, not
        // transform.position directly, or it ends up floating footOffset meters in the air.
        public Vector3 FootPosition => transform.position - new Vector3(0f, this.footOffset, 0f);

        [Inject]
        public void Construct(
            IInputService                  inputService,
            IInventoryService              inventoryService,
            ICameraRelativeMovementService cameraRelativeMovementService,
            IControlSchemeService          controlSchemeService,
            IOperatorRoster                roster)
        {
            this.inputService         = inputService;
            this.inventoryService     = inventoryService;
            this.controlSchemeService = controlSchemeService;
            this.roster               = roster;
            this.modernStrategy       = new ModernPlayerMovementStrategy(cameraRelativeMovementService);
            this.classicStrategy      = new ClassicPlayerMovementStrategy();
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
        }

        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            this.lastDevice = ctx.control.device;
        }

        private void FixedUpdate()
        {
            var isArmed = this.inventoryService.GetEquippedWeaponIndex(PlayerOperatorSlot) >= 0;
            this.animator.SetBool(ArmedHash, isArmed);

            var raw = this.inputService.Move.ReadValue<Vector2>();

            var strategy = this.controlSchemeService.CurrentScheme == ControlScheme.Classic
                ? this.classicStrategy
                : this.modernStrategy;

            // Always ticked (see IPlayerMovementStrategy) -- ModernPlayerMovementStrategy
            // depends on this running every frame, aiming or not.
            var result = strategy.Tick(transform, raw, this.lastDevice, this.IsAiming, Time.fixedDeltaTime);

            if (this.IsAiming)
            {
                this.rb.linearVelocity = Vector3.zero;
                return;
            }

            if (result.Direction == Vector3.zero)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetTrigger(IdleHash);
                return;
            }

            var isSprinting     = this.inputService.Sprint.IsPressed() && result.AllowSprint;
            var speedMultiplier = this.GetSpeedMultiplier();
            var speed           = (isSprinting ? this.runSpeed : this.walkSpeed) * speedMultiplier;

            this.animator.SetTrigger(isSprinting ? RunHash : WalkHash);

            var resolvedDir = ResolveNavMeshDirection(result.Direction, speed);
            if (resolvedDir == Vector3.zero)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetTrigger(IdleHash);
                return;
            }

            this.rb.linearVelocity = resolvedDir * speed;
        }

        private Vector3 ResolveNavMeshDirection(Vector3 moveDir, float speed)
        {
            float   step    = speed * Time.fixedDeltaTime;
            Vector3 origin  = this.rb.position;
            float   sampleY = origin.y - this.footOffset;

            Vector3 next = new Vector3(origin.x + moveDir.x * step, sampleY, origin.z + moveDir.z * step);
            if (NavMesh.SamplePosition(next, out _, this.navMeshTolerance, NavMesh.AllAreas))
                return moveDir;

            Vector3 xOnly = new Vector3(origin.x + moveDir.x * step, sampleY, origin.z);
            if (NavMesh.SamplePosition(xOnly, out _, this.navMeshTolerance, NavMesh.AllAreas))
                return new Vector3(moveDir.x, 0f, 0f).normalized;

            Vector3 zOnly = new Vector3(origin.x, sampleY, origin.z + moveDir.z * step);
            if (NavMesh.SamplePosition(zOnly, out _, this.navMeshTolerance, NavMesh.AllAreas))
                return new Vector3(0f, 0f, moveDir.z).normalized;

            return Vector3.zero;
        }

        private float GetSpeedMultiplier()
        {
            if (this.roster == null) return 1f;

            float lowestHpRatio = 1f;
            for (int i = 0; i < this.roster.Count; i++)
            {
                OperatorRuntime op = this.roster[i];
                if (!op.IsPresent || !op.IsAlive) continue;
                if (op.HpRatio < lowestHpRatio)
                    lowestHpRatio = op.HpRatio;
            }

            if (lowestHpRatio <= this.dangerThreshold)        return this.dangerSpeedRatio;
            if (lowestHpRatio <= this.orangeCautionThreshold) return this.orangeCautionSpeedRatio;
            if (lowestHpRatio <= this.yellowCautionThreshold) return this.yellowCautionSpeedRatio;
            return 1f; // Fine
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 foot = transform.position - new Vector3(0f, this.footOffset, 0f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(foot, 0.08f);

            bool onNavMesh = NavMesh.SamplePosition(foot, out _, this.navMeshTolerance, NavMesh.AllAreas);
            Gizmos.color = onNavMesh ? new Color(0f, 1f, 0f, 0.25f) : new Color(1f, 0f, 0f, 0.25f);
            Gizmos.DrawSphere(foot, this.navMeshTolerance);
        }
    }
}
```

Note what left the file: `Quantize8Way` moved into `ModernPlayerMovementStrategy` (Task 2); the `using UnityEngine.InputSystem.Controls;` line is dropped (it was unused in the original file too — `Gamepad`/`InputDevice` both live directly in `UnityEngine.InputSystem`).

- [ ] **Step 2: Compile and run the full EditMode suite**

Compile, then run the full EditMode suite.
Expected: no new compile errors; same baseline failures as Task 5's Step 2, no others.

- [ ] **Step 3: Manual Play Mode verification**

Enter Play Mode in a Navigation scene (e.g. `Deck_B_Development`). With the default scheme (Modern):
- Confirm movement is still camera-relative and facing snaps to the movement direction, matching pre-plan behavior.
- Confirm aiming still zeroes velocity and the character doesn't rotate while aiming.

There is no way yet to switch to Classic in-game (Task 7 wires that) — this step only confirms Modern has zero regressions after the refactor.

- [ ] **Step 4: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerController.cs"
git commit -m "refactor(navigation): delegate PlayerController movement to IPlayerMovementStrategy"
```

---

### Task 7: Wire the "Control" knob in Settings

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/UI/MainMenu/GeneralMenuController.cs`

**Interfaces:**
- Consumes: `IControlSchemeService`/`ControlScheme` (Task 4, resolvable per Task 5).

`GeneralMenuController.Adjust(int index, int direction)` currently no-ops for anything but `GammaIndex`. `ControlIndex` (2) becomes a real two-state toggle: either direction flips it (there are only two values, so the sign of `direction` doesn't matter). `LanguageIndex` (0) stays locked — out of scope for this feature.

- [ ] **Step 1: Modify `GeneralMenuController.cs`**

Find the class doc-comment:
```csharp
    /// <summary>
    /// General tab content: Language and Control are physical knobs too, same as Sound, but
    /// locked for now (Adjust is a no-op -- the knob/outline exist, rotating just isn't wired up
    /// yet). Gamma is a real 0-100 value: rotates its knob exactly like a volume knob and also
    /// keeps the canvas fill bar in sync. Selection is shown purely via each knob's outline
    /// (never in the flat canvas), matching Sound.
    /// </summary>
```

Replace with:
```csharp
    /// <summary>
    /// General tab content: Language is a physical knob too, same as Sound, but locked for now
    /// (Adjust is a no-op for it -- the knob/outline exist, rotating just isn't wired up yet).
    /// Control is a real two-state toggle (Modern/Classic, see IControlSchemeService) -- either
    /// direction flips it, there's nothing to clamp with only two values. Gamma is a real 0-100
    /// value: rotates its knob exactly like a volume knob and also keeps the canvas fill bar in
    /// sync. Selection is shown purely via each knob's outline (never in the flat canvas),
    /// matching Sound.
    /// </summary>
```

Find:
```csharp
using CrimsonDraft.Infrastructure.Graphics;
using UnityEngine;
using VContainer;
```

Replace with:
```csharp
using CrimsonDraft.Infrastructure.Graphics;
using CrimsonDraft.Infrastructure.Input;
using UnityEngine;
using VContainer;
```

Find:
```csharp
        private GameObject[] outlines = null!;
        private int          gammaValue;
        private IGraphicsSettingsService graphicsSettingsService = null!;

        public int ChannelCount => 3;

        [Inject]
        public void Construct(IGraphicsSettingsService graphicsSettingsService)
        {
            this.graphicsSettingsService = graphicsSettingsService;
        }
```

Replace with:
```csharp
        private GameObject[] outlines = null!;
        private int          gammaValue;
        private IGraphicsSettingsService graphicsSettingsService = null!;
        private IControlSchemeService    controlSchemeService    = null!;

        public int ChannelCount => 3;

        [Inject]
        public void Construct(IGraphicsSettingsService graphicsSettingsService, IControlSchemeService controlSchemeService)
        {
            this.graphicsSettingsService = graphicsSettingsService;
            this.controlSchemeService    = controlSchemeService;
        }
```

Find:
```csharp
        public void Adjust(int index, int direction)
        {
            if (index != GammaIndex) return; // Language and Control are locked for now.

            this.gammaValue = Mathf.Clamp(this.gammaValue + direction * this.stepPercent, 0, 100);
            ApplyGamma();
            this.graphicsSettingsService.SetGamma(this.gammaValue / 100f);
        }
```

Replace with:
```csharp
        public void Adjust(int index, int direction)
        {
            if (index == ControlIndex)
            {
                var next = this.controlSchemeService.CurrentScheme == ControlScheme.Modern
                    ? ControlScheme.Classic
                    : ControlScheme.Modern;
                this.controlSchemeService.SetScheme(next);
                return;
            }

            if (index != GammaIndex) return; // Language is locked for now.

            this.gammaValue = Mathf.Clamp(this.gammaValue + direction * this.stepPercent, 0, 100);
            ApplyGamma();
            this.graphicsSettingsService.SetGamma(this.gammaValue / 100f);
        }
```

- [ ] **Step 2: Compile and run the full EditMode suite**

Compile, then run the full EditMode suite.
Expected: no new compile errors; same baseline failures as Task 5's Step 2, no others.

- [ ] **Step 3: Manual verification**

Enter Play Mode, open Settings → General, navigate to the "Control" knob, and press left/right. Confirm:
- The outline highlight still shows/hides correctly when navigating onto/off the Control slot (unchanged — `ShowOutline`/`HideOutlines` are index-generic and untouched by this task).
- Pressing left or right toggles the scheme (verify indirectly: after toggling, the character in a Navigation scene should move via Classic tank controls — rotate in place on horizontal input, walk forward/back on vertical input, backpedal never sprints).
- No visual knob rotation is expected yet (see the design spec's caveat — `GeneralMenuController`'s `control` field has no `knob` Transform serialized, only `outline`; adding physical rotation is out of scope for this plan).

- [ ] **Step 4: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/UI/MainMenu/GeneralMenuController.cs"
git commit -m "feat(ui): wire the Settings Control knob to IControlSchemeService"
```

---

### Task 8: Full regression pass

**Files:** none (verification only).

- [ ] **Step 1: Full recompile**

Request a full script compile and confirm zero errors in the console.

- [ ] **Step 2: Run the full EditMode suite**

Run every EditMode test (no filter) via the MCP `run_tests` tool or Test Runner.
Expected: total test count increased by 18 (8 `ModernPlayerMovementStrategyTests` + 7 `ClassicPlayerMovementStrategyTests` + 3 `ControlSchemeServiceTests`) over the pre-plan baseline; all 18 new tests PASS; the same pre-existing, unrelated failures remain (3 in `CombatMenuControllerTests`, the `InventoryServiceTests` group) and no new ones appear.

- [ ] **Step 3: Update the GDD "Pendiente" checklist**

In `Design/GDD/Sistema de Movimiento.md`, check off the now-completed item:

Find:
```markdown
- [ ] Implementar `ClassicPlayerMovementStrategy` (Tank Controls) — ver spec [[2026-09-01-player-movement-control-scheme-design|Player Movement Control Scheme]]
- [ ] Cablear la perilla "Control" del menú de Settings a la persistencia real
```

Replace with:
```markdown
- [x] Implementar `ClassicPlayerMovementStrategy` (Tank Controls) — ver spec [[2026-09-01-player-movement-control-scheme-design|Player Movement Control Scheme]]
- [x] Cablear la perilla "Control" del menú de Settings a la persistencia real
```

Also update the frontmatter `ultima-revision` date to the date this task is actually completed.

- [ ] **Step 4: Commit**

```bash
git add "Design/GDD/Sistema de Movimiento.md"
git commit -m "docs(gdd): mark Modern/Classic control scheme as implemented"
```
