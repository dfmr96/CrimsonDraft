# Focus Fire Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the player mark several ready operators for Focus Fire; when an unmarked operator fires, the whole group resolves as one shared aim QTE, each participant applying their own weapon's damage/recoil from that locked position.

**Architecture:** Marking freezes an operator's ATB (reusing `ATBSystem.FreezeActor`, the same mechanism enemy stagger already uses) without enqueuing anything. Triggering (picking Shoot while ≥1 marked) enqueues one `PendingAction.FocusFire` carrying the full participant list, processed by `CombatOrchestrator` exactly like `Shoot` but publishing a new `FocusFireConfigurationRequestedEvent`. `CombatMenuController` walks the group through the existing `ShotCountSelectionState` once per participant, then a single `TargetSelectionState` + `AimingState` pass — the trigger (last in the list) drives the one real interactive QTE; every marked participant's shots are resolved from that same locked aim position via a new `IAimView.ResolveShotsForWeapon` method, reusing their own weapon's burst pattern.

**Tech Stack:** Unity C#, VContainer (DI), MessagePipe (pub/sub), UniTask, NUnit EditMode tests with hand-written fakes.

## Global Constraints

- `#nullable enable` at the top of every new/touched file (already present everywhere this plan touches).
- No `System.Linq` in `Combat/` — use plain loops, matching existing convention.
- Tests use plain C# fakes already defined in `CombatMenuControllerTests.cs` — extend them, don't introduce a mocking library.
- `CombatOrchestrator` has no dedicated EditMode test file (MonoBehaviour, `Update()`-coupled to many injected interfaces) — this plan does not add one, matching the established boundary from the Poise/stagger work. Its queue-handling changes are verified by compiling clean + the existing full test suite showing no regressions; deeper verification is manual (Play Mode).
- No `Co-Authored-By` trailers in commit messages (project convention, `CLAUDE.md`).
- Tests run via Unity Test Runner / UnityMCP `run_tests` — there is no CLI test command in this project.

---

## Task 1: Marking — data, orchestrator hook, command panel, "last available" rule

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatCommand.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/ICombatActionMenuView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatActionMenuView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/ICombatOrchestrator.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/CombatOrchestrator.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/CommandPanelState.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/OperatorSelectionState.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Interfaces:**
- Produces: `CombatCommand.FocusFire`, `ICombatActionMenuView.SetOperatorFocusFireMarked(int, bool)`, `ICombatOrchestrator.MarkOperatorForFocusFire(int)`, `CombatMenuController.FocusFireMarked : List<int>`.

- [ ] **Step 1: Write the failing tests**

Add to `CombatMenuControllerTests.cs`, after the existing stagger/death tests (search for `ShotFired_killingShot_doesNotAlsoTriggerStagger` and insert after it):

```csharp
        [Test]
        public void CommandPanel_focusFire_marksOperatorAndFreezesAtb()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.FocusFire);

            CollectionAssert.Contains(c.FocusFireMarked, 0);
            Assert.AreEqual(1, this.orchestrator.MarkOperatorForFocusFireCallCount);
            Assert.AreEqual(0, this.orchestrator.LastMarkedFocusFireSlot);
            Assert.AreEqual(1, this.menuView.FocusFireMarkedCallCount);
            Assert.IsTrue(this.menuView.LastFocusFireMarkedValue);
            Assert.IsFalse(this.commandPanel.IsVisible);
        }

        [Test]
        public void OperatorSelected_withNoneMarked_enablesFocusFire()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);

            Assert.IsTrue(this.commandPanel.IsCommandEnabled(CombatCommand.FocusFire));
        }

        [Test]
        public void OperatorSelected_withOneOfThreeMarked_stillEnablesFocusFireForAnother()
        {
            var c = BuildAndInit(); // default FakeOperatorRoster has 3 slots, all alive
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.FocusFire); // marks 0

            this.menuView.RaiseOnOperatorSelected(1);

            Assert.IsTrue(this.commandPanel.IsCommandEnabled(CombatCommand.FocusFire));
        }

        [Test]
        public void OperatorSelected_withAllOthersMarked_disablesFocusFireForTheLastOne()
        {
            var c = BuildAndInit(); // 3 slots
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.FocusFire); // marks 0

            this.menuView.RaiseOnOperatorSelected(1);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.FocusFire); // marks 1

            this.menuView.RaiseOnOperatorSelected(2); // only unmarked operator left

            Assert.IsFalse(this.commandPanel.IsCommandEnabled(CombatCommand.FocusFire));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL to compile — `CombatCommand.FocusFire`, `orchestrator.MarkOperatorForFocusFireCallCount`, `menuView.FocusFireMarkedCallCount`, and `c.FocusFireMarked` don't exist yet.

- [ ] **Step 3: Add the `FocusFire` command**

`Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatCommand.cs`:

```csharp
    public enum CombatCommand { Shoot, Items, FocusFire }
```

- [ ] **Step 4: Add the marked-visual hook to `ICombatActionMenuView` and `CombatActionMenuView`**

`ICombatActionMenuView.cs`, add next to `SetOperatorDimmed`:

```csharp
        void SetOperatorFocusFireMarked(int index, bool marked);
```

`CombatActionMenuView.cs`: add a parallel serialized array (next to `operatorWeaponIcons`) and the implementation (next to `SetOperatorDimmed`):

```csharp
        [SerializeField] private Image[] operatorFocusFireMarkers = Array.Empty<Image>();
```

```csharp
        public void SetOperatorFocusFireMarked(int index, bool marked)
        {
            if (index < 0 || index >= this.operatorFocusFireMarkers.Length) return;
            var marker = this.operatorFocusFireMarkers[index];
            if (marker != null) marker.gameObject.SetActive(marked);
        }
