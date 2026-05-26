# ATB Combat System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current real-time enemy attack scheduler with a unified ATB (Active Time Battle) system where all actors share a single gauge model, all commands flow through a global FIFO queue, and timers pause in deep submenus (Chrono Trigger-style Wait mode).

**Architecture:** `ATBSystem` (pure C#) manages all actor gauges; `CombatActionQueue` (pure C#) serializes all actions FIFO; `CombatOrchestrator` (MonoBehaviour, VContainer `IInitializable`) drives the central loop per frame — replacing both `EnemyAttackScheduler` and `EnemyAttackController`. `CombatMenuController` injects `ICombatOrchestrator` for enqueue/wait-mode calls; `CombatOrchestrator` fires `ShootConfigurationRequestedEvent` via MessagePipe when Shoot reaches the queue head, avoiding circular DI.

**Tech Stack:** Unity 2022+, C# 10 nullable, VContainer, MessagePipe, NUnit (Edit Mode)

**GDD:** [`Design/GDD/Sistema ATB de Combate.md`](../../../Design/GDD/Sistema%20ATB%20de%20Combate.md)

---

## File Map

| Action | Path |
|---|---|
| **Create** | `Assets/Scripts/Combat/ATBActorState.cs` |
| **Create** | `Assets/Scripts/Combat/ATBSystem.cs` |
| **Create** | `Assets/Scripts/Combat/PendingAction.cs` |
| **Create** | `Assets/Scripts/Combat/CombatActionQueue.cs` |
| **Create** | `Assets/Scripts/Combat/ICombatOrchestrator.cs` |
| **Create** | `Assets/Scripts/Combat/ShootConfigurationRequestedEvent.cs` |
| **Create** | `Assets/Scripts/Combat/CombatOrchestrator.cs` |
| **Create** | `Assets/Scripts/Combat/UI/CombatDebugView.cs` |
| **Create** | `Assets/Tests/EditMode/ATBSystemTests.cs` |
| **Create** | `Assets/Tests/EditMode/CombatActionQueueTests.cs` |
| **Modify** | `Assets/Scripts/Operators/OperatorData.cs` |
| **Modify** | `Assets/Scripts/Infrastructure/GameLifetimeScope.cs` |
| **Modify** | `Assets/Scripts/Combat/CombatScope.cs` |
| **Modify** | `Assets/Scripts/Combat/UI/CombatMenuController.cs` |
| **Modify** | `Assets/Scripts/Combat/States/OperatorSelectionState.cs` |
| **Modify** | `Assets/Scripts/Combat/States/CommandPanelState.cs` |
| **Modify** | `Assets/Scripts/Combat/States/SubPanelState.cs` |
| **Modify** | `Assets/Scripts/Combat/States/ShotCountSelectionState.cs` |
| **Modify** | `Assets/Scripts/Combat/States/TargetSelectionState.cs` |
| **Modify** | `Assets/Scripts/Combat/States/AimingState.cs` |
| **Delete** | `Assets/Scripts/Combat/EnemyAttackScheduler.cs` |
| **Delete** | `Assets/Scripts/Combat/EnemyAttackController.cs` |
| **Delete** | `Assets/Tests/EditMode/EnemyAttackSchedulerTests.cs` |
| **Unity** | Add `CombatOrchestrator` MonoBehaviour to Combat.unity |
| **Unity** | Add `CombatDebugView` to Combat.unity (debug canvas) |

All paths relative to `Game/CrimsonDraft/`.

---

## Task 1: ATBActorState + ATBSystem

**Files:**
- Create: `Assets/Scripts/Combat/ATBActorState.cs`
- Create: `Assets/Scripts/Combat/ATBSystem.cs`
- Create: `Assets/Tests/EditMode/ATBSystemTests.cs`
- Delete: `Assets/Tests/EditMode/EnemyAttackSchedulerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/ATBSystemTests.cs
using NUnit.Framework;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class ATBSystemTests
    {
        private static ATBActorConfig Op(int slot, float gps) =>
            new ATBActorConfig(slot, ATBActorKind.Operator, gps);

        private static ATBActorConfig En(int slot, float gps) =>
            new ATBActorConfig(slot, ATBActorKind.Enemy, gps);

        [Test]
        public void Tick_advancesGaugeOfLiveActors()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { Op(0, 0.5f) });
            sys.Tick(1f, paused: false);
            Assert.AreEqual(0.5f, sys.GetActor(0, ATBActorKind.Operator)!.Gauge, 0.0001f);
        }

        [Test]
        public void Tick_clampsGaugeAtOne()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { Op(0, 1f) });
            sys.Tick(2f, paused: false);
            Assert.AreEqual(1f, sys.GetActor(0, ATBActorKind.Operator)!.Gauge, 0.0001f);
        }

        [Test]
        public void Tick_whenPaused_doesNotAdvanceGauge()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { Op(0, 1f) });
            sys.Tick(0.5f, paused: true);
            Assert.AreEqual(0f, sys.GetActor(0, ATBActorKind.Operator)!.Gauge, 0.0001f);
        }

        [Test]
        public void IsReady_trueWhenGaugeReachesOne()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { Op(0, 1f) });
            sys.Tick(1f, paused: false);
            Assert.IsTrue(sys.GetActor(0, ATBActorKind.Operator)!.IsReady);
        }

        [Test]
        public void ResetActor_setsGaugeToZero()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { Op(0, 1f) });
            sys.Tick(1f, paused: false);
            sys.ResetActor(0, ATBActorKind.Operator);
            Assert.AreEqual(0f, sys.GetActor(0, ATBActorKind.Operator)!.Gauge, 0.0001f);
        }

        [Test]
        public void MarkDead_preventsGaugeAdvance()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { En(0, 1f) });
            sys.MarkDead(0, ATBActorKind.Enemy);
            sys.Tick(1f, paused: false);
            Assert.AreEqual(0f, sys.GetActor(0, ATBActorKind.Enemy)!.Gauge, 0.0001f);
        }

        [Test]
        public void GetActor_returnsNullForUnknownSlot()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { Op(0, 1f) });
            Assert.IsNull(sys.GetActor(99, ATBActorKind.Operator));
        }

        [Test]
        public void UpdateActorGaugeRate_changesTickBehavior()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { En(0, 0.5f) });
            sys.UpdateActorGaugeRate(0, ATBActorKind.Enemy, 1f);
            sys.Tick(1f, paused: false);
            Assert.AreEqual(1f, sys.GetActor(0, ATBActorKind.Enemy)!.Gauge, 0.0001f);
        }

        [Test]
        public void Tick_doesNotAdvanceDeadActors()
        {
            var sys = new ATBSystem();
            sys.Initialize(new[] { Op(0, 1f), En(0, 1f) });
            sys.MarkDead(0, ATBActorKind.Operator);
            sys.Tick(1f, paused: false);
            Assert.AreEqual(0f, sys.GetActor(0, ATBActorKind.Operator)!.Gauge, 0.0001f);
            Assert.AreEqual(1f, sys.GetActor(0, ATBActorKind.Enemy)!.Gauge, 0.0001f);
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compile error (types missing)**

Open Unity → Window → General → Test Runner → EditMode → Run All
Expected: compile errors — `ATBSystem`, `ATBActorKind`, `ATBActorConfig` do not exist yet.

- [ ] **Step 3: Create ATBActorState.cs**

```csharp
// Assets/Scripts/Combat/ATBActorState.cs
#nullable enable

namespace CrimsonDraft.Combat
{
    public enum ATBActorKind { Operator, Enemy }

    public readonly struct ATBActorConfig
    {
        public int         SlotIndex      { get; }
        public ATBActorKind Kind           { get; }
        public float       GaugePerSecond { get; }

        public ATBActorConfig(int slotIndex, ATBActorKind kind, float gaugePerSecond)
        {
            this.SlotIndex      = slotIndex;
            this.Kind           = kind;
            this.GaugePerSecond = gaugePerSecond > 0f ? gaugePerSecond : 0f;
        }
    }

    public sealed class ATBActorState
    {
        public ATBActorConfig Config { get; }

        public float Gauge             { get; private set; }
        public bool  IsReady           => this.Gauge >= 1f;
        public bool  IsAwaitingCommand { get; set; }
        public bool  IsDead            { get; private set; }

        private float gaugePerSecond;

        public ATBActorState(ATBActorConfig config)
        {
            this.Config          = config;
            this.gaugePerSecond  = config.GaugePerSecond;
        }

        public void Tick(float deltaTime)
        {
            if (this.IsDead) return;
            this.Gauge = (float)System.Math.Min(1.0, this.Gauge + deltaTime * this.gaugePerSecond);
        }

        public void Reset()
        {
            this.Gauge             = 0f;
            this.IsAwaitingCommand = false;
        }

        public void MarkDead() => this.IsDead = true;

        public void UpdateGaugePerSecond(float newRate)
        {
            this.gaugePerSecond = newRate > 0f ? newRate : 0f;
        }
    }
}
```

- [ ] **Step 4: Create ATBSystem.cs**

```csharp
// Assets/Scripts/Combat/ATBSystem.cs
#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Combat
{
    public sealed class ATBSystem
    {
        private readonly List<ATBActorState> actors = new();

        public IReadOnlyList<ATBActorState> Actors => this.actors;

        public void Initialize(IReadOnlyList<ATBActorConfig> configs)
        {
            this.actors.Clear();
            for (int i = 0; i < configs.Count; i++)
                this.actors.Add(new ATBActorState(configs[i]));
        }

        public void Tick(float deltaTime, bool paused)
        {
            if (paused) return;
            for (int i = 0; i < this.actors.Count; i++)
                this.actors[i].Tick(deltaTime);
        }

        public void ResetActor(int slotIndex, ATBActorKind kind)
            => GetActor(slotIndex, kind)?.Reset();

        public void MarkDead(int slotIndex, ATBActorKind kind)
            => GetActor(slotIndex, kind)?.MarkDead();

        public void UpdateActorGaugeRate(int slotIndex, ATBActorKind kind, float newGaugePerSecond)
            => GetActor(slotIndex, kind)?.UpdateGaugePerSecond(newGaugePerSecond);

        public ATBActorState? GetActor(int slotIndex, ATBActorKind kind)
        {
            for (int i = 0; i < this.actors.Count; i++)
            {
                ATBActorState a = this.actors[i];
                if (a.Config.SlotIndex == slotIndex && a.Config.Kind == kind)
                    return a;
            }
            return null;
        }
    }
}
```

- [ ] **Step 5: Run tests — expect all 9 pass**

Run: Test Runner → EditMode → Run All
Expected: 9 tests PASS, 0 failures.

- [ ] **Step 6: Delete EnemyAttackSchedulerTests.cs**

Delete: `Assets/Tests/EditMode/EnemyAttackSchedulerTests.cs`
Verify: Test Runner shows no test for `EnemyAttackSchedulerTests`.

- [ ] **Step 7: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/ATBActorState.cs Game/CrimsonDraft/Assets/Scripts/Combat/ATBSystem.cs Game/CrimsonDraft/Assets/Tests/EditMode/ATBSystemTests.cs
git rm Game/CrimsonDraft/Assets/Tests/EditMode/EnemyAttackSchedulerTests.cs
git commit -m "feat(combat): add ATBActorState, ATBSystem and tests; remove EnemyAttackSchedulerTests"
```

---

## Task 2: PendingAction + CombatActionQueue

**Files:**
- Create: `Assets/Scripts/Combat/PendingAction.cs`
- Create: `Assets/Scripts/Combat/CombatActionQueue.cs`
- Create: `Assets/Tests/EditMode/CombatActionQueueTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/CombatActionQueueTests.cs
using NUnit.Framework;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class CombatActionQueueTests
    {
        [Test]
        public void HasPending_falseWhenEmpty()
        {
            var queue = new CombatActionQueue();
            Assert.IsFalse(queue.HasPending);
        }

        [Test]
        public void Enqueue_increasesCount()
        {
            var queue = new CombatActionQueue();
            queue.Enqueue(PendingAction.Defend(0));
            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void Peek_doesNotRemove()
        {
            var queue = new CombatActionQueue();
            queue.Enqueue(PendingAction.Defend(0));
            _ = queue.Peek();
            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void Dequeue_removesFromFront()
        {
            var queue = new CombatActionQueue();
            queue.Enqueue(PendingAction.Defend(0));
            queue.Enqueue(PendingAction.EnemyAttack(0, 1, 10));
            PendingAction first = queue.Dequeue();
            Assert.AreEqual(PendingActionType.Defend, first.Type);
            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void Dequeue_preservesFifoOrder()
        {
            var queue = new CombatActionQueue();
            queue.Enqueue(PendingAction.Shoot(0));
            queue.Enqueue(PendingAction.Reload(1, 2));
            queue.Enqueue(PendingAction.EnemyAttack(0, 0, 15));
            Assert.AreEqual(PendingActionType.Shoot,       queue.Dequeue().Type);
            Assert.AreEqual(PendingActionType.Reload,      queue.Dequeue().Type);
            Assert.AreEqual(PendingActionType.EnemyAttack, queue.Dequeue().Type);
        }

        [Test]
        public void Clear_emptiesQueue()
        {
            var queue = new CombatActionQueue();
            queue.Enqueue(PendingAction.Defend(0));
            queue.Enqueue(PendingAction.Shoot(1));
            queue.Clear();
            Assert.AreEqual(0, queue.Count);
            Assert.IsFalse(queue.HasPending);
        }

        [Test]
        public void PendingAction_Reload_storesPayload()
        {
            var action = PendingAction.Reload(operatorSlot: 2, ammoBoxIndex: 5);
            Assert.AreEqual(PendingActionType.Reload, action.Type);
            Assert.AreEqual(2, action.SlotIndex);
            Assert.AreEqual(5, action.AmmoBoxIndex);
        }

        [Test]
        public void PendingAction_EnemyAttack_storesPayload()
        {
            var action = PendingAction.EnemyAttack(enemySlot: 1, targetOperatorSlot: 0, damage: 25);
            Assert.AreEqual(PendingActionType.EnemyAttack, action.Type);
            Assert.AreEqual(1,  action.SlotIndex);
            Assert.AreEqual(0,  action.TargetOperatorSlot);
            Assert.AreEqual(25, action.Damage);
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compile error**

Expected: compile errors — `CombatActionQueue`, `PendingAction`, `PendingActionType` don't exist.

- [ ] **Step 3: Create PendingAction.cs**

```csharp
// Assets/Scripts/Combat/PendingAction.cs
#nullable enable

namespace CrimsonDraft.Combat
{
    public enum PendingActionType { Shoot, Reload, UseItem, Defend, EnemyAttack }

    public readonly struct PendingAction
    {
        public PendingActionType Type               { get; }
        public int               SlotIndex          { get; }
        public int               AmmoBoxIndex       { get; }
        public int               ItemIndex          { get; }
        public int               TargetOperatorSlot { get; }
        public int               Damage             { get; }

        private PendingAction(
            PendingActionType type,
            int slotIndex,
            int ammoBoxIndex       = -1,
            int itemIndex          = -1,
            int targetOperatorSlot = -1,
            int damage             = 0)
        {
            this.Type               = type;
            this.SlotIndex          = slotIndex;
            this.AmmoBoxIndex       = ammoBoxIndex;
            this.ItemIndex          = itemIndex;
            this.TargetOperatorSlot = targetOperatorSlot;
            this.Damage             = damage;
        }

        public static PendingAction Shoot(int operatorSlot) =>
            new PendingAction(PendingActionType.Shoot, operatorSlot);

        public static PendingAction Reload(int operatorSlot, int ammoBoxIndex) =>
            new PendingAction(PendingActionType.Reload, operatorSlot, ammoBoxIndex: ammoBoxIndex);

        public static PendingAction UseItem(int operatorSlot, int itemIndex) =>
            new PendingAction(PendingActionType.UseItem, operatorSlot, itemIndex: itemIndex);

        public static PendingAction Defend(int operatorSlot) =>
            new PendingAction(PendingActionType.Defend, operatorSlot);

        public static PendingAction EnemyAttack(int enemySlot, int targetOperatorSlot, int damage) =>
            new PendingAction(PendingActionType.EnemyAttack, enemySlot,
                targetOperatorSlot: targetOperatorSlot, damage: damage);
    }
}
```

- [ ] **Step 4: Create CombatActionQueue.cs**

```csharp
// Assets/Scripts/Combat/CombatActionQueue.cs
#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Combat
{
    public sealed class CombatActionQueue
    {
        private readonly Queue<PendingAction> queue = new();

        public int  Count      => this.queue.Count;
        public bool HasPending => this.queue.Count > 0;

        public void          Enqueue(PendingAction action) => this.queue.Enqueue(action);
        public PendingAction Peek()                        => this.queue.Peek();
        public PendingAction Dequeue()                     => this.queue.Dequeue();
        public void          Clear()                       => this.queue.Clear();
        public PendingAction[] ToArray()                   => this.queue.ToArray();
    }
}
```

- [ ] **Step 5: Run tests — all 8 pass**

Run: Test Runner → EditMode → Run All
Expected: 8 new tests PASS.

- [ ] **Step 6: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/PendingAction.cs Game/CrimsonDraft/Assets/Scripts/Combat/CombatActionQueue.cs Game/CrimsonDraft/Assets/Tests/EditMode/CombatActionQueueTests.cs
git commit -m "feat(combat): add PendingAction, CombatActionQueue and queue tests"
```

---

## Task 3: Speed stat + ICombatOrchestrator + ShootConfigurationRequestedEvent

**Files:**
- Modify: `Assets/Scripts/Operators/OperatorData.cs`
- Create: `Assets/Scripts/Combat/ShootConfigurationRequestedEvent.cs`
- Create: `Assets/Scripts/Combat/ICombatOrchestrator.cs`

- [ ] **Step 1: Add Speed field to OperatorData.cs**

Find the block of `[SerializeField]` fields in `Assets/Scripts/Operators/OperatorData.cs`. After the last existing field, add:

```csharp
[SerializeField, Range(1, 99)] private int speed = 50;

public int Speed => this.speed;
```

- [ ] **Step 2: Create ShootConfigurationRequestedEvent.cs**

```csharp
// Assets/Scripts/Combat/ShootConfigurationRequestedEvent.cs
#nullable enable

namespace CrimsonDraft.Combat
{
    public readonly struct ShootConfigurationRequestedEvent
    {
        public int OperatorSlot { get; }

        public ShootConfigurationRequestedEvent(int operatorSlot)
        {
            this.OperatorSlot = operatorSlot;
        }
    }
}
```

- [ ] **Step 3: Create ICombatOrchestrator.cs**

```csharp
// Assets/Scripts/Combat/ICombatOrchestrator.cs
#nullable enable

namespace CrimsonDraft.Combat
{
    public interface ICombatOrchestrator
    {
        void EnqueueAction(PendingAction action);
        void SetWaitMode(bool paused);
        bool IsOperatorReady(int slotIndex);
        void NotifyShootCompleted();
    }
}
```

- [ ] **Step 4: Open Unity, verify no compile errors**

Expected: Console shows no errors. `OperatorData` inspector now exposes `Speed` slider (1–99) on operator assets.

- [ ] **Step 5: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Operators/OperatorData.cs Game/CrimsonDraft/Assets/Scripts/Combat/ShootConfigurationRequestedEvent.cs Game/CrimsonDraft/Assets/Scripts/Combat/ICombatOrchestrator.cs
git commit -m "feat(combat): add Speed to OperatorData, ICombatOrchestrator, ShootConfigurationRequestedEvent"
```

---

## Task 4: CombatOrchestrator

**Files:**
- Create: `Assets/Scripts/Combat/CombatOrchestrator.cs`
- Delete: `Assets/Scripts/Combat/EnemyAttackScheduler.cs`
- Delete: `Assets/Scripts/Combat/EnemyAttackController.cs`
- Modify: `Assets/Scripts/Combat/CombatScope.cs` (comment out deleted registration)

- [ ] **Step 1: Create CombatOrchestrator.cs**

```csharp
// Assets/Scripts/Combat/CombatOrchestrator.cs
#nullable enable

using System.Collections.Generic;
using MessagePipe;
using UnityEngine;
using UnityEngine.Scripting;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat
{
    public sealed class CombatOrchestrator : MonoBehaviour, ICombatOrchestrator, IInitializable
    {
        private ATBSystem                                    atbSystem          = null!;
        private CombatActionQueue                            actionQueue        = null!;
        private IPublisher<ShootConfigurationRequestedEvent> shootPublisher     = null!;
        private IPublisher<CombatEndedEvent>                 combatEndPublisher = null!;
        private IBattlefieldView                             battlefieldView    = null!;
        private IOperatorRoster                              roster             = null!;
        private IEncounterContext                            encounterContext    = null!;
        private EncounterDatabase                            encounterDatabase   = null!;
        private IInventoryService                            inventory          = null!;
        private ICombatActionMenuView                        menuView           = null!;

        private readonly IRandomSource  random               = new UnityRandomSource();
        private readonly HashSet<int>   knownAliveEnemySlots = new();

        private float          animationLockUntil;
        private bool           shootConfigurationInProgress;
        private bool           waitModeActive;
        private bool           initialized;
        private EncounterData? encounter;
        private IOperatorEcgFeedback? ecgFeedback;

        [Inject]
        [Preserve]
        public void Construct(
            ATBSystem                                    atbSystem,
            CombatActionQueue                            actionQueue,
            IPublisher<ShootConfigurationRequestedEvent> shootPublisher,
            IPublisher<CombatEndedEvent>                 combatEndPublisher,
            IBattlefieldView                             battlefieldView,
            IOperatorRoster                              roster,
            IEncounterContext                            encounterContext,
            EncounterDatabase                            encounterDatabase,
            IInventoryService                            inventory,
            ICombatActionMenuView                        menuView)
        {
            this.atbSystem          = atbSystem;
            this.actionQueue        = actionQueue;
            this.shootPublisher     = shootPublisher;
            this.combatEndPublisher = combatEndPublisher;
            this.battlefieldView    = battlefieldView;
            this.roster             = roster;
            this.encounterContext   = encounterContext;
            this.encounterDatabase  = encounterDatabase;
            this.inventory          = inventory;
            this.menuView           = menuView;
        }

        void IInitializable.Initialize()
        {
            string? encounterId = this.encounterContext.CurrentEncounterId;
            if (encounterId == null) return;

            this.encounter = this.encounterDatabase.GetById(encounterId);
            if (this.encounter == null) return;

            var configs = BuildATBConfigs(this.encounter, this.roster);
            this.atbSystem.Initialize(configs);

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

        private void Update()
        {
            if (!this.initialized) return;

            SyncDeadEnemies();
            this.atbSystem.Tick(Time.deltaTime, this.waitModeActive);
            NotifyReadyOperators();
            EnqueueReadyEnemyAttacks();
            ProcessQueueHead();
        }

        private void LateUpdate()
        {
            if (!this.initialized) return;
            SyncAllEcgStates();
        }

        // ICombatOrchestrator

        public void EnqueueAction(PendingAction action)
        {
            this.actionQueue.Enqueue(action);
            this.atbSystem.ResetActor(action.SlotIndex, ATBActorKind.Operator);
        }

        public void SetWaitMode(bool paused) => this.waitModeActive = paused;

        public bool IsOperatorReady(int slotIndex)
        {
            ATBActorState? actor = this.atbSystem.GetActor(slotIndex, ATBActorKind.Operator);
            return actor != null && actor.IsReady && actor.IsAwaitingCommand;
        }

        public void NotifyShootCompleted()
        {
            if (!this.actionQueue.HasPending) return;
            if (this.actionQueue.Peek().Type != PendingActionType.Shoot) return;
            this.actionQueue.Dequeue();
            this.shootConfigurationInProgress = false;
            this.animationLockUntil = Time.time + 0.5f;
        }

        // Internal loop

        private void NotifyReadyOperators()
        {
            for (int i = 0; i < this.roster.Count; i++)
            {
                ATBActorState? actor = this.atbSystem.GetActor(i, ATBActorKind.Operator);
                if (actor == null || actor.IsDead || actor.IsAwaitingCommand) continue;
                if (actor.IsReady)
                    actor.IsAwaitingCommand = true;
            }
        }

        private void EnqueueReadyEnemyAttacks()
        {
            if (this.encounter == null) return;
            IReadOnlyList<int> aliveOperatorSlots = this.roster.GetAliveSlots();
            if (aliveOperatorSlots.Count == 0) return;

            for (int i = 0; i < this.encounter.EnemySlots.Length; i++)
            {
                EnemyData? data = this.encounter.EnemySlots[i];
                if (data == null) continue;

                ATBActorState? actor = this.atbSystem.GetActor(i, ATBActorKind.Enemy);
                if (actor == null || actor.IsDead || !actor.IsReady) continue;

                int targetIndex = this.random.NextInt(0, aliveOperatorSlots.Count);
                int targetSlot  = aliveOperatorSlots[targetIndex];

                this.actionQueue.Enqueue(PendingAction.EnemyAttack(i, targetSlot, data.AttackDamage));

                float jitter  = Mathf.Lerp(-data.AttackJitterSec, data.AttackJitterSec, this.random.NextFloat01());
                float nextSec = Mathf.Max(0.1f, data.AttackBaseSec + jitter);
                this.atbSystem.ResetActor(i, ATBActorKind.Enemy);
                this.atbSystem.UpdateActorGaugeRate(i, ATBActorKind.Enemy, 1f / nextSec);
            }
        }

        private void ProcessQueueHead()
        {
            if (Time.time < this.animationLockUntil) return;
            if (!this.actionQueue.HasPending) return;

            PendingAction head = this.actionQueue.Peek();

            if (head.Type == PendingActionType.Shoot)
            {
                if (!this.shootConfigurationInProgress)
                {
                    this.shootConfigurationInProgress = true;
                    this.shootPublisher.Publish(new ShootConfigurationRequestedEvent(head.SlotIndex));
                }
                return;
            }

            this.actionQueue.Dequeue();

            switch (head.Type)
            {
                case PendingActionType.Reload:
                    this.inventory.ReloadOperator(head.AmmoBoxIndex, head.SlotIndex);
                    var weapon = this.roster.Count > head.SlotIndex ? this.roster[head.SlotIndex].EquippedWeapon : null;
                    this.menuView.SetOperatorAmmo(head.SlotIndex, weapon?.CurrentAmmo ?? 0, weapon?.MaxAmmo ?? 0);
                    this.animationLockUntil = Time.time + 0.5f;
                    break;

                case PendingActionType.UseItem:
                    this.animationLockUntil = Time.time + 0.5f;
                    break;

                case PendingActionType.Defend:
                    this.animationLockUntil = Time.time + 0.3f;
                    break;

                case PendingActionType.EnemyAttack:
                    ApplyEnemyAttack(head);
                    break;
            }
        }

        private void ApplyEnemyAttack(PendingAction action)
        {
            this.roster[action.TargetOperatorSlot].ApplyDamage(action.Damage);
            this.battlefieldView.PlayEnemyAttackFeedback(action.SlotIndex);
            this.battlefieldView.ShowOperatorDamage(action.TargetOperatorSlot, action.Damage);
            this.ecgFeedback?.FlashOperatorDamage(action.TargetOperatorSlot);
            if (this.roster.Count > action.TargetOperatorSlot)
            {
                this.ecgFeedback?.SetOperatorHealthState(
                    action.TargetOperatorSlot,
                    this.roster[action.TargetOperatorSlot].HpRatio,
                    this.roster[action.TargetOperatorSlot].IsAlive);
            }

            EnemyData? data = (this.encounter != null && action.SlotIndex < this.encounter.EnemySlots.Length)
                ? this.encounter.EnemySlots[action.SlotIndex]
                : null;
            this.animationLockUntil = Time.time + (data?.AttackDurationSec ?? 1.2f);
        }

        private void SyncDeadEnemies()
        {
            int[]       aliveEnemySlots = this.battlefieldView.GetOccupiedEnemySlots();
            var         aliveSet        = new HashSet<int>(aliveEnemySlots);
            var         dead            = new List<int>();

            foreach (int slot in this.knownAliveEnemySlots)
            {
                if (!aliveSet.Contains(slot))
                    dead.Add(slot);
            }

            for (int i = 0; i < dead.Count; i++)
            {
                this.atbSystem.MarkDead(dead[i], ATBActorKind.Enemy);
                this.knownAliveEnemySlots.Remove(dead[i]);
            }
        }

        private static List<ATBActorConfig> BuildATBConfigs(EncounterData encounter, IOperatorRoster roster)
        {
            var configs = new List<ATBActorConfig>();

            for (int i = 0; i < roster.Count; i++)
            {
                int speed = roster[i].Data?.Speed ?? 50;
                configs.Add(new ATBActorConfig(i, ATBActorKind.Operator, speed / 100f));
            }

            for (int i = 0; i < encounter.EnemySlots.Length; i++)
            {
                EnemyData? data = encounter.EnemySlots[i];
                if (data == null) continue;
                float gps = data.AttackBaseSec > 0f ? 1f / data.AttackBaseSec : 1f;
                configs.Add(new ATBActorConfig(i, ATBActorKind.Enemy, gps));
            }

            return configs;
        }

        private IOperatorEcgFeedback? ResolveEcgFeedback()
        {
            var behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IOperatorEcgFeedback feedback)
                    return feedback;
            }
            return null;
        }

        private void SyncAllEcgStates()
        {
            if (this.ecgFeedback == null) return;
            for (int i = 0; i < this.roster.Count; i++)
            {
                bool isPresent = i < this.roster.Count && this.roster[i].IsPresent;
                this.ecgFeedback.SetOperatorHealthState(
                    i,
                    isPresent ? this.roster[i].HpRatio : 0f,
                    isPresent && this.roster[i].IsAlive);
            }
        }
    }
}
```

- [ ] **Step 2: Delete old files**

Delete:
- `Assets/Scripts/Combat/EnemyAttackScheduler.cs`
- `Assets/Scripts/Combat/EnemyAttackController.cs`

- [ ] **Step 3: Comment out deleted registration in CombatScope.cs**

In `Assets/Scripts/Combat/CombatScope.cs`, comment out the old registration:

```csharp
// builder.RegisterComponentInHierarchy<EnemyAttackController>().AsSelf();
```

(Task 6 will replace this line with the CombatOrchestrator registration.)

- [ ] **Step 4: Open Unity, check console**

Expected: no compile errors. The comment-out in CombatScope ensures no reference to the deleted types.

- [ ] **Step 5: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/CombatOrchestrator.cs Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs
git rm Game/CrimsonDraft/Assets/Scripts/Combat/EnemyAttackScheduler.cs Game/CrimsonDraft/Assets/Scripts/Combat/EnemyAttackController.cs
git commit -m "feat(combat): add CombatOrchestrator; remove EnemyAttackScheduler and EnemyAttackController"
```

