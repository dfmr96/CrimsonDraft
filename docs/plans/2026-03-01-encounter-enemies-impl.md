# Encounter Enemies & Battlefield Layout Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Pass enemy and operator data from a combat encounter to the Combat scene, render sprites in their battlefield slots, add a linked operator indicator, and enable enemy target selection before the aim QTE.

**Architecture:** `EncounterContext` (singleton, `GameLifetimeScope`) stores the active encounter ID as a plain string — no Combat assembly reference needed. `SceneTransitionService` populates it before loading the scene. `CombatScope` registers `EncounterDatabase` (SO) as instance and a new `BattlefieldPresenter` that looks up the encounter, then calls `BattlefieldView.Populate()`. `CombatMenuController` gains direct access to `IBattlefieldView` for indicator calls and a new `TargetSelection` state between `CommandPanel` and `Aiming`.

**Tech Stack:** Unity 2D, VContainer (parent-scope injection), ScriptableObjects, MessagePipe, C# 9+ nullable, UniTask, DOTween.

---

### Task 1: ScriptableObject data model

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/EnemyData.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/OperatorData.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/EncounterData.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/EncounterDatabase.cs`

**Step 1: Create EnemyData.cs**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "CrimsonDraft/Combat/Enemy Data")]
    public sealed class EnemyData : ScriptableObject
    {
        [SerializeField] private string enemyId = string.Empty;
        [SerializeField] private Sprite sprite  = null!;

        public string EnemyId => this.enemyId;
        public Sprite Sprite   => this.sprite;
    }
}
```

**Step 2: Create OperatorData.cs**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    [CreateAssetMenu(fileName = "OperatorData", menuName = "CrimsonDraft/Combat/Operator Data")]
    public sealed class OperatorData : ScriptableObject
    {
        [SerializeField] private string operatorId = string.Empty;
        [SerializeField] private Sprite sprite     = null!;

        public string OperatorId => this.operatorId;
        public Sprite Sprite     => this.sprite;
    }
}
```

**Step 3: Create EncounterData.cs**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    [CreateAssetMenu(fileName = "EncounterData", menuName = "CrimsonDraft/Combat/Encounter Data")]
    public sealed class EncounterData : ScriptableObject
    {
        [SerializeField] private string       encounterId = string.Empty;
        [SerializeField] private EnemyData?[] enemySlots  = new EnemyData?[6];
        [SerializeField] private OperatorData?[] operators = new OperatorData?[4];

        public string          EncounterId => this.encounterId;
        public EnemyData?[]    EnemySlots  => this.enemySlots;
        public OperatorData?[] Operators   => this.operators;
    }
}
```

**Step 4: Create EncounterDatabase.cs**

```csharp
#nullable enable

using System;
using System.Linq;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    [CreateAssetMenu(fileName = "EncounterDatabase", menuName = "CrimsonDraft/Combat/Encounter Database")]
    public sealed class EncounterDatabase : ScriptableObject
    {
        [SerializeField] private EncounterData[] encounters = Array.Empty<EncounterData>();

        public EncounterData? GetById(string encounterId) =>
            this.encounters.FirstOrDefault(e => e.EncounterId == encounterId);
    }
}
```

**Step 5: Verify compilation**

Open Unity. Check the Console for any compilation errors. Expected: no errors.

**Step 6: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/
git commit -m "feat(combat): add EnemyData, OperatorData, EncounterData, EncounterDatabase SOs"
```

---

### Task 2: IEncounterContext + EncounterContext

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/IEncounterContext.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/EncounterContext.cs`

These live in `CrimsonDraft.Infrastructure` — they only hold a `string`, no reference to Combat types.

**Step 1: Create IEncounterContext.cs**

```csharp
#nullable enable

namespace CrimsonDraft.Infrastructure.Scenes
{
    public interface IEncounterContext
    {
        string? CurrentEncounterId { get; }
    }
}
```

**Step 2: Create EncounterContext.cs**

```csharp
#nullable enable

using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public sealed class EncounterContext : IEncounterContext
    {
        public string? CurrentEncounterId { get; private set; }

        [Preserve]
        public EncounterContext() { }

        public void Set(string encounterId) => this.CurrentEncounterId = encounterId;
    }
}
```