```

`operatorFocusFireMarkers` defaults to an empty array, so this is a safe no-op until the icons are assigned in the Inspector — same "code adds the hook, art gets wired later" split used for the enemy Animator work.

- [ ] **Step 5: Add `MarkOperatorForFocusFire` to `ICombatOrchestrator` and `CombatOrchestrator`**

`ICombatOrchestrator.cs`:

```csharp
#nullable enable

namespace CrimsonDraft.Combat
{
    public interface ICombatOrchestrator
    {
        void EnqueueAction(PendingAction action);
        void SetWaitMode(bool paused);
        bool IsOperatorReady(int slotIndex);
        void NotifyShootCompleted();
        void NotifyEnemyStaggered(int enemySlot);
        void MarkOperatorForFocusFire(int operatorSlot);
    }
}
```

`CombatOrchestrator.cs`, add next to `NotifyEnemyStaggered`:

```csharp
        public void MarkOperatorForFocusFire(int operatorSlot)
        {
            // Same "reset then freeze" shape as NotifyEnemyStaggered — the marked
            // operator's gauge must not tick back up to ready while it waits, or
            // NotifyReadyOperators() would offer it a command panel again.
            this.atbSystem.ResetActor(operatorSlot, ATBActorKind.Operator);
            this.atbSystem.FreezeActor(operatorSlot, ATBActorKind.Operator);
        }
```

- [ ] **Step 6: Add `FocusFireMarked` shared state to `CombatMenuController`**

In the `#region Shared state` block, next to `CurrentTargetSlot`:

```csharp
        internal List<int> FocusFireMarked { get; } = new();
```

Add `using System.Collections.Generic;` to the file's usings if not already present (check the top of `CombatMenuController.cs` first — it currently only has `System`, `MessagePipe`, `UnityEngine`, `UnityEngine.InputSystem`, `VContainer.Unity`, and four `CrimsonDraft.*` usings).

- [ ] **Step 7: Wire marking in `CommandPanelState`**

`CommandPanelState.cs`, `OnCommandSelected` currently handles `Shoot` and `Items`. Add a `FocusFire` branch:

```csharp
        public void OnCommandSelected(CombatCommand command)
        {
            if (command == CombatCommand.Shoot)
            {
                if (GetMaxAvailableShotCount() <= 0) return;
                this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);
                this.context.Orchestrator.EnqueueAction(PendingAction.Shoot(this.context.SelectedOperator));
                this.commandPanel.Hide();
                this.menuView.SetDimmed(false);
                this.context.TransitionTo(this.context.OperatorSelState);
                return;
            }

            if (command == CombatCommand.FocusFire)
            {
                this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);
                int slot = this.context.SelectedOperator;
                this.context.FocusFireMarked.Add(slot);
                this.context.Orchestrator.MarkOperatorForFocusFire(slot);
                this.menuView.SetOperatorFocusFireMarked(slot, true);
                this.commandPanel.Hide();
                this.menuView.SetDimmed(false);
                this.context.TransitionTo(this.context.OperatorSelState);
                return;
            }

            if (command == CombatCommand.Items)
            {
                this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);
                this.commandPanel.Hide();
                this.context.TransitionTo(this.context.CombatInventoryState);
            }
        }
```

(The `Shoot` branch stays exactly as it is here — Task 2 changes it.)

- [ ] **Step 8: Add the "last available" disable rule**

This lives in `OperatorSelectionState.OnOperatorSelected`, not `CommandPanelState` — that's the existing home for the equivalent Shoot/ammo enablement check (`this.commandPanel.SetCommandEnabled(CombatCommand.Shoot, hasAmmo);`). Add right after it:

```csharp
        public void OnOperatorSelected(int index)
        {
            if (UnityEngine.Time.unscaledTime < this.canAcceptSubmitAt) return;
            if (!this.context.Orchestrator.IsOperatorReady(index)) return;
            this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);
            this.context.SelectedOperator = index;
            bool hasAmmo = this.roster.Count > index && (this.roster[index].ActiveWeapon?.CurrentAmmo ?? 0) > 0;
            this.commandPanel.SetCommandEnabled(CombatCommand.Shoot, hasAmmo);
            bool wouldExhaustFocusFireGroup = this.context.FocusFireMarked.Count >= this.roster.GetAliveSlots().Count - 1;
            this.commandPanel.SetCommandEnabled(CombatCommand.FocusFire, !wouldExhaustFocusFireGroup);
            this.commandPanel.Show(this.menuView.GetOperatorOverviewRect(index));
            this.menuView.SetDimmed(true);
            this.battlefieldView.DimOperatorIndicator();
            this.context.TransitionTo(this.context.CommandPanelState);
        }
```

- [ ] **Step 9: Fix the now-broken `FakeCombatActionMenuView` and `FakeOrchestrator` test fakes**

`FakeCombatActionMenuView` (in `CombatMenuControllerTests.cs`) needs the new interface member. Add next to `SetOperatorDimmed`:

```csharp
            public int  FocusFireMarkedCallCount  { get; private set; }
            public bool LastFocusFireMarkedValue  { get; private set; }
            public int  LastFocusFireMarkedSlot   { get; private set; } = -1;
            public void SetOperatorFocusFireMarked(int index, bool marked)
            {
                this.FocusFireMarkedCallCount++;
                this.LastFocusFireMarkedValue = marked;
                this.LastFocusFireMarkedSlot  = index;
            }
```

`FakeOrchestrator` needs the new interface member. Add next to `NotifyEnemyStaggered`:

```csharp
            public int MarkOperatorForFocusFireCallCount { get; private set; }
            public int LastMarkedFocusFireSlot           { get; private set; } = -1;
            public void MarkOperatorForFocusFire(int operatorSlot)
            {
                this.MarkOperatorForFocusFireCallCount++;
                this.LastMarkedFocusFireSlot = operatorSlot;
            }
```

- [ ] **Step 10: Run tests to verify they pass**

Run the full `CombatMenuControllerTests` suite via Test Runner.
Expected: all 4 new tests PASS, and no existing test regresses (in particular the existing `Shoot`-flow tests, since `CommandPanelState.OnCommandSelected`'s `Shoot`/`Items` branches are untouched and `OperatorSelectionState.OnOperatorSelected`'s new line is purely additive).

- [ ] **Step 11: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatCommand.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/UI/ICombatActionMenuView.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatActionMenuView.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/ICombatOrchestrator.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/CombatOrchestrator.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/States/CommandPanelState.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/States/OperatorSelectionState.cs \
        Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat): add Focus Fire marking and the last-available-operator rule"
```

---

## Task 2: Triggering — queue plumbing and the group PendingAction

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/PendingAction.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Events/GameEvents.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/ICombatOrchestrator.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/CombatOrchestrator.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/CommandPanelState.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Interfaces:**
- Consumes: `CombatMenuController.FocusFireMarked` (Task 1).
- Produces: `PendingActionType.FocusFire`, `PendingAction.FocusFire(int, int[])`, `FocusFireConfigurationRequestedEvent`, `ICombatOrchestrator.NotifyFocusFireCompleted()`.

- [ ] **Step 1: Write the failing tests**

Add to `CombatMenuControllerTests.cs`, after Task 1's tests:

```csharp
        [Test]
        public void CommandPanel_shoot_withMarkedOperators_enqueuesFocusFireAction()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.FocusFire); // marks 0

            this.menuView.RaiseOnOperatorSelected(1);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot); // triggers, 1 is the trigger

            Assert.IsNotNull(this.orchestrator.LastEnqueuedAction);
            var action = this.orchestrator.LastEnqueuedAction!.Value;
            Assert.AreEqual(PendingActionType.FocusFire, action.Type);
            Assert.AreEqual(1, action.SlotIndex);
            CollectionAssert.AreEqual(new[] { 0, 1 }, action.FocusFireParticipants);
        }

        [Test]
        public void CommandPanel_shoot_withMarkedOperators_clearsMarksAndUnmarksView()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.FocusFire); // marks 0

            this.menuView.RaiseOnOperatorSelected(1);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);

            Assert.AreEqual(0, c.FocusFireMarked.Count);
            Assert.AreEqual(2, this.menuView.FocusFireMarkedCallCount); // marked(0,true) then unmarked(0,false)
            Assert.IsFalse(this.menuView.LastFocusFireMarkedValue);
            Assert.AreEqual(0, this.menuView.LastFocusFireMarkedSlot);
        }

        [Test]
        public void CommandPanel_shoot_withNoMarkedOperators_enqueuesNormalShoot()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);

            Assert.IsNotNull(this.orchestrator.LastEnqueuedAction);
            var action = this.orchestrator.LastEnqueuedAction!.Value;
            Assert.AreEqual(PendingActionType.Shoot, action.Type);
            Assert.AreEqual(0, action.SlotIndex);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL to compile — `PendingActionType.FocusFire`, `action.FocusFireParticipants` don't exist yet.

- [ ] **Step 3: Extend `PendingAction`**

`PendingAction.cs`:

```csharp
#nullable enable

namespace CrimsonDraft.Combat
{
    public enum PendingActionType { Shoot, UseItem, EnemyAttack, EnemyRecover, FocusFire }

    public readonly struct PendingAction
    {
        public PendingActionType Type               { get; }
        public int               SlotIndex          { get; }
        public int               ItemIndex          { get; }
        public int               TargetOperatorSlot { get; }
        public int               Damage             { get; }
        public int[]             FocusFireParticipants { get; }

        private PendingAction(
            PendingActionType type,
            int slotIndex,
            int itemIndex          = -1,
            int targetOperatorSlot = -1,
            int damage             = 0,
            int[]? focusFireParticipants = null)
        {
            this.Type               = type;
            this.SlotIndex          = slotIndex;
            this.ItemIndex          = itemIndex;
            this.TargetOperatorSlot = targetOperatorSlot;
            this.Damage             = damage;
            this.FocusFireParticipants = focusFireParticipants ?? System.Array.Empty<int>();
        }

        public static PendingAction Shoot(int operatorSlot) =>
            new PendingAction(PendingActionType.Shoot, operatorSlot);

        public static PendingAction UseItem(int operatorSlot, int itemIndex) =>
            new PendingAction(PendingActionType.UseItem, operatorSlot, itemIndex: itemIndex);

        public static PendingAction EnemyAttack(int enemySlot, int targetOperatorSlot, int damage) =>
            new PendingAction(PendingActionType.EnemyAttack, enemySlot,
                targetOperatorSlot: targetOperatorSlot, damage: damage);

        public static PendingAction EnemyRecover(int enemySlot) =>
            new PendingAction(PendingActionType.EnemyRecover, enemySlot);

        public static PendingAction FocusFire(int triggerOperatorSlot, int[] participants) =>
            new PendingAction(PendingActionType.FocusFire, triggerOperatorSlot, focusFireParticipants: participants);
    }
}
```

- [ ] **Step 4: Add the event**

`GameEvents.cs`, add next to `ShootConfigurationRequestedEvent`:

```csharp
    public readonly struct FocusFireConfigurationRequestedEvent
    {
        public int[] ParticipantSlots { get; }

        public FocusFireConfigurationRequestedEvent(int[] participantSlots)
        {
            this.ParticipantSlots = participantSlots;
        }
    }
```