---

## Task 5: CombatMenuController + States

**Files:**
- Modify: `Assets/Scripts/Combat/UI/CombatMenuController.cs`
- Modify: `Assets/Scripts/Combat/States/OperatorSelectionState.cs`
- Modify: `Assets/Scripts/Combat/States/CommandPanelState.cs`
- Modify: `Assets/Scripts/Combat/States/SubPanelState.cs`
- Modify: `Assets/Scripts/Combat/States/ShotCountSelectionState.cs`
- Modify: `Assets/Scripts/Combat/States/TargetSelectionState.cs`
- Modify: `Assets/Scripts/Combat/States/AimingState.cs`

- [ ] **Step 1: Modify CombatMenuController.cs — add shared state property**

In the `#region Shared state` block, add after `ReloadAmmoBoxIndices`:

```csharp
internal ICombatOrchestrator Orchestrator { get; private set; } = null!;
```

- [ ] **Step 2: Modify CombatMenuController.cs — add injected fields**

After `private readonly IInventoryService inventory;`, add:

```csharp
private readonly ICombatOrchestrator                          orchestrator;
private readonly ISubscriber<ShootConfigurationRequestedEvent> shootSubscriber;
private IDisposable? shootSubscription;
```

- [ ] **Step 3: Modify CombatMenuController.cs — update [Preserve] constructor**

