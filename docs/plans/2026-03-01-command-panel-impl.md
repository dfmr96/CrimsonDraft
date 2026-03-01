# Command Panel Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Show a CommandPanel (SHOOT/RELOAD/ITEMS/DEFEND) above the selected operator when Submit is pressed, and a SubPanel with placeholder items when an inventory command is selected.

**Architecture:** `CombatMenuController` owns a 3-state machine (OperatorSelection → CommandPanel → SubPanel). `CommandPanelView` and `SubPanelView` are MonoBehaviours in the Combat scene, disabled by default, shown/hidden via their interfaces. Cancel input is moved entirely from `CombatSessionController` to `CombatMenuController` so panel back-navigation intercepts before combat exit.

**Tech Stack:** Unity uGUI (Selectable / EventSystem), VContainer, MessagePipe, DOTween (existing), NUnit EditMode tests.

---

## Task 1: Data types — CombatCommand + SubPanelItem

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatCommand.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/SubPanelItem.cs`

**Step 1: Create CombatCommand enum**

```csharp
// CombatCommand.cs
#nullable enable
namespace CrimsonDraft.Combat
{
    public enum CombatCommand { Shoot, Reload, Items, Defend }
}
```

**Step 2: Create SubPanelItem record**

```csharp
// SubPanelItem.cs
#nullable enable
namespace CrimsonDraft.Combat
{
    public sealed record SubPanelItem(string Label);
}
```

**Step 3: Compile check — open Unity, confirm no errors in Console**

**Step 4: Commit**
```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatCommand.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/SubPanelItem.cs
git commit -m "feat(combat-ui): add CombatCommand enum and SubPanelItem record"
```

---

## Task 2: Interfaces — ICommandPanelView + ISubPanelView

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/ICommandPanelView.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/ISubPanelView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/ICombatActionMenuView.cs`

**Step 1: Update ICombatActionMenuView**

Add `FocusOperator` (restore EventSystem selection) and `GetOperatorAnchor` (anchor for CommandPanel positioning) — the controller needs both without knowing the concrete type:

```csharp
// ICombatActionMenuView.cs
#nullable enable
using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface ICombatActionMenuView
    {
        event Action<int>? OnOperatorSelected;
        void FocusOperator(int index);
        RectTransform GetOperatorAnchor(int index);
    }
}
```

**Step 2: Create ICommandPanelView**

```csharp
// ICommandPanelView.cs
#nullable enable
using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface ICommandPanelView
    {
        event Action<CombatCommand>? OnCommandSelected;
        RectTransform TopAnchor { get; }
        void Show(RectTransform operatorAnchor);
        void Hide();
    }
}
```

**Step 3: Create ISubPanelView**

```csharp
// ISubPanelView.cs
#nullable enable
using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface ISubPanelView
    {
        event Action<int>? OnItemSelected;
        void Show(SubPanelItem[] items, RectTransform bottomAnchor);
        void Hide();
    }
}
```

**Step 4: Compile check in Unity**

**Step 5: Commit**
```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/ICombatActionMenuView.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/ICommandPanelView.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/ISubPanelView.cs
git commit -m "feat(combat-ui): add ICommandPanelView, ISubPanelView interfaces"
```

---

## Task 3: Implement FocusOperator in CombatActionMenuView

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatActionMenuView.cs`

**Step 1: Add FocusOperator and GetOperatorAnchor**

After the `OnDisable` method, add both interface methods. `GetOperatorAnchor` needs `using UnityEngine;` (already present via `RectTransform`):

```csharp
public void FocusOperator(int index)
{
    if (index >= 0 && index < this.operators.Length)
        EventSystem.current.SetSelectedGameObject(this.operators[index].gameObject);
}

public RectTransform GetOperatorAnchor(int index) =>
    this.operators[index].SelectorAnchor;
```

Also check `ActionMenuItem.cs` — if `SelectorAnchor` is `private`, change it to `public RectTransform SelectorAnchor => this.selectorAnchor;`.

**Step 2: Compile check in Unity**

**Step 3: Commit**
```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatActionMenuView.cs
git commit -m "feat(combat-ui): implement FocusOperator on CombatActionMenuView"
```

---

## Task 4: CommandPanelView MonoBehaviour

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CommandPanelView.cs`

**Step 1: Create the file**