- [ ] **Step 5: Register the event's message broker**

`GameLifetimeScope.cs`, add next to the `ShootConfigurationRequestedEvent` registration:

```csharp
            builder.RegisterMessageBroker<ShootConfigurationRequestedEvent>(options);
            builder.RegisterMessageBroker<FocusFireConfigurationRequestedEvent>(options);
```

- [ ] **Step 6: Add `NotifyFocusFireCompleted` to `ICombatOrchestrator`**

```csharp
#nullable enable

namespace CrimsonDraft.Combat
{
    public interface ICombatOrchestrator
    {
        void EnqueueAction(PendingAction action);
        void SetWaitMode(bool paused);
        bool IsOperatorReady(int slotIndex);
        void NotifyShootCompleted();
        void NotifyEnemyStaggered(int enemySlot);
        void MarkOperatorForFocusFire(int operatorSlot);
        void NotifyFocusFireCompleted();
    }
}
```

- [ ] **Step 7: Wire `CombatOrchestrator`**

Add the injected publisher next to `shootPublisher`:

```csharp
        private IPublisher<ShootConfigurationRequestedEvent> shootPublisher     = null!;
        private IPublisher<FocusFireConfigurationRequestedEvent> focusFirePublisher = null!;
```

Add it to `Construct(...)`'s parameter list and body, right after `shootPublisher`:

```csharp
        [Inject]
        [UnityEngine.Scripting.Preserve]
        public void Construct(
            ATBSystem                                    atbSystem,
            CombatActionQueue                            actionQueue,
            IPublisher<ShootConfigurationRequestedEvent> shootPublisher,
            IPublisher<FocusFireConfigurationRequestedEvent> focusFirePublisher,
            IPublisher<CombatEndedEvent>                 combatEndPublisher,
            IBattlefieldView                             battlefieldView,
            IOperatorRoster                              roster,
            IEncounterContext                            encounterContext,
            IInventoryService                            inventory,
            ICombatActionMenuView                        menuView)
        {
            this.atbSystem          = atbSystem;
            this.actionQueue        = actionQueue;
            this.shootPublisher     = shootPublisher;
            this.focusFirePublisher = focusFirePublisher;
            this.combatEndPublisher = combatEndPublisher;
            this.battlefieldView    = battlefieldView;
            this.roster             = roster;
            this.encounterContext   = encounterContext;
            this.inventory          = inventory;
            this.menuView           = menuView;
        }
```

Extend `IsActorDead` with the `FocusFire` case:

```csharp
        private bool IsActorDead(PendingAction action)
        {
            if (action.Type == PendingActionType.EnemyAttack || action.Type == PendingActionType.EnemyRecover)
            {
                if (this.battlefieldView.IsEnemyDead(action.SlotIndex)) return true;
                ATBActorState? actor = this.atbSystem.GetActor(action.SlotIndex, ATBActorKind.Enemy);
                return actor == null || actor.IsDead;
            }
            if (action.Type == PendingActionType.FocusFire)
            {
                for (int i = 0; i < action.FocusFireParticipants.Length; i++)
                {
                    int s = action.FocusFireParticipants[i];
                    if (s >= this.roster.Count || !this.roster[s].IsAlive) return true;
                }
                return false;
            }
            return action.SlotIndex >= this.roster.Count || !this.roster[action.SlotIndex].IsAlive;
        }
```

Add a `FocusFire` branch to `ProcessQueueHead`, right after the `Shoot` branch:

```csharp
            if (head.Type == PendingActionType.Shoot)
            {
                if (!this.shootConfigurationInProgress)
                {
                    if (IsActorDead(head)) { this.DequeueAction(); return; }
                    this.shootConfigurationInProgress = true;
                    this.shootPublisher.Publish(new ShootConfigurationRequestedEvent(head.SlotIndex));
                }
                return;
            }

            if (head.Type == PendingActionType.FocusFire)
            {
                if (!this.shootConfigurationInProgress)
                {
                    if (IsActorDead(head)) { this.DequeueAction(); return; }
                    this.shootConfigurationInProgress = true;
                    for (int i = 0; i < head.FocusFireParticipants.Length; i++)
                        this.atbSystem.UnfreezeActor(head.FocusFireParticipants[i], ATBActorKind.Operator);
                    this.focusFirePublisher.Publish(new FocusFireConfigurationRequestedEvent(head.FocusFireParticipants));
                }
                return;
            }
```

Add `NotifyFocusFireCompleted`, right after `NotifyShootCompleted`:

```csharp
        public void NotifyFocusFireCompleted()
        {
            if (!this.actionQueue.HasPending) return;
            if (this.actionQueue.Peek().Type != PendingActionType.FocusFire) return;
            this.DequeueAction();
            this.shootConfigurationInProgress = false;
            SetAnimationLock(this.operatorActionDurationSec);
        }
```

(Reuses `shootConfigurationInProgress` — only one action is ever at the head of the queue at a time, so `Shoot` and `FocusFire` never contend for it.)

- [ ] **Step 8: Wire the trigger branch in `CommandPanelState`**