Replace the existing `[Preserve]` constructor with:

```csharp
[Preserve]
public CombatMenuController(
    ICombatActionMenuView                          menuView,
    ICommandPanelView                              commandPanel,
    ISubPanelView                                  subPanel,
    IShotCountView                                 shotCountView,
    IPublisher<CombatEndedEvent>                   combatEndedPublisher,
    IAimView                                       aimView,
    IBattlefieldView                               battlefieldView,
    IOperatorRoster                                roster,
    IInventoryService                              inventory,
    IInputService                                  inputService,
    ICombatOrchestrator                            orchestrator,
    ISubscriber<ShootConfigurationRequestedEvent>  shootSubscriber)
{
    this.menuView             = menuView;
    this.commandPanel         = commandPanel;
    this.subPanel             = subPanel;
    this.shotCountView        = shotCountView;
    this.combatEndedPublisher = combatEndedPublisher;
    this.aimView              = aimView;
    this.battlefieldView      = battlefieldView;
    this.roster               = roster;
    this.inventory            = inventory;
    this.inputService         = inputService;
    this.orchestrator         = orchestrator;
    this.shootSubscriber      = shootSubscriber;
}
```

Also update the `internal` test constructor to accept optional parameters (add after the existing internal constructor, or add defaults):

```csharp
internal CombatMenuController(
    ICombatActionMenuView        menuView,
    ICommandPanelView            commandPanel,
    ISubPanelView                subPanel,
    IShotCountView               shotCountView,
    IPublisher<CombatEndedEvent> combatEndedPublisher,
    IAimView                     aimView,
    IBattlefieldView             battlefieldView,
    IOperatorRoster              roster,
    IInventoryService            inventory,
    ICombatOrchestrator?         orchestrator   = null,
    ISubscriber<ShootConfigurationRequestedEvent>? shootSubscriber = null)
{
    this.menuView             = menuView;
    this.commandPanel         = commandPanel;
    this.subPanel             = subPanel;
    this.shotCountView        = shotCountView;
    this.combatEndedPublisher = combatEndedPublisher;
    this.aimView              = aimView;
    this.battlefieldView      = battlefieldView;
    this.roster               = roster;
    this.inventory            = inventory;
    this.orchestrator         = orchestrator!;
    this.shootSubscriber      = shootSubscriber!;
}
```