```csharp
// CommandPanelView.cs
#nullable enable

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CrimsonDraft.Combat
{
    public sealed class CommandPanelView : MonoBehaviour, ICommandPanelView
    {
        #region Events

        public event Action<CombatCommand>? OnCommandSelected;

        #endregion

        #region Fields

        [Serializable]
        private sealed class CommandEntry
        {
            public ActionMenuItem item    = null!;
            public CombatCommand  command;
        }

        [SerializeField] private CommandEntry[] entries   = Array.Empty<CommandEntry>();
        [SerializeField] private RectTransform  topAnchor = null!;

        private Action[] submitHandlers = Array.Empty<Action>();

        #endregion

        #region ICommandPanelView

        public RectTransform TopAnchor => this.topAnchor;

        public void Show(RectTransform operatorAnchor)
        {
            var hudRoot   = (RectTransform)this.transform.parent;
            var localPos  = hudRoot.InverseTransformPoint(operatorAnchor.position);
            var panel     = (RectTransform)this.transform;
            var halfWidth = panel.rect.width * 0.5f;
            var clampedX  = Mathf.Clamp(localPos.x, halfWidth, 320f - halfWidth);
            panel.anchoredPosition = new Vector2(clampedX, panel.anchoredPosition.y);

            this.gameObject.SetActive(true);
            SelectFirstNextFrame().Forget();
        }

        public void Hide()
        {
            this.gameObject.SetActive(false);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            this.submitHandlers = new Action[this.entries.Length];
        }

        private void OnEnable()
        {
            for (int i = 0; i < this.entries.Length; i++)
            {
                var capturedCommand       = this.entries[i].command;
                this.submitHandlers[i]    = () => this.OnCommandSelected?.Invoke(capturedCommand);
                this.entries[i].item.OnSubmit += this.submitHandlers[i];
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < this.entries.Length; i++)
            {
                this.entries[i].item.OnSubmit -= this.submitHandlers[i];
            }
        }

        #endregion

        #region Private

        private async UniTaskVoid SelectFirstNextFrame()
        {
            await UniTask.NextFrame();
            if (this.entries.Length > 0)
                EventSystem.current.SetSelectedGameObject(this.entries[0].item.gameObject);
        }

        #endregion
    }
}
```

**Step 2: Compile check in Unity**

**Step 3: Commit**
```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CommandPanelView.cs
git commit -m "feat(combat-ui): add CommandPanelView MonoBehaviour"
```

---

## Task 5: SubPanelView MonoBehaviour

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/SubPanelView.cs`

**Step 1: Create the file**

```csharp
// SubPanelView.cs
#nullable enable

using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CrimsonDraft.Combat
{
    public sealed class SubPanelView : MonoBehaviour, ISubPanelView
    {
        #region Events

        public event Action<int>? OnItemSelected;

        #endregion

        #region Fields

        [Serializable]
        private sealed class SubPanelSlot
        {
            public ActionMenuItem item  = null!;
            public TextMeshProUGUI label = null!;
        }

        [SerializeField] private SubPanelSlot[] slots = Array.Empty<SubPanelSlot>();

        private Action[] submitHandlers = Array.Empty<Action>();

        #endregion

        #region ISubPanelView

        public void Show(SubPanelItem[] items, RectTransform bottomAnchor)
        {
            var hudRoot  = (RectTransform)this.transform.parent;
            var localPos = hudRoot.InverseTransformPoint(bottomAnchor.position);
            var panel    = (RectTransform)this.transform;
            panel.anchoredPosition = new Vector2(localPos.x, localPos.y);

            for (int i = 0; i < this.slots.Length; i++)
            {
                bool active = i < items.Length;
                this.slots[i].item.gameObject.SetActive(active);
                if (active)
                    this.slots[i].label.text = items[i].Label;
            }

            this.gameObject.SetActive(true);
            SelectFirstNextFrame(items.Length).Forget();
        }

        public void Hide()
        {
            this.gameObject.SetActive(false);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            this.submitHandlers = new Action[this.slots.Length];
        }

        private void OnEnable()
        {
            for (int i = 0; i < this.slots.Length; i++)
            {
                int captured            = i;
                this.submitHandlers[i]  = () => this.OnItemSelected?.Invoke(captured);
                this.slots[i].item.OnSubmit += this.submitHandlers[i];
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < this.slots.Length; i++)
            {
                this.slots[i].item.OnSubmit -= this.submitHandlers[i];
            }
        }

        #endregion

        #region Private

        private async UniTaskVoid SelectFirstNextFrame(int count)
        {
            await UniTask.NextFrame();
            if (count > 0)
                EventSystem.current.SetSelectedGameObject(this.slots[0].item.gameObject);
        }

        #endregion
    }
}
```

**Step 2: Compile check in Unity**

**Step 3: Commit**
```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/SubPanelView.cs
git commit -m "feat(combat-ui): add SubPanelView MonoBehaviour"
```

---

## Task 6: Update CombatSessionController — remove Cancel handling

Cancel is now owned by `CombatMenuController`. `CombatSessionController` keeps `EndCombat(bool)` as a public trigger for future use (e.g., all enemies defeated).

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/CombatSessionController.cs`