**Step 3: Verify compilation — no errors.**

**Step 4: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/IEncounterContext.cs
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/EncounterContext.cs
git commit -m "feat(combat): add IEncounterContext and EncounterContext singleton"
```

---

### Task 3: Register EncounterContext in GameLifetimeScope

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs`

**Step 1: Add registration**

In `Configure()`, after the `SceneTransitionService` line, add:

```csharp
builder.Register<EncounterContext>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();
```

The full `Configure()` method after the change:

```csharp
protected override void Configure(IContainerBuilder builder)
{
    if (this.inputActions == null)
        throw new System.InvalidOperationException(
            $"{nameof(this.inputActions)} is not assigned in {nameof(GameLifetimeScope)}.");

    builder.RegisterInstance(this.inputActions);
    builder.Register<InputService>(Lifetime.Singleton).AsImplementedInterfaces();

    var options = builder.RegisterMessagePipe();
    builder.RegisterMessageBroker<CombatStartedEvent>(options);
    builder.RegisterMessageBroker<CombatEndedEvent>(options);

    builder.Register<SceneTransitionService>(Lifetime.Singleton).AsImplementedInterfaces();
    builder.Register<EncounterContext>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();
}
```

**Step 2: Verify compilation — no errors.**

**Step 3: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/GameLifetimeScope.cs
git commit -m "feat(combat): register EncounterContext singleton in GameLifetimeScope"
```

---

### Task 4: Update SceneTransitionService to populate EncounterContext

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/SceneTransitionService.cs`

**Step 1: Add EncounterContext field and constructor parameter**

Add to fields:
```csharp
private readonly EncounterContext encounterContext;
```

Update constructor:
```csharp
[Preserve]
public SceneTransitionService(
    IInputService inputService,
    ISubscriber<CombatEndedEvent> combatEndedSubscriber,
    EncounterContext encounterContext)
{
    this.inputService        = inputService;
    this.combatEndedSubscriber = combatEndedSubscriber;
    this.encounterContext    = encounterContext;
}
```

**Step 2: Call Set() before loading the scene**

In `StartCombatAsync`, add the context call before the scene load:

```csharp
public async UniTask StartCombatAsync(string encounterId)
{
    if (this.isInCombat)
        return;

    this.isInCombat = true;
    this.encounterContext.Set(encounterId);
    this.inputService.SwitchToCombat();
    await SceneManager.LoadSceneAsync(CombatSceneName, LoadSceneMode.Additive).ToUniTask();
}
```

**Step 3: Verify compilation — no errors.**

**Step 4: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Scenes/SceneTransitionService.cs
git commit -m "feat(combat): populate EncounterContext before loading Combat scene"
```

---

### Task 5: IBattlefieldView interface + BattlefieldView MonoBehaviour

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs`

**Step 1: Create IBattlefieldView.cs**

```csharp
#nullable enable

namespace CrimsonDraft.Combat
{
    public interface IBattlefieldView
    {
        void Populate(EncounterData encounter);
        void SetOperatorIndicator(int slotIndex);
        void DimOperatorIndicator();
        void SetEnemyTargetIndicator(int slotIndex);
        void HideEnemyTargetIndicator();
        int[] GetOccupiedEnemySlots();
    }
}
```