- [ ] **Step 4: Modify CombatMenuController.cs — wire up in Initialize**

In `IInitializable.Initialize()`, add before `this.TransitionTo(this.OperatorSelState)`:

```csharp
this.Orchestrator      = this.orchestrator;
this.shootSubscription = this.shootSubscriber.Subscribe(e => BeginShootConfiguration(e.OperatorSlot));
```

- [ ] **Step 5: Modify CombatMenuController.cs — Dispose and BeginShootConfiguration**

In `IDisposable.Dispose()`, add:

```csharp
this.shootSubscription?.Dispose();
```

In the `#region Internal API (testable)` block, add:

```csharp
internal void BeginShootConfiguration(int slot)
{
    this.SelectedOperator = slot;
    this.TransitionTo(this.ShotCountState);
}
```

- [ ] **Step 6: Modify OperatorSelectionState.cs**

At the top of `OnOperatorSelected`, add the ATB ready guard:

```csharp
public void OnOperatorSelected(int index)
{
    if (!this.context.Orchestrator.IsOperatorReady(index)) return;
    // ... rest unchanged
```

- [ ] **Step 7: Modify CommandPanelState.cs — Shoot branch**

Replace the Shoot branch in `OnCommandSelected`:

Old:
```csharp
if (command == CombatCommand.Shoot)
{
    if (GetMaxAvailableShotCount() <= 0) return;
    this.commandPanel.SetDimmed(true);
    this.menuView.SetDimmed(true);
    this.context.TransitionTo(this.context.ShotCountState);
    return;
}
```