Replace the `Shoot` branch (from Task 1's Step 7) with the group-aware version:

```csharp
            if (command == CombatCommand.Shoot)
            {
                if (GetMaxAvailableShotCount() <= 0) return;
                this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);

                if (this.context.FocusFireMarked.Count > 0)
                {
                    int[] participants = new int[this.context.FocusFireMarked.Count + 1];
                    this.context.FocusFireMarked.CopyTo(participants, 0);
                    participants[participants.Length - 1] = this.context.SelectedOperator;

                    for (int i = 0; i < this.context.FocusFireMarked.Count; i++)
                        this.menuView.SetOperatorFocusFireMarked(this.context.FocusFireMarked[i], false);
                    this.context.FocusFireMarked.Clear();

                    this.context.Orchestrator.EnqueueAction(PendingAction.FocusFire(this.context.SelectedOperator, participants));
                }
                else
                {
                    this.context.Orchestrator.EnqueueAction(PendingAction.Shoot(this.context.SelectedOperator));
                }

                this.commandPanel.Hide();
                this.menuView.SetDimmed(false);
                this.context.TransitionTo(this.context.OperatorSelState);
                return;
            }
```

(`participants[participants.Length - 1]` rather than `[^1]` — check the file's existing style/C# language version before using the index-from-end operator; plain indexing is always safe.)

- [ ] **Step 9: Run tests to verify they pass**

Expected: all 3 new tests PASS, plus every pre-existing test (in particular Task 1's marking tests and all the stagger/death/poise tests from earlier work, since none of their call paths go through the new `FocusFire` branches).

- [ ] **Step 10: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/PendingAction.cs \
        Game/CrimsonDraft/Assets/Scripts/Infrastructure/Events/GameEvents.cs \
        Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/ICombatOrchestrator.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/CombatOrchestrator.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/States/CommandPanelState.cs \
        Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat): enqueue and process a combined Focus Fire action"
```

---

## Task 3: `IAimView.ResolveShotsForWeapon`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IAimView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Interfaces:**
- Produces: `IAimView.ResolveShotsForWeapon(WeaponData?, int) : ResolvedShot[]`.

This task has no dedicated automated test for the real `AimViewController` implementation — `AimViewControllerTests.cs` only tests its `static` pure helpers (`ResolveZone`, `MapUvToTexturePixel`); none of its instance methods are unit tested today because they depend on a live `RectTransform`/`Image` hierarchy. `ResolveShotsForWeapon` reuses existing private instance methods the same way, so it follows the same (already-accepted) boundary. The `FakeAimView` update below is what makes Task 5's tests possible.

- [ ] **Step 1: Add the method to `IAimView`**

```csharp
#nullable enable
using System;
using CrimsonDraft.Inventory;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface IAimView
    {
        event Action<ResolvedShot[]>? OnShotsResolved;
        void ConfigureHitMask(AimHitMaskProfile? profile);
        void ConfigureWeapon(WeaponData? weaponData);
        void SetShotCount(int shotCount);
        void ShowShotFeedback(Vector2 normalizedPos, int damage, bool isMiss);
        void Show();
        void Confirm();
        void Hide();
        ResolvedShot[] ResolveShotsForWeapon(WeaponData? weaponData, int shotCount);
    }
}
```

- [ ] **Step 2: Implement it in `AimViewController`**

Add next to `Confirm()`:

```csharp
        // Reuses the aim position already locked by the real interactive QTE (confirmedLocalPos)
        // without re-running the vertical/horizontal oscillation — used for Focus Fire's marked
        // participants, who share the trigger's aim point but apply their own weapon's burst
        // pattern and dispersion.
        public ResolvedShot[] ResolveShotsForWeapon(WeaponData? weaponData, int shotCount)
        {
            this.ConfigureWeapon(weaponData);
            this.shotCount = Mathf.Max(1, shotCount);
            var firstShotLocal = this.ComputeRandomShotLocal();
            return this.BuildResolvedShots(firstShotLocal, this.shotCount);
        }
```

- [ ] **Step 3: Update `FakeAimView`**

In `CombatMenuControllerTests.cs`, add to `FakeAimView`:

```csharp
            public int ResolveShotsForWeaponCallCount { get; private set; }
            public CrimsonDraft.Inventory.WeaponData? LastResolvedWeaponData { get; private set; }
            public int LastResolvedShotCount { get; private set; }
            public Func<CrimsonDraft.Inventory.WeaponData?, int, ResolvedShot[]>? ResolveShotsForWeaponHandler;

            public ResolvedShot[] ResolveShotsForWeapon(CrimsonDraft.Inventory.WeaponData? weaponData, int shotCount)
            {
                this.ResolveShotsForWeaponCallCount++;
                this.LastResolvedWeaponData = weaponData;
                this.LastResolvedShotCount  = shotCount;

                if (this.ResolveShotsForWeaponHandler != null)
                    return this.ResolveShotsForWeaponHandler(weaponData, shotCount);

                var shots = new ResolvedShot[Mathf.Max(1, shotCount)];
                for (int i = 0; i < shots.Length; i++)
                    shots[i] = new ResolvedShot(i, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20);
                return shots;
            }
```

- [ ] **Step 4: Confirm the project compiles**

Run the full EditMode suite via Test Runner.
Expected: everything compiles (the only production caller of `ResolveShotsForWeapon` is added in Task 5) and no existing test regresses.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/IAimView.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs \
        Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat): add IAimView.ResolveShotsForWeapon for shared-QTE resolution"
```

---

## Task 4: Group shot-count loop

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/ShotCountSelectionState.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Interfaces:**
- Consumes: `FocusFireConfigurationRequestedEvent` (Task 2).
- Produces: `CombatMenuController.FocusFireParticipants : int[]`, `FocusFireParticipantIndex : int`, `FocusFireShotCounts : Dictionary<int,int>`, `BeginFocusFireConfiguration(int[])`.

- [ ] **Step 1: Write the failing tests**

Add to `CombatMenuControllerTests.cs`:

```csharp
        [Test]
        public void BeginFocusFireConfiguration_seedsGroupStateAndEntersShotCountForFirstParticipant()
        {
            var c = BuildAndInit();
            c.BeginFocusFireConfiguration(new[] { 0, 1 });

            Assert.AreEqual(0, c.SelectedOperator);
            CollectionAssert.AreEqual(new[] { 0, 1 }, c.FocusFireParticipants);
            Assert.IsTrue(this.shotCountView.IsVisible);
        }

        [Test]
        public void ShotCountConfirm_groupFlow_loopsThroughParticipantsThenReachesTargetSelection()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            var c = BuildAndInit();
            c.BeginFocusFireConfiguration(new[] { 0, 1 });

            InvokeConfirm(c); // confirms participant 0's shot count

            Assert.AreEqual(1, c.SelectedOperator);
            Assert.AreEqual(1, c.FocusFireShotCounts[0]);
            Assert.IsTrue(this.shotCountView.IsVisible); // re-entered for participant 1

            InvokeConfirm(c); // confirms participant 1's (trigger) shot count -> TargetSelState

            Assert.AreEqual(1, c.FocusFireShotCounts[1]);
            Assert.IsTrue(this.battlefieldView.EnemyTargetVisible);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL to compile — `BeginFocusFireConfiguration`, `FocusFireParticipants`, `FocusFireShotCounts` don't exist yet.

- [ ] **Step 3: Add the group state fields to `CombatMenuController`**

In `#region Shared state`, next to `FocusFireMarked`:

```csharp
        internal int[] FocusFireParticipants     { get; set; } = Array.Empty<int>();
        internal int   FocusFireParticipantIndex { get; set; }
        internal Dictionary<int, int> FocusFireShotCounts { get; } = new();
```

- [ ] **Step 4: Add `BeginFocusFireConfiguration`**

In `#region Internal API (testable)`, next to `BeginShootConfiguration`:

```csharp
        internal void BeginFocusFireConfiguration(int[] participants)
        {
            this.FocusFireParticipants     = participants;
            this.FocusFireParticipantIndex = 0;
            this.FocusFireShotCounts.Clear();
            this.SelectedOperator = participants[0];
            this.commandPanel.RepositionTo(this.menuView.GetOperatorRect(participants[0]));
            this.menuView.SetDimmed(true);
            this.TransitionTo(this.ShotCountState);
        }
```

- [ ] **Step 5: Subscribe to the group event**

Add the field next to `shootSubscriber`:

```csharp
        private readonly ISubscriber<FocusFireConfigurationRequestedEvent> focusFireSubscriber;
        private IDisposable? focusFireSubscription;
```

Add the parameter to **both** constructors (public and internal test one), following `shootSubscriber`'s exact pattern — public constructor requires it, internal test constructor makes it optional (`= null`):

```csharp
        // public constructor: add after shootSubscriber
        ISubscriber<ShootConfigurationRequestedEvent>  shootSubscriber,
        ISubscriber<FocusFireConfigurationRequestedEvent> focusFireSubscriber)
        {
            ...
            this.shootSubscriber      = shootSubscriber;
            this.focusFireSubscriber  = focusFireSubscriber;
        }

        // internal test constructor: add after shootSubscriber, still optional/nullable
        ISubscriber<ShootConfigurationRequestedEvent>? shootSubscriber = null,
        ISubscriber<FocusFireConfigurationRequestedEvent>? focusFireSubscriber = null,
        CombatSfxData?               sfx             = null)
        {
            ...
            this.shootSubscriber      = shootSubscriber!;
            this.focusFireSubscriber  = focusFireSubscriber!;
            this.sfx                  = sfx;
        }
```

Subscribe in `Initialize()`, next to the existing `shootSubscription`:

```csharp
            this.shootSubscription     = this.shootSubscriber?.Subscribe(e => BeginShootConfiguration(e.OperatorSlot));
            this.focusFireSubscription = this.focusFireSubscriber?.Subscribe(e => BeginFocusFireConfiguration(e.ParticipantSlots));
```

And dispose it in `Dispose()`, next to `shootSubscription?.Dispose();`:

```csharp
            this.shootSubscription?.Dispose();
            this.focusFireSubscription?.Dispose();
```

`BuildAndInit` in the test file keeps compiling unchanged — the new parameter is optional and defaults to `null`, same as `shootSubscriber` already does there.

- [ ] **Step 6: Make `ShotCountSelectionState` group-aware**

Replace `OnConfirm`:

```csharp
        public void OnConfirm()
        {
            this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);
            int max = GetMaxAvailable();
            this.context.SelectedShotCount = Mathf.Clamp(this.shotCountView.Value, 1, max);
            this.shotCountView.Hide();

            if (this.context.FocusFireParticipants.Length > 0)
            {
                this.context.FocusFireShotCounts[this.context.SelectedOperator] = this.context.SelectedShotCount;

                int nextIndex = this.context.FocusFireParticipantIndex + 1;
                if (nextIndex < this.context.FocusFireParticipants.Length)
                {
                    this.context.FocusFireParticipantIndex = nextIndex;
                    this.context.SelectedOperator          = this.context.FocusFireParticipants[nextIndex];
                    this.context.TransitionTo(this);
                    return;
                }

                this.context.TransitionTo(this.context.TargetSelState);
                return;
            }

            int[] enemies = this.battlefieldView.GetOccupiedEnemySlots();
            if (enemies.Length == 0)
            {
                int op = this.context.SelectedOperator;
                WeaponData? weaponData = this.roster.Count > op ? (this.roster[op].ActiveWeapon as WeaponItem)?.Data : null;
                this.aimView.ConfigureWeapon(weaponData);
                this.aimView.ConfigureHitMask(null);
                this.aimView.SetShotCount(this.context.SelectedShotCount);
                this.context.TransitionTo(this.context.AimingState);
                return;
            }

            this.context.TransitionTo(this.context.TargetSelState);
        }
```

(`GetMaxAvailable()` already reads `this.context.SelectedOperator`, which is updated *before* `TransitionTo(this)` re-enters and calls it again — so the shot-count cap is correctly per-participant.)

- [ ] **Step 7: Run tests to verify they pass**

Expected: both new tests PASS; every pre-existing test — including the solo-Shoot `ShotCountSelectionState` tests, which take the untouched `FocusFireParticipants.Length == 0` branch — keeps passing unchanged.

- [ ] **Step 8: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/States/ShotCountSelectionState.cs \
        Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat): loop shot-count selection across a Focus Fire group"