**Step 2: Create BattlefieldView.cs**

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public sealed class BattlefieldView : MonoBehaviour, IBattlefieldView
    {
        [SerializeField] private Transform[] enemySlotTransforms  = Array.Empty<Transform>();
        [SerializeField] private Transform[] playerSlotTransforms = Array.Empty<Transform>();
        [SerializeField] private GameObject  operatorIndicator    = null!;
        [SerializeField] private GameObject  enemyTargetIndicator = null!;

        private readonly List<GameObject> spawnedSprites = new();
        private int[] occupiedEnemySlots = Array.Empty<int>();

        private void Awake()
        {
            this.operatorIndicator.SetActive(false);
            this.enemyTargetIndicator.SetActive(false);
        }

        public void Populate(EncounterData encounter)
        {
            foreach (var go in this.spawnedSprites)
                Destroy(go);
            this.spawnedSprites.Clear();

            var occupied = new List<int>();
            for (int i = 0; i < encounter.EnemySlots.Length && i < this.enemySlotTransforms.Length; i++)
            {
                var enemy = encounter.EnemySlots[i];
                if (enemy == null) continue;

                occupied.Add(i);
                var go = new GameObject($"Enemy_{i}");
                go.transform.SetParent(this.enemySlotTransforms[i], false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = enemy.Sprite;
                this.spawnedSprites.Add(go);
            }
            this.occupiedEnemySlots = occupied.ToArray();

            for (int i = 0; i < encounter.Operators.Length && i < this.playerSlotTransforms.Length; i++)
            {
                var op = encounter.Operators[i];
                if (op == null) continue;

                var go = new GameObject($"Operator_{i}");
                go.transform.SetParent(this.playerSlotTransforms[i], false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = op.Sprite;
                this.spawnedSprites.Add(go);
            }
        }

        public int[] GetOccupiedEnemySlots() => this.occupiedEnemySlots;

        public void SetOperatorIndicator(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= this.playerSlotTransforms.Length) return;
            this.operatorIndicator.SetActive(true);
            this.operatorIndicator.transform.position = this.playerSlotTransforms[slotIndex].position;
            var sr = this.operatorIndicator.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = Color.white;
        }

        public void DimOperatorIndicator()
        {
            var sr = this.operatorIndicator.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.4f);
        }

        public void SetEnemyTargetIndicator(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= this.enemySlotTransforms.Length) return;
            this.enemyTargetIndicator.SetActive(true);
            this.enemyTargetIndicator.transform.position = this.enemySlotTransforms[slotIndex].position;
        }

        public void HideEnemyTargetIndicator()
        {
            this.enemyTargetIndicator.SetActive(false);
        }
    }
}
```

**Step 3: Verify compilation — no errors.**

**Step 4: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs
git commit -m "feat(combat): add IBattlefieldView interface and BattlefieldView MonoBehaviour"
```

---

### Task 6: BattlefieldPresenter

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/BattlefieldPresenter.cs`

**Step 1: Create BattlefieldPresenter.cs**

```csharp
#nullable enable

using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Scenes;

namespace CrimsonDraft.Combat
{
    public sealed class BattlefieldPresenter : IInitializable
    {
        private readonly IEncounterContext  encounterContext;
        private readonly EncounterDatabase  encounterDatabase;
        private readonly IBattlefieldView   battlefieldView;

        [Preserve]
        public BattlefieldPresenter(
            IEncounterContext encounterContext,
            EncounterDatabase encounterDatabase,
            IBattlefieldView  battlefieldView)
        {
            this.encounterContext  = encounterContext;
            this.encounterDatabase = encounterDatabase;
            this.battlefieldView   = battlefieldView;
        }

        void IInitializable.Initialize()
        {
            var encounterId = this.encounterContext.CurrentEncounterId;
            if (encounterId == null) return;

            var encounter = this.encounterDatabase.GetById(encounterId);
            if (encounter == null) return;

            this.battlefieldView.Populate(encounter);
        }
    }
}
```

**Step 2: Verify compilation — no errors.**

**Step 3: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/BattlefieldPresenter.cs
git commit -m "feat(combat): add BattlefieldPresenter that populates view from EncounterContext"
```

---

### Task 7: Add OnOperatorFocused to ICombatActionMenuView + CombatActionMenuView

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/ICombatActionMenuView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatActionMenuView.cs`

**Step 1: Add OnOperatorFocused to interface**

In `ICombatActionMenuView.cs`, add the new event after `OnOperatorSelected`:

```csharp
public interface ICombatActionMenuView
{
    event Action<int>? OnOperatorSelected;
    event Action<int>? OnOperatorFocused;      // <-- new
    void FocusOperator(int index);
    RectTransform GetOperatorAnchor(int index);
    RectTransform GetOperatorRect(int index);
    void MoveSelectorTo(RectTransform anchor);
    void SetDimmed(bool dimmed);
}
```

**Step 2: Implement OnOperatorFocused in CombatActionMenuView**

In `CombatActionMenuView`, add the event field at the top of the `#region Events` block:

```csharp
public event Action<int>? OnOperatorSelected;
public event Action<int>? OnOperatorFocused;
```