New:
```csharp
if (command == CombatCommand.Shoot)
{
    if (GetMaxAvailableShotCount() <= 0) return;
    this.context.Orchestrator.EnqueueAction(PendingAction.Shoot(this.context.SelectedOperator));
    this.commandPanel.Hide();
    this.menuView.SetDimmed(false);
    this.context.TransitionTo(this.context.OperatorSelState);
    return;
}
```

- [ ] **Step 8: Modify SubPanelState.cs — OnItemSelected**

Replace `OnItemSelected` body:

```csharp
public void OnItemSelected(int index)
{
    int[] indices = this.context.ReloadAmmoBoxIndices;
    if (index >= indices.Length) return;

    int op = this.context.SelectedOperator;
    this.context.Orchestrator.EnqueueAction(PendingAction.Reload(op, indices[index]));

    this.subPanel.Hide();
    this.context.TransitionTo(this.context.OperatorSelState);
}
```

- [ ] **Step 9: Modify ShotCountSelectionState.cs — Wait mode**

Replace `Enter` and `Exit`:

```csharp
public void Enter()
{
    this.context.Orchestrator.SetWaitMode(true);
    int max = GetMaxAvailable();
    this.context.SelectedShotCount = 1;
    this.shotCountView.Show(this.commandPanel.PanelRect, 1, max);
}

public void Exit()
{
    this.context.Orchestrator.SetWaitMode(false);
    this.shotCountView.Hide();
}
```