```

---

## Task 5: `AimingState` group resolution

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Interfaces:**
- Consumes: `IAimView.ResolveShotsForWeapon` (Task 3), `CombatMenuController.FocusFireParticipants`/`FocusFireShotCounts` (Task 4), `ICombatOrchestrator.NotifyFocusFireCompleted` (Task 2).

This is the last piece: applying every participant's damage/poise, playing every participant's burst sequentially, and finalizing stagger/death exactly once for the whole group.

- [ ] **Step 1: Write the failing test**

Add to `CombatMenuControllerTests.cs`:

```csharp
        [Test]
        public void FocusFireResolution_appliesDamagePerParticipantAndPlaysSequentialBursts()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 1000);
            this.aimView.ResolveShotsForWeaponHandler = (data, count) =>
                new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 15) };

            var c = BuildAndInit();
            c.BeginFocusFireConfiguration(new[] { 0, 1 });

            InvokeConfirm(c); // participant 0's shot count
            InvokeConfirm(c); // participant 1's (trigger) shot count -> TargetSelState

            InvokeConfirm(c); // TargetSelState -> AimingState (only slot 1 is occupied)

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Head, ShotPrecision.Normal, 40) });

            InvokeConfirm(c); // dismiss aim window -> plays both bursts, finalizes

            Assert.AreEqual(2, this.battlefieldView.BurstCallCount);
            Assert.AreEqual(1, this.battlefieldView.LastBurstOperatorSlotIndex); // trigger (participant 1) fires last
            Assert.AreEqual(945, this.battlefieldView.LastDamageResult.RemainingHp); // 1000 - 15 (marked) - 40 (trigger)
            Assert.AreEqual(1, this.orchestrator.NotifyFocusFireCompletedCallCount);
            Assert.AreEqual(0, c.FocusFireParticipants.Length);
        }

        [Test]
        public void FocusFireResolution_resolvesMarkedParticipantsFromAimView()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 1000);

            var c = BuildAndInit();
            c.BeginFocusFireConfiguration(new[] { 0, 1 });

            InvokeConfirm(c);
            InvokeConfirm(c);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Head, ShotPrecision.Normal, 40) });
            InvokeConfirm(c);

            Assert.AreEqual(1, this.aimView.ResolveShotsForWeaponCallCount); // once for the one marked participant
            Assert.AreEqual(1, this.aimView.LastResolvedShotCount);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: compiles but FAILS — `NotifyFocusFireCompletedCallCount` doesn't exist on `FakeOrchestrator` yet (add it now, see Step 3), and the resolution logic itself doesn't branch on the group case yet.