In `OnEnable()`, update `selectedHandlers[i]` so it fires both the visual move and the new event:

```csharp
this.selectedHandlers[i] = () =>
{
    this.MoveSelector(index);
    this.OnOperatorFocused?.Invoke(index);
};
```

**Step 3: Verify compilation — no errors.**

**Step 4: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/ICombatActionMenuView.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatActionMenuView.cs
git commit -m "feat(combat): add OnOperatorFocused event to ICombatActionMenuView"
```

---

### Task 8: Update CombatScope

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs`

**Step 1: Add EncounterDatabase field and register new components**

```csharp
#nullable enable

using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace CrimsonDraft.Combat
{
    public sealed class CombatScope : LifetimeScope
    {
        [SerializeField] private EncounterDatabase encounterDatabase = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<CombatSessionController>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<CombatActionMenuView>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<CommandPanelView>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<SubPanelView>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<AimViewController>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<BattlefieldView>().AsImplementedInterfaces();

            builder.RegisterInstance(this.encounterDatabase);

            builder.Register<BattlefieldPresenter>(Lifetime.Scoped).AsImplementedInterfaces();

            builder.Register<CombatMenuController>(Lifetime.Scoped)
                .AsSelf().AsImplementedInterfaces();
        }
    }
}
```

**Step 2: In the Unity Editor**, select the `CombatScope` GameObject in the `Combat` scene and assign the `EncounterDatabase` asset to the new field in the Inspector.

**Step 3: Verify compilation — no errors.**

**Step 4: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs
git commit -m "feat(combat): register BattlefieldPresenter and BattlefieldView in CombatScope"
```

---

### Task 9: Update CombatMenuController — battlefield indicators + TargetSelection state

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs`

This is the largest change. Replace the full file with the version below (preserves all existing behavior, adds new state and view calls).

**Step 1: Replace CombatMenuController.cs**