Replace `OnCancel` (player is committed to Shoot — cancel is a no-op):

```csharp
public void OnCancel() { }
```

- [ ] **Step 10: Modify TargetSelectionState.cs — Wait mode + cancel target**

Replace `Enter` and add `Exit`:

```csharp
public void Enter()
{
    this.context.Orchestrator.SetWaitMode(true);
    this.occupiedSlots = this.battlefieldView.GetOccupiedEnemySlots();
    this.cursor        = 0;
    if (this.occupiedSlots.Length > 0)
        this.battlefieldView.SetEnemyTargetIndicator(this.occupiedSlots[0]);
}

public void Exit()
{
    this.context.Orchestrator.SetWaitMode(false);
}
```

Replace `OnCancel` (cancel goes back to ShotCount to pick a different count, not to CommandPanel):

```csharp
public void OnCancel()
{
    this.battlefieldView.HideEnemyTargetIndicator();
    this.context.TransitionTo(this.context.ShotCountState);
}
```

- [ ] **Step 11: Modify AimingState.cs — Wait mode + NotifyShootCompleted**

Replace `Enter` and `Exit`:

```csharp
public void Enter()
{
    this.context.Orchestrator.SetWaitMode(true);
    this.awaitingDismiss = false;
    this.aimView.OnShotsResolved += HandleShotsResolved;
    this.aimView.Show();
}

public void Exit()
{
    this.context.Orchestrator.SetWaitMode(false);
    this.aimView.OnShotsResolved -= HandleShotsResolved;
}
```