**Step 1: Remove Cancel subscription, keep EndCombat**

Replace the entire file:

```csharp
#nullable enable

using System;
using MessagePipe;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Combat
{
    public sealed class CombatSessionController : IInitializable, IDisposable
    {
        private readonly IPublisher<CombatEndedEvent> combatEndedPublisher;

        [Preserve]
        public CombatSessionController(IPublisher<CombatEndedEvent> combatEndedPublisher)
        {
            this.combatEndedPublisher = combatEndedPublisher;
        }

        void IInitializable.Initialize() { }

        public void EndCombat(bool victory) =>
            this.combatEndedPublisher.Publish(new CombatEndedEvent { Victory = victory });

        void IDisposable.Dispose() { }
    }
}
```

**Step 2: Compile check in Unity**

**Step 3: Commit**
```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/CombatSessionController.cs
git commit -m "refactor(combat-ui): move Cancel input from CombatSessionController to CombatMenuController"
```

---

## Task 7: Add InternalsVisibleTo for tests

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/AssemblyInfo.cs`

**Step 1: Create AssemblyInfo**

```csharp
// AssemblyInfo.cs
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CrimsonDraft.Tests.EditMode")]
```

This lets the test assembly call `internal` members of `CrimsonDraft.Combat`.

**Step 2: Compile check in Unity**

**Step 3: Commit**
```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/AssemblyInfo.cs
git commit -m "chore(combat): expose internals to EditMode test assembly"
```

---

## Task 8: Update CombatMenuController — state machine (TDD)

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

### Step 1: Write the failing tests first

Replace `CombatMenuControllerTests.cs`:

```csharp
using System;
using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;
using CrimsonDraft.Combat;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Tests
{
    public sealed class CombatMenuControllerTests
    {
        private FakeCombatActionMenuView menuView      = null!;
        private FakeCommandPanelView     commandPanel  = null!;
        private FakeSubPanelView         subPanel      = null!;
        private FakePublisher            publisher     = null!;

        [SetUp]
        public void SetUp()
        {
            this.menuView     = new FakeCombatActionMenuView();
            this.commandPanel = new FakeCommandPanelView();
            this.subPanel     = new FakeSubPanelView();
            this.publisher    = new FakePublisher();
        }

        private CombatMenuController BuildAndInit()
        {
            var controller = new CombatMenuController(
                this.menuView, this.commandPanel, this.subPanel, this.publisher);
            ((IInitializable)controller).Initialize();
            return controller;
        }

        // ── Existing subscription tests ────────────────────────────────

        [Test]
        public void Initialize_subscribesToMenuView()
        {
            BuildAndInit();
            Assert.IsTrue(this.menuView.HasSubscribers);
        }

        [Test]
        public void AfterDispose_unsubscribesFromMenuView()
        {
            var c = BuildAndInit();
            ((IDisposable)c).Dispose();
            Assert.IsFalse(this.menuView.HasSubscribers);
        }

        [Test]
        public void AfterDispose_operatorSelectedEvent_doesNotThrow()
        {
            var c = BuildAndInit();
            ((IDisposable)c).Dispose();
            Assert.DoesNotThrow(() => this.menuView.RaiseOnOperatorSelected(0));
        }

        // ── State machine ──────────────────────────────────────────────

        [Test]
        public void OperatorSelected_showsCommandPanel()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(1);
            Assert.IsTrue(this.commandPanel.IsVisible);
        }

        [Test]
        public void CommandSelected_Reload_showsSubPanel()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Reload);
            Assert.IsTrue(this.subPanel.IsVisible);
        }

        [Test]
        public void CommandSelected_Items_showsSubPanel()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Items);
            Assert.IsTrue(this.subPanel.IsVisible);
        }

        [Test]
        public void CommandSelected_Defend_showsSubPanel()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Defend);
            Assert.IsTrue(this.subPanel.IsVisible);
        }

        [Test]
        public void Cancel_inSubPanel_hidesSubPanel_commandPanelRemains()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Reload);
            c.HandleCancelPressed();
            Assert.IsFalse(this.subPanel.IsVisible);
            Assert.IsTrue(this.commandPanel.IsVisible);
        }

        [Test]
        public void Cancel_inCommandPanel_hidesCommandPanel()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.HandleCancelPressed();
            Assert.IsFalse(this.commandPanel.IsVisible);
        }

        [Test]
        public void Cancel_inOperatorSelection_publishesCombatEndedEvent()
        {
            var c = BuildAndInit();
            c.HandleCancelPressed();
            Assert.IsTrue(this.publisher.Published);
        }

        // ── Fakes ──────────────────────────────────────────────────────

        private sealed class FakeCombatActionMenuView : ICombatActionMenuView
        {
            public event Action<int>? OnOperatorSelected;
            public bool HasSubscribers => this.OnOperatorSelected != null;
            public void RaiseOnOperatorSelected(int index) => this.OnOperatorSelected?.Invoke(index);
            public void FocusOperator(int index) { }
            public RectTransform GetOperatorAnchor(int index) =>
                new GameObject().AddComponent<RectTransform>();
        }

        private sealed class FakeCommandPanelView : ICommandPanelView
        {
            public event Action<CombatCommand>? OnCommandSelected;
            public bool IsVisible { get; private set; }
            private readonly RectTransform topAnchor = new GameObject().AddComponent<RectTransform>();
            public RectTransform TopAnchor => this.topAnchor;
            public void Show(RectTransform _) => this.IsVisible = true;
            public void Hide()                => this.IsVisible = false;
            public void RaiseOnCommandSelected(CombatCommand cmd) => this.OnCommandSelected?.Invoke(cmd);
        }

        private sealed class FakeSubPanelView : ISubPanelView
        {
            public event Action<int>? OnItemSelected;
            public bool IsVisible { get; private set; }
            public void Show(SubPanelItem[] _, RectTransform __) => this.IsVisible = true;
            public void Hide()                                    => this.IsVisible = false;
        }

        private sealed class FakePublisher : MessagePipe.IPublisher<CombatEndedEvent>
        {
            public bool Published { get; private set; }
            public void Publish(CombatEndedEvent message) => this.Published = true;
        }
    }
}
```

### Step 2: Run tests — verify they FAIL

In Unity: **Window → General → Test Runner → EditMode → Run All**

Expected: Multiple failures since `CombatMenuController` doesn't have the new constructor yet.

### Step 3: Implement the new CombatMenuController

Replace `CombatMenuController.cs`:

```csharp
#nullable enable

