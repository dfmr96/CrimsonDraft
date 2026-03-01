# Aim Minigame Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement a two-phase aim minigame that activates when the player selects Shoot in the CommandPanel.

**Architecture:** New `IAimView` interface + `AimViewController` MonoBehaviour follow the exact same pattern as `CommandPanelView`/`SubPanelView`. `CombatMenuController` gains an `Aiming` state and a new `IAimView` dependency; it forwards `CombatConfirm` input to the view when in that state.

**Tech Stack:** Unity 6 · C# 9 · VContainer · DOTween · MessagePipe · NUnit

---

## Files at a glance

| Action | Path |
|--------|------|
| Create | `Assets/Scripts/Combat/UI/IAimView.cs` |
| Create | `Assets/Scripts/Combat/UI/AimViewController.cs` |
| Modify | `Assets/Scripts/Combat/UI/CombatMenuController.cs` |
| Modify | `Assets/Scripts/Combat/CombatScope.cs` |
| Modify | `Assets/Tests/EditMode/CombatMenuControllerTests.cs` |
| Manual | Wire `AimViewController` in Inspector + create `ShotMarker` prefab |

---

## Task 1 — IAimView interface

**Files:**
- Create: `Assets/Scripts/Combat/UI/IAimView.cs`

**Step 1: Create the file**

```csharp
#nullable enable
using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface IAimView
    {
        event Action<Vector2>? OnShotFired;
        void Show();
        void Confirm();
        void Hide();
    }
}
```

**Step 2: Verify compilation in Unity console — zero errors.**

---

## Task 2 — Failing tests (TDD)

**Files:**
- Modify: `Assets/Tests/EditMode/CombatMenuControllerTests.cs`

The existing `BuildAndInit()` passes 4 args. After adding `aimView` as a 5th arg and writing tests that reference it, the whole file will not compile until `CombatMenuController` is updated (Task 3). That's the expected TDD "red" state.

**Step 1: Add `FakeAimView` at the bottom of the `CombatMenuControllerTests` class (inside, after the other Fakes)**

```csharp
private sealed class FakeAimView : IAimView
{
    public event Action<Vector2>? OnShotFired;
    public bool IsVisible { get; private set; }
    public void Show()    => this.IsVisible = true;
    public void Confirm() { }
    public void Hide()    => this.IsVisible = false;
    public void FireShot(Vector2 pos) => this.OnShotFired?.Invoke(pos);
}
```

**Step 2: Add the `aimView` field next to the other fakes at the top of the test class**

```csharp
private FakeAimView aimView = null!;
```

**Step 3: Initialize it in `SetUp`**

Add after the last `= new Fake...()` line:
```csharp
this.aimView = new FakeAimView();
```

**Step 4: Update `BuildAndInit()` to pass aimView as 5th argument**

Replace:
```csharp
var controller = new CombatMenuController(
    this.menuView, this.commandPanel, this.subPanel, this.publisher);
```
With:
```csharp
var controller = new CombatMenuController(
    this.menuView, this.commandPanel, this.subPanel, this.publisher, this.aimView);
```

**Step 5: Add 4 new test methods after the existing state-machine tests**

```csharp
[Test]
public void ShootCommand_showsAimView()
{
    BuildAndInit();
    this.menuView.RaiseOnOperatorSelected(0);
    this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
    Assert.IsTrue(this.aimView.IsVisible);
}

[Test]
public void ShootCommand_doesNotShowSubPanel()
{
    BuildAndInit();
    this.menuView.RaiseOnOperatorSelected(0);
    this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
    Assert.IsFalse(this.subPanel.IsVisible);
}

[Test]
public void ShotFired_hidesAimView()
{
    BuildAndInit();
    this.menuView.RaiseOnOperatorSelected(0);
    this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
    this.aimView.FireShot(Vector2.zero);
    Assert.IsFalse(this.aimView.IsVisible);
}

[Test]
public void ShotFired_hidesCommandPanel()
{
    BuildAndInit();
    this.menuView.RaiseOnOperatorSelected(0);
    this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
    this.aimView.FireShot(Vector2.zero);
    Assert.IsFalse(this.commandPanel.IsVisible);
}
```

