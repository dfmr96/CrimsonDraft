# Combat QTE UI — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a joystick-navigable combat action menu (Disparar / Cerrar) and a QTE placeholder panel to Combat.unity using UI Toolkit, wired via VContainer.

**Architecture:** Two separate UIDocuments in Combat.unity — `CombatActionMenuDocument` (always visible) and `QTEDocument` (starts disabled). A `CombatMenuController` service registered in `CombatScope` subscribes to menu button events and shows/hides the QTE panel. Controller takes interfaces for testability; MonoBehaviours implement those interfaces. Navigation.unity's EventSystem handles input (Combat loads additively on top).

**Tech Stack:** Unity UI Toolkit (UXML/USS), VContainer, InputSystemUIInputModule, NUnit (Unity Test Runner EditMode)

---

### Task 1: Create PanelSettings asset (manual, Unity Editor)

This is a binary Unity asset — must be created inside the Editor, not as a text file.

**Files:**
- Create: `Game/CrimsonDraft/Assets/Art/UI/CombatPanelSettings.asset`

**Step 1: Open Unity Editor and navigate to Assets/Art/UI/**
The folder `Assets/Art/UI/` may not exist yet. In the Project panel, navigate to `Assets/Art/` and right-click → Create → Folder → name it `UI`.

**Step 2: Create PanelSettings**
Right-click inside `Assets/Art/UI/` → Create → UI Toolkit → Panel Settings.
Name it `CombatPanelSettings`.

**Step 3: Configure CombatPanelSettings in Inspector**
Select the asset. Set:
- Scale Mode: `Scale With Screen Size`
- Reference Resolution: `1920` × `1080`
- Screen Match Mode: `Match Width Or Height`, slider at `0.5`

**Step 4: Commit**
```bash
git add "Game/CrimsonDraft/Assets/Art/UI/"
git commit -m "feat: add CombatPanelSettings for UI Toolkit combat UI"
```

---

### Task 2: Create CombatActionMenu UXML + USS

**Files:**
- Create: `Game/CrimsonDraft/Assets/Art/UI/CombatActionMenu.uxml`
- Create: `Game/CrimsonDraft/Assets/Art/UI/CombatActionMenu.uss`

**Step 1: Create CombatActionMenu.uxml**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="CombatActionMenu.uss" />
    <ui:VisualElement name="root" class="action-menu-root">
        <ui:VisualElement name="action-menu" class="action-menu">
            <ui:Button name="btn-disparar" text="DISPARAR" class="action-btn" />
            <ui:Button name="btn-cerrar" text="CERRAR" class="action-btn" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

**Step 2: Create CombatActionMenu.uss**

```css
.action-menu-root {
    width: 100%;
    height: 100%;
    justify-content: flex-end;
    align-items: center;
    padding-bottom: 24px;
}

.action-menu {
    flex-direction: row;
    gap: 12px;
}

.action-btn {
    width: 140px;
    height: 44px;
    background-color: rgba(0, 0, 0, 180);
    color: rgb(220, 220, 220);
    border-color: rgb(80, 80, 80);
    border-width: 1px;
    border-radius: 4px;
    font-size: 13px;
    -unity-font-style: bold;
    -unity-text-align: middle-center;
}

.action-btn:hover {
    background-color: rgba(160, 20, 20, 150);
    border-color: rgb(180, 20, 20);
    color: rgb(255, 255, 255);
}

.action-btn:focus {
    background-color: rgba(160, 20, 20, 150);
    border-color: rgb(220, 50, 50);
    color: rgb(255, 255, 255);
}
```

**Step 3: Commit**
```bash
git add "Game/CrimsonDraft/Assets/Art/UI/CombatActionMenu.uxml"
git add "Game/CrimsonDraft/Assets/Art/UI/CombatActionMenu.uxml.meta"
git add "Game/CrimsonDraft/Assets/Art/UI/CombatActionMenu.uss"
git add "Game/CrimsonDraft/Assets/Art/UI/CombatActionMenu.uss.meta"
git commit -m "feat: add CombatActionMenu UXML and USS"
```

---

### Task 3: Create QTEPanel UXML + USS

**Files:**
- Create: `Game/CrimsonDraft/Assets/Art/UI/QTEPanel.uxml`
- Create: `Game/CrimsonDraft/Assets/Art/UI/QTEPanel.uss`

**Step 1: Create QTEPanel.uxml**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="QTEPanel.uss" />
    <ui:VisualElement name="root" class="qte-root">
        <ui:VisualElement name="qte-panel" class="qte-panel">
            <ui:Label name="qte-placeholder" text="[ QTE ]" class="qte-placeholder" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

**Step 2: Create QTEPanel.uss**

```css
.qte-root {
    width: 100%;
    height: 100%;
    align-items: center;
    justify-content: center;
}

.qte-panel {
    width: 320px;
    height: 200px;
    background-color: rgba(10, 10, 10, 230);
    border-color: rgb(180, 20, 20);
    border-width: 2px;
    border-radius: 6px;
    align-items: center;
    justify-content: center;
}

.qte-placeholder {
    color: rgb(180, 180, 180);
    font-size: 16px;
    -unity-font-style: italic;
    -unity-text-align: middle-center;
}
```

**Step 3: Commit**
```bash
git add "Game/CrimsonDraft/Assets/Art/UI/QTEPanel.uxml"
git add "Game/CrimsonDraft/Assets/Art/UI/QTEPanel.uxml.meta"
git add "Game/CrimsonDraft/Assets/Art/UI/QTEPanel.uss"
git add "Game/CrimsonDraft/Assets/Art/UI/QTEPanel.uss.meta"
git commit -m "feat: add QTEPanel UXML and USS placeholder"
```

---

### Task 4: Create ICombatActionMenuView and IQTEView interfaces

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/ICombatActionMenuView.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IQTEView.cs`

These scripts live in a subfolder of `Assets/Scripts/Combat/`, so they automatically belong to the `CrimsonDraft.Combat` assembly — no new `.asmdef` needed.

**Step 1: Create ICombatActionMenuView.cs**

```csharp
#nullable enable

using System;

namespace CrimsonDraft.Combat
{
    public interface ICombatActionMenuView
    {
        event Action? OnDisparar;
        event Action? OnCerrar;
    }
}
```

**Step 2: Create IQTEView.cs**

```csharp
#nullable enable

namespace CrimsonDraft.Combat
{
    public interface IQTEView
    {
        void Show();
        void Hide();
    }
}
```

**Step 3: Compile check**
Open Unity Editor and wait for it to compile. There should be no errors. If there are, fix them before continuing.

**Step 4: Commit**
```bash
git add "Game/CrimsonDraft/Assets/Scripts/Combat/UI/ICombatActionMenuView.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Combat/UI/ICombatActionMenuView.cs.meta"
git add "Game/CrimsonDraft/Assets/Scripts/Combat/UI/IQTEView.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Combat/UI/IQTEView.cs.meta"
git commit -m "feat: add ICombatActionMenuView and IQTEView interfaces"
```

---

### Task 5: Create CombatActionMenuView MonoBehaviour

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatActionMenuView.cs`

**Step 1: Create CombatActionMenuView.cs**

```csharp
#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrimsonDraft.Combat
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class CombatActionMenuView : MonoBehaviour, ICombatActionMenuView
    {
        public event Action? OnDisparar;
        public event Action? OnCerrar;

        private Button btnDisparar = null!;
        private Button btnCerrar = null!;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            this.btnDisparar = root.Q<Button>("btn-disparar");
            this.btnCerrar = root.Q<Button>("btn-cerrar");

            this.btnDisparar.clicked += this.HandleDisparar;
            this.btnCerrar.clicked += this.HandleCerrar;

            this.btnDisparar.Focus();
        }

        private void OnDisable()
        {
            this.btnDisparar.clicked -= this.HandleDisparar;
            this.btnCerrar.clicked -= this.HandleCerrar;
        }

        private void HandleDisparar() => this.OnDisparar?.Invoke();
        private void HandleCerrar() => this.OnCerrar?.Invoke();
    }
}
```

**Step 2: Compile check**
Wait for Unity to compile. No errors expected.

**Step 3: Commit**
```bash
git add "Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatActionMenuView.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatActionMenuView.cs.meta"
git commit -m "feat: add CombatActionMenuView MonoBehaviour"
```

---

### Task 6: Create QTEView MonoBehaviour

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/QTEView.cs`

**Step 1: Create QTEView.cs**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    public sealed class QTEView : MonoBehaviour, IQTEView
    {
        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
```

**Step 2: Compile check**
Wait for Unity to compile. No errors expected.

**Step 3: Commit**
```bash
git add "Game/CrimsonDraft/Assets/Scripts/Combat/UI/QTEView.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Combat/UI/QTEView.cs.meta"
git commit -m "feat: add QTEView MonoBehaviour"
```

---

### Task 7: Write failing tests for CombatMenuController (TDD)

The test asmdef at `Assets/Tests/EditMode/CrimsonDraft.Tests.EditMode.asmdef` already references `CrimsonDraft.Combat`, so test files placed in that folder can use our interfaces directly.

**Files:**
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Step 1: Write the failing tests**

```csharp
using System;
using NUnit.Framework;
using VContainer.Unity;

namespace CrimsonDraft.Tests
{
    public sealed class CombatMenuControllerTests
    {
        private FakeCombatActionMenuView menuView = null!;
        private FakeQTEView qteView = null!;

        [SetUp]
        public void SetUp()
        {
            this.menuView = new FakeCombatActionMenuView();
            this.qteView = new FakeQTEView();
        }

        [Test]
        public void QTEPanel_StartsHidden()
        {
            IInitializable controller = new CrimsonDraft.Combat.CombatMenuController(this.menuView, this.qteView);
            controller.Initialize();

            Assert.IsFalse(this.qteView.IsVisible);
        }

        [Test]
        public void DisparasEvent_ShowsQTEPanel()
        {
            IInitializable controller = new CrimsonDraft.Combat.CombatMenuController(this.menuView, this.qteView);
            controller.Initialize();

            this.menuView.RaiseOnDisparar();

            Assert.IsTrue(this.qteView.IsVisible);
        }

        [Test]
        public void CerrarEvent_HidesQTEPanel()
        {
            IInitializable controller = new CrimsonDraft.Combat.CombatMenuController(this.menuView, this.qteView);
            controller.Initialize();
            this.menuView.RaiseOnDisparar();

            this.menuView.RaiseOnCerrar();

            Assert.IsFalse(this.qteView.IsVisible);
        }

        [Test]
        public void AfterDispose_EventsNoLongerTriggerView()
        {
            var controller = new CrimsonDraft.Combat.CombatMenuController(this.menuView, this.qteView);
            ((IInitializable)controller).Initialize();
            ((IDisposable)controller).Dispose();

            this.menuView.RaiseOnDisparar();

            Assert.IsFalse(this.qteView.IsVisible);
        }

        private sealed class FakeCombatActionMenuView : CrimsonDraft.Combat.ICombatActionMenuView
        {
            public event Action? OnDisparar;
            public event Action? OnCerrar;
            public void RaiseOnDisparar() => OnDisparar?.Invoke();
            public void RaiseOnCerrar() => OnCerrar?.Invoke();
        }

        private sealed class FakeQTEView : CrimsonDraft.Combat.IQTEView
        {
            public bool IsVisible { get; private set; }
            public void Show() => IsVisible = true;
            public void Hide() => IsVisible = false;
        }
    }
}
```

**Step 2: Run tests — expect FAIL**
Open Unity: Window → General → Test Runner → EditMode tab.
Run `CombatMenuControllerTests`. Expected result: **all 4 tests FAIL** with `CS0246: The type or namespace 'CombatMenuController' could not be found`. This confirms the tests are wired correctly and the implementation is missing.

**Step 3: Commit the failing tests**
```bash
git add "Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs.meta"
git commit -m "test: add failing tests for CombatMenuController (TDD)"
```

---

### Task 8: Implement CombatMenuController

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs`

**Step 1: Write the minimal implementation to pass the tests**

```csharp
#nullable enable

using System;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace CrimsonDraft.Combat
{
    public sealed class CombatMenuController : IInitializable, IDisposable
    {
        #region Dependency Injection

        private readonly ICombatActionMenuView menuView;
        private readonly IQTEView qteView;

        [Preserve]
        public CombatMenuController(ICombatActionMenuView menuView, IQTEView qteView)
        {
            this.menuView = menuView;
            this.qteView = qteView;
        }

        #endregion

        #region IInitializable

        void IInitializable.Initialize()
        {
            this.menuView.OnDisparar += this.HandleDisparar;
            this.menuView.OnCerrar += this.HandleCerrar;
        }

        #endregion

        #region IDisposable

        void IDisposable.Dispose()
        {
            this.menuView.OnDisparar -= this.HandleDisparar;
            this.menuView.OnCerrar -= this.HandleCerrar;
        }

        #endregion

        #region Handlers

        private void HandleDisparar() => this.qteView.Show();
        private void HandleCerrar() => this.qteView.Hide();

        #endregion
    }
}
```

**Step 2: Run tests — expect PASS**
In Test Runner → EditMode: run `CombatMenuControllerTests`.
Expected: **all 4 tests PASS** (green).

If any fail, read the assertion message and fix the logic before continuing.

**Step 3: Commit**
```bash
git add "Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs.meta"
git commit -m "feat: implement CombatMenuController (all tests pass)"
```

---

### Task 9: Register in CombatScope

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs`

**Step 1: Read the current file**
Current content of `CombatScope.cs`:
```csharp
protected override void Configure(IContainerBuilder builder)
{
    builder.Register<CombatSessionController>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();
}
```

**Step 2: Add the three new registrations**

Replace the `Configure` method body with:

```csharp
protected override void Configure(IContainerBuilder builder)
{
    builder.Register<CombatSessionController>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();

    builder.RegisterComponentInHierarchy<CombatActionMenuView>().AsImplementedInterfaces();
    builder.RegisterComponentInHierarchy<QTEView>().AsImplementedInterfaces();
    builder.Register<CombatMenuController>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();
}
```

**Why `.AsImplementedInterfaces()`:**
- `CombatActionMenuView` → registered as `ICombatActionMenuView` so `CombatMenuController` can inject it via interface
- `QTEView` → registered as `IQTEView` for the same reason
- `CombatMenuController` → registered as `IInitializable` and `IDisposable` so VContainer calls `Initialize()` on scene load and `Dispose()` on scene unload

**Step 3: Compile check**
Wait for Unity to compile. If you see `VContainer resolution error` at runtime it means a component is missing from the scene — that's fixed in Task 10, not here.

**Step 4: Commit**
```bash
git add "Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs"
git commit -m "feat: register CombatActionMenuView, QTEView, CombatMenuController in CombatScope"
```

---

### Task 10: Configure the Combat scene (manual, Unity Editor)

This task sets up the GameObjects, prefabs, and verifies the EventSystem. All steps are done inside Unity Editor.

**Step 1: Verify EventSystem has InputSystemUIInputModule**
Open `Navigation.unity`. Find the EventSystem GameObject. In Inspector, check which input module is attached:
- If it shows `Input System UI Input Module` → OK, nothing to do.
- If it shows `Standalone Input Module` → Remove it and add `Input System UI Input Module` (Add Component → Event → Input System UI Input Module).

Save `Navigation.unity`.

**Step 2: Open Combat.unity**
File → Open Scene (additive is fine) or double-click `Assets/Scenes/Combat.unity`.

**Step 3: Create CombatActionMenuDocument GameObject**
In the Hierarchy:
1. Right-click → Create Empty → rename to `CombatActionMenuDocument`
2. Add Component → `UI Document`
3. In the UIDocument Inspector:
   - Panel Settings: assign `CombatPanelSettings`
   - Source Asset: assign `CombatActionMenu.uxml`
   - Sort Order: `0`
4. Add Component → `CombatActionMenuView` (search for it in Add Component)

**Step 4: Create QTEDocument GameObject**
1. Right-click → Create Empty → rename to `QTEDocument`
2. Add Component → `UI Document`
3. In the UIDocument Inspector:
   - Panel Settings: assign `CombatPanelSettings`
   - Source Asset: assign `QTEPanel.uxml`
   - Sort Order: `10` (renders on top of the action menu)
4. Add Component → `QTEView`
5. **In the Hierarchy, disable the `QTEDocument` GameObject** (uncheck the checkbox next to its name). It must start inactive.

**Step 5: Assign the CombatScope parent**
The `CombatScope` LifetimeScope needs to find both components via `RegisterComponentInHierarchy`. Ensure both `CombatActionMenuDocument` and `QTEDocument` are children (or descendants) of the GameObject that has `CombatScope` — OR that they are anywhere in the scene hierarchy (VContainer searches the entire scene with `RegisterComponentInHierarchy`).

If `CombatScope` is on a separate root GameObject, the components just need to be in the same scene. No parenting required.

**Step 6: Save prefabs**
1. Drag `CombatActionMenuDocument` from Hierarchy into `Assets/Prefabs/UI/` → create prefab
2. Drag `QTEDocument` from Hierarchy into `Assets/Prefabs/UI/` → create prefab

**Step 7: Save the scene**
Ctrl+S to save `Combat.unity`.

**Step 8: Play test**
Press Play in the Editor. The Navigation scene loads, then trigger combat (walk into a CombatTrigger). Expected behavior:
- Two buttons appear at the bottom of the screen: `DISPARAR` and `CERRAR`
- Press arrow keys or D-pad to navigate between them (focus highlight switches)
- Press Enter/South button on `DISPARAR` → gray QTE panel appears in the center
- Press Enter/South on `CERRAR` → QTE panel disappears

If buttons don't respond to keyboard/gamepad:
- Check that the EventSystem is active and has `Input System UI Input Module`
- Check that the Input Action Asset on `Input System UI Input Module` has a Submit action bound (default Unity asset includes it)

**Step 9: Commit the scene and prefabs**
```bash
git add "Game/CrimsonDraft/Assets/Scenes/Combat.unity"
git add "Game/CrimsonDraft/Assets/Scenes/Navigation.unity"
git add "Game/CrimsonDraft/Assets/Prefabs/UI/"
git commit -m "feat: add combat action menu and QTE panel UIDocuments to Combat scene"
```

---

## Verification Checklist

- [ ] All 4 `CombatMenuControllerTests` pass in Test Runner (EditMode)
- [ ] Compile: zero errors in Unity Console
- [ ] Runtime: action menu visible when combat loads
- [ ] Runtime: joystick/keyboard navigation highlights buttons
- [ ] Runtime: DISPARAR → QTE panel appears, menu stays visible
- [ ] Runtime: CERRAR → QTE panel disappears
- [ ] No errors in Unity Console during play