using System;
using MessagePipe;
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

        private enum CombatMenuState { OperatorSelection, CommandPanel, SubPanel }
        private CombatMenuState state           = CombatMenuState.OperatorSelection;
        private int             selectedOperator = 0;

        #endregion

        #region Dependency Injection

        private readonly ICombatActionMenuView          menuView;
        private readonly ICommandPanelView              commandPanel;
        private readonly ISubPanelView                  subPanel;
        private readonly IPublisher<CombatEndedEvent>   combatEndedPublisher;
        private readonly IInputService?                 inputService;

        [Preserve]
        public CombatMenuController(
            ICombatActionMenuView        menuView,
            ICommandPanelView            commandPanel,
            ISubPanelView                subPanel,
            IPublisher<CombatEndedEvent> combatEndedPublisher,
            IInputService                inputService)
        {
            this.menuView             = menuView;
            this.commandPanel         = commandPanel;
            this.subPanel             = subPanel;
            this.combatEndedPublisher = combatEndedPublisher;
            this.inputService         = inputService;
        }

        // Test constructor — no IInputService (no Unity Input System in EditMode tests)
        internal CombatMenuController(
            ICombatActionMenuView        menuView,
            ICommandPanelView            commandPanel,
            ISubPanelView                subPanel,
            IPublisher<CombatEndedEvent> combatEndedPublisher)
        {
            this.menuView             = menuView;
            this.commandPanel         = commandPanel;
            this.subPanel             = subPanel;
            this.combatEndedPublisher = combatEndedPublisher;
        }

        #endregion

        #region IInitializable

        void IInitializable.Initialize()
        {
            this.menuView.OnOperatorSelected     += this.HandleOperatorSelected;
            this.commandPanel.OnCommandSelected  += this.HandleCommandSelected;
            this.subPanel.OnItemSelected         += this.HandleItemSelected;

            if (this.inputService != null)
                this.inputService.CombatCancel.performed += this.OnCancelPerformed;
        }

        #endregion

        #region IDisposable

        void IDisposable.Dispose()
        {
            this.menuView.OnOperatorSelected     -= this.HandleOperatorSelected;
            this.commandPanel.OnCommandSelected  -= this.HandleCommandSelected;
            this.subPanel.OnItemSelected         -= this.HandleItemSelected;

            if (this.inputService != null)
                this.inputService.CombatCancel.performed -= this.OnCancelPerformed;
        }

        #endregion

        #region Public (testable)

        internal void HandleCancelPressed()
        {
            switch (this.state)
            {
                case CombatMenuState.SubPanel:
                    this.subPanel.Hide();
                    this.state = CombatMenuState.CommandPanel;
                    break;

                case CombatMenuState.CommandPanel:
                    this.commandPanel.Hide();
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

        private void HandleOperatorSelected(int index)
        {
            this.selectedOperator = index;
            this.commandPanel.Show(this.menuView.GetOperatorAnchor(index));
            this.state = CombatMenuState.CommandPanel;
        }

        private void HandleCommandSelected(CombatCommand command)
        {
            if (command == CombatCommand.Shoot)
                return;

            this.subPanel.Show(this.GetItemsFor(command), this.commandPanel.TopAnchor);
            this.state = CombatMenuState.SubPanel;
        }

        private void HandleItemSelected(int index)
        {
            // TODO: apply item to operator when inventory system is implemented
        }

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

**⚠ Note:** `HandleOperatorSelected` calls `view.GetOperatorAnchor(index)` — this method doesn't exist yet on `CombatActionMenuView`. Add it in the next sub-step.

### Step 4: Add GetOperatorAnchor to CombatActionMenuView

In `CombatActionMenuView.cs`, add inside the `#region Private` block:

```csharp
public RectTransform GetOperatorAnchor(int index) =>
    this.operators[index].SelectorAnchor;
```

Also expose `SelectorAnchor` as public on `ActionMenuItem` if it isn't already — check `ActionMenuItem.cs` and add `public RectTransform SelectorAnchor => this.selectorAnchor;` if needed.

### Step 5: Run tests — verify they PASS

**Window → General → Test Runner → EditMode → Run All**

Expected: All tests pass.

### Step 6: Commit
```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatActionMenuView.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat-ui): implement CombatMenuController state machine with tests"
```

---

## Task 9: Update CombatScope

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs`

**Step 1: Register new views**

```csharp
#nullable enable

using VContainer;
using VContainer.Unity;

namespace CrimsonDraft.Combat
{
    public sealed class CombatScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<CombatSessionController>(Lifetime.Scoped)
                .AsSelf().AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<CombatActionMenuView>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<CommandPanelView>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<SubPanelView>().AsImplementedInterfaces();

            builder.Register<CombatMenuController>(Lifetime.Scoped)
                .AsSelf().AsImplementedInterfaces();
        }
    }
}
```

**Step 2: Compile check in Unity**

**Step 3: Commit**
```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs
git commit -m "feat(combat-ui): register CommandPanelView and SubPanelView in CombatScope"
```

---

## Task 10: Create CommandPanel prefab (Unity Editor)

> This task is done manually in the Unity Editor.

**Step 1: In the Project window, navigate to `Assets/Prefabs/UI/`**

**Step 2: Create an empty GameObject, name it `CommandPanel`**

**Step 3: Add components to root:**
- `RectTransform` — set size to `76 × 52`, pivot `(0.5, 0)`, anchor `bottom-center`
- `Image` — background sprite (use same dark background sprite as BottomStrip)

**Step 4: Add a child `TopAnchor` (empty GameObject, RectTransform)**
- Position at top-center of panel: anchoredPosition `(0, 52)`, size `(0, 0)`

**Step 5: Add 4 `ActionMenuItem` children — one per command**

For each child: name `Cmd_Shoot`, `Cmd_Reload`, `Cmd_Items`, `Cmd_Defend`:
- Copy the `OperatorName` structure from `OperatorOverview` prefab as reference
- `ActionMenuItem` component — set `selectorAnchor` to a child `SelectorAnchor` empty
- `TextMeshProUGUI` child showing the command label (SHOOT / RELOAD / ITEMS / DEFEND)
- Navigation: set Explicit, chain Up/Down between the 4 items (wrap: top → bottom)

**Step 6: Add `CommandPanelView` MonoBehaviour to root**
- `topAnchor` → drag the `TopAnchor` child
- `entries[0]` → item=`Cmd_Shoot`, command=`Shoot`
- `entries[1]` → item=`Cmd_Reload`, command=`Reload`
- `entries[2]` → item=`Cmd_Items`, command=`Items`
- `entries[3]` → item=`Cmd_Defend`, command=`Defend`

**Step 7: Save as prefab to `Assets/Prefabs/UI/CommandPanel.prefab`**

**Step 8: Commit**
```bash
git add Game/CrimsonDraft/Assets/Prefabs/UI/CommandPanel.prefab
git add Game/CrimsonDraft/Assets/Prefabs/UI/CommandPanel.prefab.meta
git commit -m "feat(combat-ui): create CommandPanel prefab"
```

---

## Task 11: Create SubPanel prefab (Unity Editor)

> This task is done manually in the Unity Editor.

**Step 1: Navigate to `Assets/Prefabs/UI/`**

**Step 2: Create empty `SubPanel` GameObject**
- `RectTransform` — size `76 × 60` (6 slots × ~10px each), pivot `(0.5, 0)`, anchor `bottom-center`
- `Image` — same dark background sprite

**Step 3: Add 6 slot children** — named `Slot_0` through `Slot_5`

Each slot:
- `RectTransform` — height ~10px, full width
- `ActionMenuItem` component + `SelectorAnchor` child
- `TextMeshProUGUI` child — placeholder text "---"
- Navigation: Explicit Up/Down chained within active slots

**Step 4: Add `SubPanelView` MonoBehaviour to root**
- `slots[0..5]` → wire each slot's `ActionMenuItem` and its `TextMeshProUGUI`

**Step 5: Save as `Assets/Prefabs/UI/SubPanel.prefab`**

**Step 6: Commit**
```bash
git add Game/CrimsonDraft/Assets/Prefabs/UI/SubPanel.prefab
git add Game/CrimsonDraft/Assets/Prefabs/UI/SubPanel.prefab.meta
git commit -m "feat(combat-ui): create SubPanel prefab"
```

---

## Task 12: Wire Combat.unity scene

> Done in the Unity Editor. Open `Assets/Scenes/Combat.unity`.

**Step 1: In the Hierarchy, expand `Canvas → HUDRoot`**

**Step 2: Drag `CommandPanel.prefab` into HUDRoot**
- Set `RectTransform` anchor to `(0.5, 0)` — bottom-center of HUDRoot
- Initial `anchoredPosition.y` = `63` (top of BottomStrip + 3px margin)
- **Disable** the GameObject (Inspector checkbox off)

**Step 3: Drag `SubPanel.prefab` into HUDRoot**
- Same anchor setup
- Initial `anchoredPosition.y` = `63` (will be overridden at runtime by `Show()`)
- **Disable** the GameObject

**Step 4: Verify `CombatActionMenuView` on `HUDRoot`**
- The `operators[0..3]` references should already be wired (from previous commits)

**Step 5: Play the scene (additive load from Navigation)**
- Walk into combat trigger
- Verify operator strip loads, Hand_Selector bobs on first operator
- Navigate L/R — selector moves between operators
- Press Submit — CommandPanel appears above selected operator
- Navigate U/D in CommandPanel
- Press Submit on RELOAD/ITEMS/DEFEND — SubPanel appears with placeholder items
- Press Submit on SHOOT — nothing visible (placeholder)
- Cancel from SubPanel → back to CommandPanel
- Cancel from CommandPanel → back to operator selection, Hand_Selector returns
- Cancel from operator selection → combat ends, scene unloads

**Step 6: Commit**
```bash
git add Game/CrimsonDraft/Assets/Scenes/Combat.unity
git commit -m "feat(combat-ui): wire CommandPanel and SubPanel into Combat scene"
```

---

## Done

The command panel flow is fully implemented with placeholder data. Future tasks:
- Replace `GetItemsFor()` with real inventory queries
- Implement SHOOT → QTEView integration
- Implement `HandleItemSelected()` to apply effects