- [ ] **Step 3: Add `NotifyFocusFireCompletedCallCount` to `FakeOrchestrator`**

Next to `NotifyShootCompletedCallCount`:

```csharp
            public int NotifyFocusFireCompletedCallCount { get; private set; }
            public void NotifyFocusFireCompleted()        => this.NotifyFocusFireCompletedCallCount++;
```

- [ ] **Step 4: Add `System.Collections.Generic` to `AimingState.cs`'s usings**

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CrimsonDraft.Audio;
using CrimsonDraft.Operators;
```

- [ ] **Step 5: Branch `HandleShotsResolved` on the group case**

The existing solo-path body stays completely unchanged — only wrap it with an early group branch at the top:

```csharp
        private readonly List<(int Slot, ResolvedShot[] Shots)> pendingGroupShots = new();

        private void HandleShotsResolved(ResolvedShot[] shots)
        {
            this.pendingShots   = shots ?? Array.Empty<ResolvedShot>();
            this.pendingStagger = false;
            this.pendingDeath   = false;

            if (this.context.FocusFireParticipants.Length > 0)
            {
                HandleGroupShotsResolved();
                this.awaitingDismiss = true;
                return;
            }

            int op = this.context.SelectedOperator;
            var weapon = this.roster.Count > op ? this.roster[op].ActiveWeapon : null;
            int weaponPoiseDamage = weapon?.PoiseDamage ?? 0;

            int totalDamage = 0;
            int totalPoiseDamage = 0;
            foreach (var shot in this.pendingShots)
            {
                totalDamage += Mathf.Max(0, shot.Damage);
                if (shot.Zone != ShotZone.Miss)
                    totalPoiseDamage += CombatMenuController.ComputePoiseDamage(shot.Zone, weaponPoiseDamage);
            }

            if (this.context.CurrentTargetSlot >= 0)
            {
                var result = this.battlefieldView.ApplyDamageToEnemy(
                    this.context.CurrentTargetSlot, totalDamage, totalPoiseDamage);
#if UNITY_EDITOR
                Debug.Log(
                    $"[Combat] Enemy slot={this.context.CurrentTargetSlot} bullets={this.context.SelectedShotCount} damage={result.DamageApplied} hp={result.RemainingHp} dead={result.IsDead}");
#endif
                this.pendingStagger = result.IsStaggered;
                this.pendingDeath   = result.IsDead;
            }

            if (weapon != null)
                weapon.SetAmmo(weapon.CurrentAmmo - this.context.SelectedShotCount);

            this.awaitingDismiss = true;
        }

        private void HandleGroupShotsResolved()
        {
            this.pendingGroupShots.Clear();
            int[] participants = this.context.FocusFireParticipants;

            for (int i = 0; i < participants.Length; i++)
            {
                int slot = participants[i];
                var weapon = this.roster.Count > slot ? this.roster[slot].ActiveWeapon : null;
                int weaponPoiseDamage = weapon?.PoiseDamage ?? 0;
                bool isTrigger = i == participants.Length - 1;
                int shotCount = this.context.FocusFireShotCounts.TryGetValue(slot, out int sc) ? sc : 1;

                ResolvedShot[] participantShots = isTrigger
                    ? this.pendingShots
                    : this.aimView.ResolveShotsForWeapon((weapon as WeaponItem)?.Data, shotCount);

                int totalDamage = 0;
                int totalPoiseDamage = 0;
                foreach (var shot in participantShots)
                {
                    totalDamage += Mathf.Max(0, shot.Damage);
                    if (shot.Zone != ShotZone.Miss)
                        totalPoiseDamage += CombatMenuController.ComputePoiseDamage(shot.Zone, weaponPoiseDamage);
                }

                if (this.context.CurrentTargetSlot >= 0)
                {
                    var result = this.battlefieldView.ApplyDamageToEnemy(
                        this.context.CurrentTargetSlot, totalDamage, totalPoiseDamage);
                    this.pendingStagger = result.IsStaggered;
                    this.pendingDeath   = result.IsDead;
                }

                if (weapon != null)
                    weapon.SetAmmo(weapon.CurrentAmmo - shotCount);

                this.pendingGroupShots.Add((slot, participantShots));
            }
        }