**Step 6: Check Unity console — expect compilation errors (constructor mismatch). That's correct.**

---

## Task 3 — Update CombatMenuController

**Files:**
- Modify: `Assets/Scripts/Combat/UI/CombatMenuController.cs`

Apply each change in order. The file must compile cleanly after all changes.

**Step 1: Add `Aiming` to the state enum and `IAimView` field**

Replace:
```csharp
private enum CombatMenuState { OperatorSelection, CommandPanel, SubPanel }
```
With:
```csharp
private enum CombatMenuState { OperatorSelection, CommandPanel, SubPanel, Aiming }
```

Add after `private readonly IInputService? inputService;`:
```csharp
private readonly IAimView aimView;
```

**Step 2: Replace the production constructor (6 args now)**

Replace the `[Preserve]` constructor with:
```csharp
[Preserve]
public CombatMenuController(
    ICombatActionMenuView        menuView,
    ICommandPanelView            commandPanel,
    ISubPanelView                subPanel,
    IPublisher<CombatEndedEvent> combatEndedPublisher,
    IAimView                     aimView,
    IInputService                inputService)
{
    this.menuView             = menuView;
    this.commandPanel         = commandPanel;
    this.subPanel             = subPanel;
    this.combatEndedPublisher = combatEndedPublisher;
    this.aimView              = aimView;
    this.inputService         = inputService;
}
```

**Step 3: Replace the internal test constructor (5 args now)**

Replace the `internal` constructor with:
```csharp
internal CombatMenuController(
    ICombatActionMenuView        menuView,
    ICommandPanelView            commandPanel,
    ISubPanelView                subPanel,
    IPublisher<CombatEndedEvent> combatEndedPublisher,
    IAimView                     aimView)
{
    this.menuView             = menuView;
    this.commandPanel         = commandPanel;
    this.subPanel             = subPanel;
    this.combatEndedPublisher = combatEndedPublisher;
    this.aimView              = aimView;
}
```

**Step 4: Subscribe/unsubscribe CombatConfirm in Initialize and Dispose**

In `IInitializable.Initialize()`, replace:
```csharp
if (this.inputService != null)
    this.inputService.CombatCancel.performed += this.OnCancelPerformed;
```
With:
```csharp
if (this.inputService != null)
{
    this.inputService.CombatCancel.performed  += this.OnCancelPerformed;
    this.inputService.CombatConfirm.performed += this.OnConfirmPerformed;
}
```

In `IDisposable.Dispose()`, replace:
```csharp
if (this.inputService != null)
    this.inputService.CombatCancel.performed -= this.OnCancelPerformed;
```
With:
```csharp
if (this.inputService != null)
{
    this.inputService.CombatCancel.performed  -= this.OnCancelPerformed;
    this.inputService.CombatConfirm.performed -= this.OnConfirmPerformed;
}
```

**Step 5: Replace `HandleCommandSelected` to handle Shoot**

Replace:
```csharp
private void HandleCommandSelected(CombatCommand command)
{
    if (command == CombatCommand.Shoot)
        return;

    this.commandPanel.SetDimmed(true);
    this.subPanel.Show(this.GetItemsFor(command), this.commandPanel.PanelRect);
    this.state = CombatMenuState.SubPanel;
}
```
With:
```csharp
private void HandleCommandSelected(CombatCommand command)
{
    if (command == CombatCommand.Shoot)
    {
        this.commandPanel.SetDimmed(true);
        this.aimView.OnShotFired += this.HandleShotFired;
        this.aimView.Show();
        this.state = CombatMenuState.Aiming;
        return;
    }

    this.commandPanel.SetDimmed(true);
    this.subPanel.Show(this.GetItemsFor(command), this.commandPanel.PanelRect);
    this.state = CombatMenuState.SubPanel;
}
```

**Step 6: Add two new handlers in the `#region Handlers` block (after `HandleItemSelected`)**