```csharp
#nullable enable

using System;
using MessagePipe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Combat
{
    public sealed class CombatMenuController : IInitializable, IDisposable
    {
        #region State

        private enum CombatMenuState { OperatorSelection, CommandPanel, SubPanel, TargetSelection, Aiming }
        private CombatMenuState state           = CombatMenuState.OperatorSelection;
        private int             selectedOperator = 0;
        private int[]           occupiedEnemySlots = Array.Empty<int>();
        private int             enemyTargetCursor   = 0;

        #endregion

        #region Dependency Injection

        private readonly ICombatActionMenuView          menuView;
        private readonly ICommandPanelView              commandPanel;
        private readonly ISubPanelView                  subPanel;
        private readonly IPublisher<CombatEndedEvent>   combatEndedPublisher;
        private readonly IInputService?                 inputService;
        private readonly IAimView                       aimView;
        private readonly IBattlefieldView               battlefieldView;

        [Preserve]
        public CombatMenuController(
            ICombatActionMenuView        menuView,
            ICommandPanelView            commandPanel,
            ISubPanelView                subPanel,
            IPublisher<CombatEndedEvent> combatEndedPublisher,
            IAimView                     aimView,
            IBattlefieldView             battlefieldView,
            IInputService                inputService)
        {
            this.menuView             = menuView;
            this.commandPanel         = commandPanel;
            this.subPanel             = subPanel;
            this.combatEndedPublisher = combatEndedPublisher;
            this.aimView              = aimView;
            this.battlefieldView      = battlefieldView;
            this.inputService         = inputService;
        }

        // Internal constructor for tests (no inputService, no battlefieldView wired to input)
        internal CombatMenuController(
            ICombatActionMenuView        menuView,
            ICommandPanelView            commandPanel,
            ISubPanelView                subPanel,
            IPublisher<CombatEndedEvent> combatEndedPublisher,
            IAimView                     aimView,
            IBattlefieldView             battlefieldView)
        {
            this.menuView             = menuView;
            this.commandPanel         = commandPanel;
            this.subPanel             = subPanel;
            this.combatEndedPublisher = combatEndedPublisher;
            this.aimView              = aimView;
            this.battlefieldView      = battlefieldView;
        }

        #endregion

        #region IInitializable

        void IInitializable.Initialize()
        {
            this.menuView.OnOperatorSelected    += this.HandleOperatorSelected;
            this.menuView.OnOperatorFocused     += this.HandleOperatorFocused;
            this.commandPanel.OnCommandSelected += this.HandleCommandSelected;
            this.commandPanel.OnEntryFocused    += this.menuView.MoveSelectorTo;
            this.subPanel.OnItemSelected        += this.HandleItemSelected;
            this.subPanel.OnEntryFocused        += this.menuView.MoveSelectorTo;

            if (this.inputService != null)
            {
                this.inputService.CombatCancel.performed   += this.OnCancelPerformed;
                this.inputService.CombatConfirm.performed  += this.OnConfirmPerformed;
                this.inputService.CombatNavigate.performed += this.OnNavigatePerformed;
            }
        }

        #endregion

        #region IDisposable

        void IDisposable.Dispose()
        {
            this.menuView.OnOperatorSelected    -= this.HandleOperatorSelected;
            this.menuView.OnOperatorFocused     -= this.HandleOperatorFocused;
            this.commandPanel.OnCommandSelected -= this.HandleCommandSelected;
            this.commandPanel.OnEntryFocused    -= this.menuView.MoveSelectorTo;
            this.subPanel.OnItemSelected        -= this.HandleItemSelected;
            this.subPanel.OnEntryFocused        -= this.menuView.MoveSelectorTo;

            if (this.inputService != null)
            {
                this.inputService.CombatCancel.performed   -= this.OnCancelPerformed;
                this.inputService.CombatConfirm.performed  -= this.OnConfirmPerformed;
                this.inputService.CombatNavigate.performed -= this.OnNavigatePerformed;
            }
        }

        #endregion

        #region Public (testable)

        internal void HandleCancelPressed()
        {
            switch (this.state)
            {
                case CombatMenuState.SubPanel:
                    this.subPanel.Hide();
                    this.commandPanel.SetDimmed(false);
                    this.commandPanel.Focus();
                    this.state = CombatMenuState.CommandPanel;
                    break;

                case CombatMenuState.TargetSelection:
                    this.battlefieldView.HideEnemyTargetIndicator();
                    this.commandPanel.SetDimmed(false);
                    this.commandPanel.Focus();
                    this.state = CombatMenuState.CommandPanel;
                    break;

                case CombatMenuState.CommandPanel:
                    this.commandPanel.Hide();
                    this.menuView.SetDimmed(false);
                    this.battlefieldView.SetOperatorIndicator(this.selectedOperator);
                    this.menuView.FocusOperator(this.selectedOperator);
                    this.state = CombatMenuState.OperatorSelection;
                    break;

                case CombatMenuState.OperatorSelection:
                    this.combatEndedPublisher.Publish(new CombatEndedEvent { Victory = false });
                    break;
            }
        }

        #endregion

        #region Handlers

        private void OnCancelPerformed(InputAction.CallbackContext _) =>
            this.HandleCancelPressed();

        private void OnConfirmPerformed(InputAction.CallbackContext _)
        {
            switch (this.state)
            {
                case CombatMenuState.TargetSelection:
                    this.ConfirmTarget();
                    break;
                case CombatMenuState.Aiming:
                    this.aimView.Confirm();
                    break;
            }
        }

        private void OnNavigatePerformed(InputAction.CallbackContext ctx)
        {
            if (this.state != CombatMenuState.TargetSelection) return;
            var dir = ctx.ReadValue<Vector2>();
            if (dir.x > 0.5f)       this.NavigateTarget(1);
            else if (dir.x < -0.5f) this.NavigateTarget(-1);
        }

        private void HandleOperatorFocused(int index)
        {
            if (this.state != CombatMenuState.OperatorSelection) return;
            this.battlefieldView.SetOperatorIndicator(index);
        }

        private void HandleOperatorSelected(int index)
        {
            this.selectedOperator = index;
            this.commandPanel.Show(this.menuView.GetOperatorRect(index));
            this.menuView.SetDimmed(true);
            this.battlefieldView.DimOperatorIndicator();
            this.state = CombatMenuState.CommandPanel;
        }

        private void HandleCommandSelected(CombatCommand command)
        {
            if (this.state != CombatMenuState.CommandPanel) return;

            if (command == CombatCommand.Shoot)
            {
                this.commandPanel.SetDimmed(true);
                this.menuView.SetDimmed(true);
                this.EnterTargetSelection();
                return;
            }

            this.commandPanel.SetDimmed(true);
            this.subPanel.Show(this.GetItemsFor(command), this.commandPanel.PanelRect);
            this.state = CombatMenuState.SubPanel;
        }

        private void HandleItemSelected(int index) { }

        private void HandleShotFired(Vector2 _)
        {
            this.aimView.OnShotFired -= this.HandleShotFired;
            this.aimView.Hide();
            this.commandPanel.Hide();
            this.menuView.SetDimmed(false);
            this.battlefieldView.SetOperatorIndicator(this.selectedOperator);
            this.menuView.FocusOperator(this.selectedOperator);
            this.state = CombatMenuState.OperatorSelection;
        }

        #endregion

        #region Target Selection

        private void EnterTargetSelection()
        {
            this.occupiedEnemySlots = this.battlefieldView.GetOccupiedEnemySlots();
            if (this.occupiedEnemySlots.Length == 0)
            {
                // No enemies in scene — go straight to aim
                this.aimView.OnShotFired += this.HandleShotFired;
                this.aimView.Show();
                this.state = CombatMenuState.Aiming;
                return;
            }
            this.enemyTargetCursor = 0;
            this.battlefieldView.SetEnemyTargetIndicator(this.occupiedEnemySlots[0]);
            this.state = CombatMenuState.TargetSelection;
        }

        private void NavigateTarget(int delta)
        {
            if (this.occupiedEnemySlots.Length == 0) return;
            this.enemyTargetCursor =
                (this.enemyTargetCursor + delta + this.occupiedEnemySlots.Length) % this.occupiedEnemySlots.Length;
            this.battlefieldView.SetEnemyTargetIndicator(this.occupiedEnemySlots[this.enemyTargetCursor]);
        }

        private void ConfirmTarget()
        {
            this.battlefieldView.HideEnemyTargetIndicator();
            this.aimView.OnShotFired += this.HandleShotFired;
            this.aimView.Show();
            this.state = CombatMenuState.Aiming;
        }

        #endregion

        #region Helpers

        private SubPanelItem[] GetItemsFor(CombatCommand command) => command switch
        {
            CombatCommand.Reload => new[] { new SubPanelItem("9MM FMJ"), new SubPanelItem("9MM RIP") },
            CombatCommand.Items  => new[] { new SubPanelItem("MORPHINE"), new SubPanelItem("BANDAGE") },
            CombatCommand.Defend => new[] { new SubPanelItem("SHIELD") },
            _                    => Array.Empty<SubPanelItem>()
        };

        #endregion
    }
}
```