```

`WeaponItem` needs `using CrimsonDraft.Inventory;` — check whether `AimingState.cs` already has it (it currently doesn't; `TargetSelectionState.cs`/`ShotCountSelectionState.cs` do). Add it:

```csharp
using CrimsonDraft.Inventory;
```

- [ ] **Step 6: Branch `CloseAimAndReturnToOperatorSelectionAsync` on the group case**

```csharp
        private async UniTaskVoid CloseAimAndReturnToOperatorSelectionAsync()
        {
            this.awaitingDismiss = false;
            this.aimView.Hide();
            this.commandPanel.Hide();

            bool isGroup = this.context.FocusFireParticipants.Length > 0;

            this.isPlayingBurst = true;
            if (isGroup)
            {
                foreach (var participant in this.pendingGroupShots)
                {
                    await this.battlefieldView.PlayOperatorShootBurstAsync(
                        participant.Slot, this.context.CurrentTargetSlot, participant.Shots);
                }
            }
            else
            {
                await this.battlefieldView.PlayOperatorShootBurstAsync(
                    this.context.SelectedOperator,
                    this.context.CurrentTargetSlot,
                    this.pendingShots);
            }
            this.isPlayingBurst = false;

            if (this.pendingDeath)
            {
                this.battlefieldView.FinalizeEnemyDeath(this.context.CurrentTargetSlot);
                this.pendingDeath = false;
            }
            else if (this.pendingStagger)
            {
                this.battlefieldView.TriggerEnemyStagger(this.context.CurrentTargetSlot);
                this.context.Orchestrator.NotifyEnemyStaggered(this.context.CurrentTargetSlot);
                this.pendingStagger = false;
            }

            if (isGroup)
            {
                this.context.Orchestrator.NotifyFocusFireCompleted();
                this.context.FocusFireParticipants = Array.Empty<int>();
                this.context.FocusFireShotCounts.Clear();
            }
            else
            {
                this.context.Orchestrator.NotifyShootCompleted();
            }

            this.context.CurrentTargetSlot = -1;
            this.context.SelectedShotCount = 1;
            this.context.TransitionTo(this.context.OperatorSelState);
        }
```

- [ ] **Step 7: Run tests to verify they pass**

Run the full `CombatMenuControllerTests` suite.
Expected: both new tests PASS, and every pre-existing test (solo Shoot, stagger, death, poise, all from earlier work) keeps passing — the solo path in `HandleShotsResolved`/`CloseAimAndReturnToOperatorSelectionAsync` is untouched aside from the new `isGroup` branch wrapping it.

- [ ] **Step 8: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs \
        Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat): resolve Focus Fire damage and bursts per participant"
```

---

## Manual verification (Play Mode)

Not covered by the automated suite: `CombatOrchestrator`'s queue-handling (Task 2, no dedicated test file) and the full interactive flow through the real `AimViewController`/`CombatActionMenuView` (Task 3/1's visual hooks). After all 5 tasks:

1. Enter a combat encounter with ≥2 alive operators.
2. Select an operator whose ATB is ready, choose **Focus Fire** — confirm their command panel closes, they don't get offered commands again while their gauge would otherwise refill, and (once `operatorFocusFireMarkers` is wired in the Inspector) their marked-visual shows.
3. Repeat marking until only one un-marked operator would be left ready — confirm **Focus Fire** is grayed out/disabled for that last one, and **Shoot** still works.
4. Choose **Shoot** on that last operator — confirm the game asks for a shot count **once per marked operator plus the trigger**, in sequence, then a single target selection, then a single aim QTE.
5. Confirm the QTE — confirm each participant's operator animation/burst plays **one after another** (not simultaneously), enemy HP drops by the sum of all participants' damage, and ammo is deducted from each participant's own weapon.
6. If the combined damage staggers or kills the enemy, confirm that resolves exactly once (existing Fall/Death-mark sequencing from the Poise work), not once per participant.