Update `CloseAimAndReturnToOperatorSelection` — call `NotifyShootCompleted` first:

```csharp
private void CloseAimAndReturnToOperatorSelection()
{
    this.context.Orchestrator.NotifyShootCompleted();
    this.context.CurrentTargetSlot = -1;
    this.context.SelectedShotCount = 1;
    this.awaitingDismiss           = false;
    this.aimView.Hide();
    this.commandPanel.Hide();
    this.context.TransitionTo(this.context.OperatorSelState);
}
```

- [ ] **Step 12: Open Unity, verify no compile errors**

Expected: Console clean. If `Orchestrator` property is not found on `context`, verify Step 1 was applied correctly.

- [ ] **Step 13: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs Game/CrimsonDraft/Assets/Scripts/Combat/States/OperatorSelectionState.cs Game/CrimsonDraft/Assets/Scripts/Combat/States/CommandPanelState.cs Game/CrimsonDraft/Assets/Scripts/Combat/States/SubPanelState.cs Game/CrimsonDraft/Assets/Scripts/Combat/States/ShotCountSelectionState.cs Game/CrimsonDraft/Assets/Scripts/Combat/States/TargetSelectionState.cs Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs
git commit -m "feat(combat): wire ICombatOrchestrator into CombatMenuController and all states"
```

---

## Task 6: CombatScope + GameLifetimeScope + Scene

**Files:**
- Modify: `Assets/Scripts/Infrastructure/GameLifetimeScope.cs`
- Modify: `Assets/Scripts/Combat/CombatScope.cs`
- Unity: add `CombatOrchestrator` MonoBehaviour to Combat.unity

- [ ] **Step 1: Register ShootConfigurationRequestedEvent in GameLifetimeScope.cs**

After the two existing `RegisterMessageBroker` calls, add:

```csharp
builder.RegisterMessageBroker<ShootConfigurationRequestedEvent>(options);
```

- [ ] **Step 2: Update CombatScope.cs — replace entire Configure body**

```csharp
protected override void Configure(IContainerBuilder builder)
{
    builder.Register<CombatSessionController>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();

    builder.RegisterComponentInHierarchy<CombatActionMenuView>().AsImplementedInterfaces();
    builder.RegisterComponentInHierarchy<CommandPanelView>().AsImplementedInterfaces();
    builder.RegisterComponentInHierarchy<SubPanelView>().AsImplementedInterfaces();
    builder.RegisterComponentInHierarchy<ShotCountView>().AsImplementedInterfaces();
    builder.RegisterComponentInHierarchy<AimViewController>().AsImplementedInterfaces();
    builder.RegisterComponentInHierarchy<BattlefieldView>().AsImplementedInterfaces();

    builder.RegisterInstance(this.encounterDatabase);

    builder.Register<ATBSystem>(Lifetime.Scoped).AsSelf();
    builder.Register<CombatActionQueue>(Lifetime.Scoped).AsSelf();
    builder.RegisterComponentInHierarchy<CombatOrchestrator>()
        .AsSelf().AsImplementedInterfaces();

    builder.Register<BattlefieldPresenter>(Lifetime.Scoped).AsImplementedInterfaces();
    builder.RegisterComponentInHierarchy<CombatCameraRegistrar>().AsImplementedInterfaces();

    builder.Register<CombatMenuController>(Lifetime.Scoped)
        .AsSelf().AsImplementedInterfaces();
}
```

- [ ] **Step 3: Add CombatOrchestrator to Combat.unity scene**

In Unity Editor:
1. Open `Assets/Scenes/Production/Combat.unity`
2. Create empty GameObject → name it `CombatOrchestrator`
3. Add Component: `CombatOrchestrator`
4. Save the scene (Ctrl+S)

- [ ] **Step 4: Enter Play mode, check console**

Expected: no VContainer errors, no null reference exceptions. Combat scene initializes without error.

If you see `VContainer: CombatOrchestrator not found in hierarchy`, the MonoBehaviour was not added to the scene — repeat Step 3.

If you see `ICombatOrchestrator is not registered`, verify the `AsImplementedInterfaces()` call is present in CombatScope.

- [ ] **Step 5: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs Game/CrimsonDraft/Assets/Scenes/Production/Combat.unity
git commit -m "feat(combat): register ATBSystem, CombatActionQueue, CombatOrchestrator in DI; wire ShootConfigurationRequestedEvent"
```