**Step 2: Verify compilation — no errors.**

**Step 3: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs
git commit -m "feat(combat): add TargetSelection state and battlefield indicator calls to CombatMenuController"
```

---

### Task 10: Create test assets in Unity Editor

**This task is done inside the Unity Editor (no code).**

**Step 1: Create EnemyData assets**

In the Project window, right-click `Assets/Art/Data/` (create folder if needed):
- `Create → CrimsonDraft/Combat/Enemy Data` → name it `Enemy_Grunt`
  - Set `Enemy Id`: `grunt`
  - Assign any temporary sprite (use a built-in Unity sprite if needed)
- Repeat for `Enemy_Heavy` with id `heavy`

**Step 2: Create OperatorData assets**

- `Create → CrimsonDraft/Combat/Operator Data` → `Operator_Alpha`, id `alpha`
- Repeat for `Operator_Bravo` (id `bravo`), `Operator_Charlie` (id `charlie`), `Operator_Delta` (id `delta`)
- Assign placeholder sprites

**Step 3: Create EncounterData asset**

- `Create → CrimsonDraft/Combat/Encounter Data` → `Encounter_Test01`
  - Set `Encounter Id`: `test_01`
  - In `Enemy Slots` (size 6): assign `Enemy_Grunt` to slots 0 and 2, `Enemy_Heavy` to slot 4, leave others null
  - In `Operators` (size 4): assign Alpha→[0], Bravo→[1], Charlie→[2], Delta→[3]

**Step 4: Create EncounterDatabase asset**

- `Create → CrimsonDraft/Combat/Encounter Database` → `EncounterDatabase`
  - Add `Encounter_Test01` to the `Encounters` list

**Step 5: Assign EncounterDatabase to CombatScope**

- Open `Combat.unity`
- Select the GameObject with `CombatScope`
- Drag `EncounterDatabase` asset to the `Encounter Database` field in the Inspector

**Step 6: Set encounterId on CombatTrigger**

- Open `Navigation.unity`
- Select the `CombatTrigger` GameObject
- Set `Encounter Id` field to `test_01`

**Step 7: Commit**

```
git add Game/CrimsonDraft/Assets/Art/Data/
git commit -m "feat(combat): add test encounter and enemy assets"
```

---

### Task 11: Add BattlefieldView to Combat scene

**This task is done inside the Unity Editor.**

**Step 1: Create the BattlefieldView GameObject**

In `Combat.unity`:
- Create an empty GameObject named `Battlefield`
- Add the `BattlefieldView` component to it

**Step 2: Create enemy slot anchors**

Under `Battlefield`, create 6 empty GameObjects named `EnemySlot_0` through `EnemySlot_5`. Position them according to the staggered layout:

```
Layout (world space, example positions):
  EnemySlot_0 (1, 2, 0)   EnemySlot_1 (2, 2, 0)
  EnemySlot_2 (0, 1, 0)   EnemySlot_3 (3, 1, 0)
  EnemySlot_4 (1, 0, 0)   EnemySlot_5 (2, 0, 0)