```csharp
private void OnConfirmPerformed(InputAction.CallbackContext _)
{
    if (this.state == CombatMenuState.Aiming)
        this.aimView.Confirm();
}

private void HandleShotFired(Vector2 _)
{
    this.aimView.OnShotFired -= this.HandleShotFired;
    this.aimView.Hide();
    this.commandPanel.Hide();
    this.menuView.SetDimmed(false);
    this.menuView.FocusOperator(this.selectedOperator);
    this.state = CombatMenuState.OperatorSelection;
}
```

**Step 7: Run all EditMode tests**

Run: Unity Test Runner → EditMode → CrimsonDraft.Tests.EditMode
Expected: **14/14 passed**

If any test fails, read the error carefully before changing anything else.

**Step 8: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/IAimView.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/IAimView.cs.meta
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat-ui): add IAimView interface and Aiming state to CombatMenuController"
```

---

## Task 4 — AimViewController

**Files:**
- Create: `Assets/Scripts/Combat/UI/AimViewController.cs`

**Step 1: Create the file with this exact content**

```csharp
#nullable enable

using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Combat
{
    public sealed class AimViewController : MonoBehaviour, IAimView
    {
        #region Events

        public event Action<Vector2>? OnShotFired;

        #endregion

        #region Fields

        private enum AimPhase { VerticalAiming, HorizontalAiming }

        [SerializeField] private RectTransform verticalSpace      = null!;
        [SerializeField] private Image         verticalSelector   = null!;
        [SerializeField] private RectTransform horizontalSpace    = null!;
        [SerializeField] private Image         horizontalSelector = null!;
        [SerializeField] private RectTransform aimSpace           = null!;
        [SerializeField] private GameObject    shotMarkerPrefab   = null!;
        [SerializeField] private float         speed              = 0.8f;
        [SerializeField] private float         dimmingAlpha       = 0.3f;

        private AimPhase phase;
        private float    confirmedY;

        #endregion

        #region IAimView

        public void Show()
        {
            this.gameObject.SetActive(true);
            this.StartVerticalOscillation();
            this.phase = AimPhase.VerticalAiming;
        }

        public void Confirm()
        {
            if (this.phase == AimPhase.VerticalAiming)
            {
                float halfH      = this.verticalSpace.rect.height / 2f;
                this.confirmedY  = (this.verticalSelector.rectTransform.localPosition.y + halfH) / (halfH * 2f);
                this.verticalSelector.rectTransform.DOKill();
                this.verticalSelector.DOFade(this.dimmingAlpha, 0.15f);
                this.StartHorizontalOscillation();
                this.phase = AimPhase.HorizontalAiming;
            }
            else
            {
                float halfW    = this.horizontalSpace.rect.width / 2f;
                float confirmedX = (this.horizontalSelector.rectTransform.localPosition.x + halfW) / (halfW * 2f);
                this.horizontalSelector.rectTransform.DOKill();
                this.horizontalSelector.DOFade(this.dimmingAlpha, 0.15f);
                this.SpawnMarker(confirmedX, this.confirmedY);
                this.OnShotFired?.Invoke(new Vector2(confirmedX, this.confirmedY));
            }
        }

        public void Hide()
        {
            this.verticalSelector.DOKill();
            this.verticalSelector.rectTransform.DOKill();
            this.horizontalSelector.DOKill();
            this.horizontalSelector.rectTransform.DOKill();
            this.gameObject.SetActive(false);
        }

        #endregion

        #region Private

        private void StartVerticalOscillation()
        {
            float halfH = this.verticalSpace.rect.height / 2f;
            this.verticalSelector.DOKill();
            this.verticalSelector.rectTransform.DOKill();
            this.verticalSelector.DOFade(1f, 0f);
            this.verticalSelector.rectTransform.localPosition = new Vector3(0f, -halfH, 0f);
            this.verticalSelector.rectTransform
                .DOLocalMoveY(halfH, this.speed)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void StartHorizontalOscillation()
        {
            float halfW = this.horizontalSpace.rect.width / 2f;
            this.horizontalSelector.DOKill();
            this.horizontalSelector.rectTransform.DOKill();
            this.horizontalSelector.DOFade(1f, 0f);
            this.horizontalSelector.rectTransform.localPosition = new Vector3(-halfW, 0f, 0f);
            this.horizontalSelector.rectTransform
                .DOLocalMoveX(halfW, this.speed)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void SpawnMarker(float normalizedX, float normalizedY)
        {
            var   r      = this.aimSpace.rect;
            float x      = Mathf.Lerp(r.xMin, r.xMax, normalizedX);
            float y      = Mathf.Lerp(r.yMin, r.yMax, normalizedY);
            var   marker = Instantiate(this.shotMarkerPrefab, this.aimSpace);
            ((RectTransform)marker.transform).localPosition = new Vector3(x, y, 0f);
        }

        #endregion
    }
}
```

**Step 2: Verify compilation in Unity — zero errors.**

---

## Task 5 — Register in CombatScope

**Files:**
- Modify: `Assets/Scripts/Combat/CombatScope.cs`

**Step 1: Add the registration line after the SubPanelView line**

In `Configure()`, add:
```csharp
builder.RegisterComponentInHierarchy<AimViewController>().AsImplementedInterfaces();
```

The full `Configure` method should now look like:
```csharp
protected override void Configure(IContainerBuilder builder)
{
    builder.Register<CombatSessionController>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();

    builder.RegisterComponentInHierarchy<CombatActionMenuView>().AsImplementedInterfaces();
    builder.RegisterComponentInHierarchy<CommandPanelView>().AsImplementedInterfaces();
    builder.RegisterComponentInHierarchy<SubPanelView>().AsImplementedInterfaces();
    builder.RegisterComponentInHierarchy<AimViewController>().AsImplementedInterfaces();

    builder.Register<CombatMenuController>(Lifetime.Scoped)
        .AsSelf().AsImplementedInterfaces();
}
```

**Step 2: Verify compilation in Unity — zero errors.**

**Step 3: Commit**

```
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs.meta
git add Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs
git commit -m "feat(combat-ui): add AimViewController and register in CombatScope"
```

---

## Task 6 — Manual Unity wiring ⚠️ (no code — Editor only)

**Step 1: Create the ShotMarker prefab**
- In the Project window, create a new UI Image GameObject in the scene
- Give it a simple sprite (e.g. a small circle/crosshair — can use Unity's built-in UISprite or any dot texture)
- Set its RectTransform size (e.g. 20×20)
- Drag it to `Assets/Prefabs/UI/` to save it as `ShotMarker.prefab`
- Delete the scene instance

**Step 2: Set AimView inactive in the scene**
- In the Hierarchy, select `Canvas/AimView`
- Uncheck the active checkbox (top of the Inspector) so it starts disabled at runtime

**Step 3: Add AimViewController component to AimView**
- Select `Canvas/AimView` in the Hierarchy
- Click Add Component → search for `AimViewController` → add it

**Step 4: Wire the Inspector fields on AimViewController**

| Field | Drag from Hierarchy |
|-------|---------------------|
| Vertical Space | `Canvas/AimView/QTE/VerticalSpace` |
| Vertical Selector | `Canvas/AimView/QTE/VerticalSpace/VerticalSelector` (Image component) |
| Horizontal Space | `Canvas/AimView/QTE/HortizontalSpace` |
| Horizontal Selector | `Canvas/AimView/QTE/HortizontalSpace/HorizontalSelector` (Image component) |
| Aim Space | `Canvas/AimView/QTE/AimSpace` |
| Shot Marker Prefab | `Assets/Prefabs/UI/ShotMarker.prefab` |
| Speed | 0.8 (default) |
| Dimming Alpha | 0.3 (default) |

**Step 5: Save scene** `Ctrl+S`

**Step 6: Play → select an operator → select Shoot → verify the vertical selector oscillates → press Submit → verify horizontal selector activates → press Submit → verify a marker appears in AimSpace**

**Step 7: Commit**

```
git add Game/CrimsonDraft/Assets/Scenes/Combat.unity
git add Game/CrimsonDraft/Assets/Prefabs/UI/ShotMarker.prefab
git add Game/CrimsonDraft/Assets/Prefabs/UI/ShotMarker.prefab.meta
git commit -m "feat(combat-ui): wire AimViewController in Combat scene and add ShotMarker prefab"
```