---

## Task 7: CombatDebugView

**Files:**
- Create: `Assets/Scripts/Combat/UI/CombatDebugView.cs`
- Modify: `Assets/Scripts/Combat/CombatScope.cs`
- Unity: add `CombatDebugView` MonoBehaviour + debug canvas to Combat.unity

- [ ] **Step 1: Create CombatDebugView.cs**

```csharp
// Assets/Scripts/Combat/UI/CombatDebugView.cs
#nullable enable

#if UNITY_EDITOR || DEBUG_COMBAT

using System.Text;
using TMPro;
using UnityEngine;
using VContainer;

namespace CrimsonDraft.Combat
{
    public sealed class CombatDebugView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI? text;

        private ATBSystem?         atbSystem;
        private CombatActionQueue? actionQueue;
        private bool               initialized;

        [Inject]
        public void Construct(ATBSystem atbSystem, CombatActionQueue actionQueue)
        {
            this.atbSystem   = atbSystem;
            this.actionQueue = actionQueue;
            this.initialized = true;
        }

        private void Update()
        {
            if (!this.initialized || this.text == null) return;
            this.text.text = BuildDebugText();
        }

        private string BuildDebugText()
        {
            var sb = new StringBuilder();

            sb.AppendLine("[ATB ACTORS]");
            if (this.atbSystem != null)
            {
                foreach (ATBActorState actor in this.atbSystem.Actors)
                {
                    if (actor.IsDead) continue;
                    string kind  = actor.Config.Kind == ATBActorKind.Operator ? "OP" : "EN";
                    string bar   = GaugeBar(actor.Gauge);
                    string state = actor.IsReady
                        ? (actor.IsAwaitingCommand ? "READY*" : "READY")
                        : "FILLING";
                    sb.AppendLine($"  {kind}[{actor.Config.SlotIndex}] {bar} {actor.Gauge:P0} {state}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("[QUEUE]");
            if (this.actionQueue != null)
            {
                PendingAction[] pending = this.actionQueue.ToArray();
                if (pending.Length == 0)
                {
                    sb.AppendLine("  (empty)");
                }
                else
                {
                    for (int i = 0; i < pending.Length; i++)
                    {
                        PendingAction a      = pending[i];
                        string        prefix = i == 0 ? "► " : "  ";
                        sb.AppendLine($"  {prefix}[{i}] {a.Type} slot={a.SlotIndex}");
                    }
                }
            }

            return sb.ToString();
        }

        private static string GaugeBar(float gauge)
        {
            const int width  = 10;
            int       filled = Mathf.RoundToInt(gauge * width);
            return new string('█', filled) + new string('░', width - filled);
        }
    }
}

#endif
```

- [ ] **Step 2: Register CombatDebugView in CombatScope.cs**

Add at the end of `Configure`, inside `#if` guards:

```csharp
#if UNITY_EDITOR || DEBUG_COMBAT
builder.RegisterComponentInHierarchy<CombatDebugView>().AsSelf();
#endif
```

- [ ] **Step 3: Add CombatDebugView to Combat.unity**

In Unity Editor:
1. Open `Assets/Scenes/Production/Combat.unity`
2. Create Canvas: GameObject → UI → Canvas, name `DebugCanvas`
   - Render Mode: Screen Space Overlay
   - Sort Order: 99
3. Create TextMeshProUGUI child of DebugCanvas: name `ATBDebugText`
   - Anchor: top-left, pivot (0, 1)
   - Pos: (10, -10, 0), Width: 400, Height: 500
   - Font size: 12, alignment: top-left
4. Add `CombatDebugView` component to `DebugCanvas`
5. Assign `ATBDebugText` to the `text` field in inspector
6. Save scene

- [ ] **Step 4: Enter Play mode, verify overlay**

Expected: top-left corner shows actor gauges and queue contents updating in real time.

- [ ] **Step 5: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatDebugView.cs Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs Game/CrimsonDraft/Assets/Scenes/Production/Combat.unity
git commit -m "feat(combat): add CombatDebugView for ATB gauge and queue visualization"
```

---

## Self-Review

**Spec coverage:**
- ATBActorState + ATBSystem (gauge model, tick, pause, dead) ✓ Task 1
- PendingAction FIFO queue ✓ Task 2
- Speed stat on OperatorData ✓ Task 3
- ICombatOrchestrator interface ✓ Task 3
- ShootConfigurationRequestedEvent ✓ Task 3
- CombatOrchestrator central loop (advance gauges, enqueue enemy attacks, process head) ✓ Task 4
- Wait mode in ShotCount, TargetSel, Aiming ✓ Task 5
- Shoot → enqueue → configure at head (not direct) ✓ Task 5
- Reload → SubPanel → enqueue → execute at head ✓ Task 5
- EnemyAttack with jitter-based reset rate ✓ Task 4
- Animation lock serializing actions ✓ Task 4 (`animationLockUntil`)
- Debug panel ✓ Task 7

**Placeholder scan:** No TBD/TODO in code steps. UseItem at queue head is a no-op stub — intentional, inventory UseItem not yet implemented.

**Type consistency:** `ATBActorKind`, `ATBActorConfig`, `ATBActorState`, `PendingAction`, `PendingActionType`, `CombatActionQueue`, `ICombatOrchestrator`, `ShootConfigurationRequestedEvent`, `CombatOrchestrator` — all consistent across tasks.

**Known limitation:** Enemy jitter resets gauge rate per-attack (Task 4). Operator gauge rate is fixed at init from `Speed` stat. Both match the GDD spec.