```

Adjust to match your camera/canvas setup.

**Step 3: Create player slot anchors**

Under `Battlefield`, create 4 empty GameObjects named `PlayerSlot_0` through `PlayerSlot_3`:

```
  PlayerSlot_0 (-4, 2, 0)
  PlayerSlot_1 (-5, 1, 0)   PlayerSlot_2 (-3, 1, 0)
  PlayerSlot_3 (-4, 0, 0)
```

**Step 4: Create indicator GameObjects**

Under `Battlefield`:
- Create `OperatorIndicator` — add a `SpriteRenderer` with a visible sprite (arrow or highlight)
- Create `EnemyTargetIndicator` — similar, different color/sprite

**Step 5: Wire BattlefieldView in the Inspector**

Select the `Battlefield` GameObject, in the `BattlefieldView` component:
- `Enemy Slot Transforms`: assign EnemySlot_0..5 in order
- `Player Slot Transforms`: assign PlayerSlot_0..3 in order
- `Operator Indicator`: assign the `OperatorIndicator` GameObject
- `Enemy Target Indicator`: assign the `EnemyTargetIndicator` GameObject

**Step 6: Save scene and commit**

```
git add Game/CrimsonDraft/Assets/Scenes/Combat.unity
git commit -m "feat(combat): add BattlefieldView with slot anchors and indicators to Combat scene"
```

---

### Task 12: Smoke test

**Step 1: Enter Play Mode in Unity**

Open `Boot.unity` (or use Additive load setup). Enter Play Mode.

**Step 2: Verify expected behavior**

- Walk the player into the CombatTrigger zone
- Combat scene loads: enemy sprites appear at their respective slots (slots 0, 2, 4 occupied); operator sprites appear on the player side
- Navigate operators in the menu: the battlefield indicator moves to the corresponding player slot
- Confirm an operator: indicator dims
- Select Shoot: target indicator appears on the first enemy slot, navigate left/right to cycle through occupied enemy slots (0 → 2 → 4 → 0), confirm to enter aim QTE
- Cancel from TargetSelection: returns to CommandPanel (undimmed)
- Cancel from CommandPanel: returns to OperatorSelection, indicator brightens

**Step 3: Check Console — no errors or exceptions.**

**Step 4: If anything is off, check:**
- `CombatScope.encounterDatabase` is assigned in Inspector
- `BattlefieldView` slot arrays are populated and in the correct order
- `CombatTrigger.encounterId` matches the `EncounterId` in `EncounterData`
- `IEncounterContext` is resolvable from `CombatScope` (it's registered in `GameLifetimeScope`, which is the root parent)
