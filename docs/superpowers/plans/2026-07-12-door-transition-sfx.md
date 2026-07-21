# Door Transition SFX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Play the `Play_DoorsSC` Wwise event with the `DoorType` switch set to `Open` when the door-opening animation plays, and with the switch set to `Close` whenever the door transition ends (natural completion, timeout fallback, or player skip).

**Architecture:** `DoorTransitionController` (Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/DoorTransitionController.cs) gains three `AK.Wwise` fields and two small methods. The Open sound is triggered by a new Animation Event on the door's opening clip, forwarded through the existing `DoorAnimationRelay` MonoBehaviour. The Close sound is posted once inside `OnAnimationComplete()`, which is already the single choke point reached by all three ways the transition can end.

**Tech Stack:** C# / Unity, Wwise (`AK.Wwise.Event`, `AK.Wwise.Switch`), UnityMCP tools for compilation/console verification (this project has no CLI test runner — see CLAUDE.md).

## Global Constraints

- `#nullable enable` in every file (existing convention).
- No new EditMode tests: this codebase does not unit-test thin Wwise-wiring MonoBehaviours (`OperatorCombatAudio` has none either) because they're tightly coupled to Unity's `Animator`/Wwise runtime. Verification here is compilation + manual Play Mode check, consistent with prior audio work in this repo.
- Do not touch the `Metal`/`Wood` switches, the `DoorsSC` switch container's material routing, or `RoomTransitionContext`/`RoomOrchestrator` — out of scope per the design spec.
- Git: no `Co-Authored-By` trailers (per CLAUDE.md).

---

### Task 1: Wire Open/Close SFX fields and logic into DoorTransitionController

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/DoorTransitionController.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/DoorAnimationRelay.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/CrimsonDraft.Navigation.asmdef`

**Interfaces:**
- Produces: `DoorTransitionController.PlayDoorOpenSfx()` (internal, no params, no return) — called by `DoorAnimationRelay.OnDoorOpenSfx()`.
- Produces: `DoorAnimationRelay.OnDoorOpenSfx()` (public, no params, no return) — target of the new Animation Event added in Task 2.

- [ ] **Step 1: Add the Wwise assembly reference**

`CrimsonDraft.Navigation.asmdef` currently has no reference to Wwise's runtime types, so `AK.Wwise.Event`/`AK.Wwise.Switch` won't resolve. `CrimsonDraft.Audio.asmdef` already references the correct assembly name — reuse it.

Edit `Game/CrimsonDraft/Assets/Scripts/Navigation/CrimsonDraft.Navigation.asmdef`, adding `"AK.Wwise.Unity.API.WwiseTypes"` to the `"references"` array (keep existing entries):

```json
{
    "name": "CrimsonDraft.Navigation",
    "rootNamespace": "CrimsonDraft.Navigation",
    "references": [
        "CrimsonDraft.Infrastructure",
        "CrimsonDraft.Combat",
        "CrimsonDraft.Inventory",
        "CrimsonDraft.Operators",
        "VContainer",
        "VContainer.Unity",
        "UniTask",
        "Unity.InputSystem",
        "Unity.TextMeshPro",
        "YarnSpinner.Unity",
        "Unity.Cinemachine",
        "MessagePipe",
        "MessagePipe.VContainer",
        "DOTween.Modules",
        "NaughtyAttributes.Core",
        "AK.Wwise.Unity.API.WwiseTypes"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [
        "DOTween.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Add the forwarding method to DoorAnimationRelay**

Edit `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/DoorAnimationRelay.cs` to match:

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    // Added at runtime to the Animator's GameObject so Animation Events reach DoorTransitionController.
    public sealed class DoorAnimationRelay : MonoBehaviour
    {
        private DoorTransitionController controller = null!;

        internal void Init(DoorTransitionController controller)
            => this.controller = controller;

        public void OnAnimationComplete() => this.controller.OnAnimationComplete();

        public void OnDoorOpenSfx() => this.controller.PlayDoorOpenSfx();
    }
}
```

- [ ] **Step 3: Add Wwise fields, hasDoor tracking, PlayDoorOpenSfx, and the Close post in OnAnimationComplete**

Edit `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/DoorTransitionController.cs`. Add the three serialized fields next to the existing ones, add a `hasDoor` field, set it in `Start()`, add `PlayDoorOpenSfx()`, and post the Close switch/event inside `OnAnimationComplete()`. The full resulting file:

```csharp
#nullable enable

using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class DoorTransitionController : MonoBehaviour
    {
        [SerializeField] private float        animationTimeout = 5f;
        [SerializeField] private float        fadeInDuration   = 0.8f;
        [SerializeField] private float        fadeOutDuration  = 0.4f;
        [SerializeField] private Ease         fadeInEase       = Ease.InQuad;
        [SerializeField] private Ease         fadeOutEase      = Ease.OutQuad;
        [SerializeField] private CanvasGroup? fadeOverlay;
        [SerializeField] private GameObject?  defaultDoorPrefab;
        [SerializeField] private AK.Wwise.Event  doorEvent   = new();
        [SerializeField] private AK.Wwise.Switch openSwitch  = new();
        [SerializeField] private AK.Wwise.Switch closeSwitch = new();

        private RoomTransitionContext? context;
        private bool                   completed;
        private bool                   canSkip;
        private bool                   hasDoor;

        private void Awake()
        {
            if (this.fadeOverlay != null)
                this.fadeOverlay.alpha = 1f;
        }

        private void OnDestroy()
        {
            if (this.context?.SkipAction != null)
                this.context.SkipAction.performed -= OnSkip;
        }

        private void OnSkip(InputAction.CallbackContext _)
        {
            if (!this.canSkip) return;
            this.canSkip = false;
            DOTween.Kill(this.fadeOverlay);
            OnAnimationComplete();
        }

        private void Start()
        {
            this.context = Resources.Load<RoomTransitionContext>("RoomTransitionContext");

            if (this.context == null)
            {
                Debug.LogError("[DoorTransitionController] RoomTransitionContext not found in Resources.");
                return;
            }

            if (this.context.SkipAction != null)
                this.context.SkipAction.performed += OnSkip;

            var prefab    = this.context.DoorPrefab ?? this.defaultDoorPrefab;
            Animator? animator = null;

            if (prefab != null)
            {
                var door = Instantiate(prefab, transform);
                door.transform.localPosition = Vector3.zero;
                door.transform.localRotation = Quaternion.identity;

                animator = door.GetComponentInChildren<Animator>();
                if (animator != null)
                    animator.gameObject.AddComponent<DoorAnimationRelay>().Init(this);
            }
            else
            {
                Debug.LogWarning("[DoorTransitionController] No door prefab assigned — transition will fade without door.");
            }

            this.hasDoor = animator != null;

            RunTransition(animator).Forget();
        }

        private async UniTaskVoid RunTransition(Animator? animator)
        {
            this.canSkip = true;
            await Fade(0f, this.fadeInDuration, this.fadeInEase);

            if (animator == null)
            {
                OnAnimationComplete();
                return;
            }

            TimeoutFallback().Forget();
        }

        internal void PlayDoorOpenSfx()
        {
            this.openSwitch.SetValue(gameObject);
            this.doorEvent.Post(gameObject);
        }

        internal void OnAnimationComplete()
        {
            if (this.completed) return;
            this.completed = true;

            if (this.hasDoor)
            {
                this.closeSwitch.SetValue(gameObject);
                this.doorEvent.Post(gameObject);
            }

            FadeOutAndComplete().Forget();
        }

        private async UniTaskVoid FadeOutAndComplete()
        {
            await Fade(1f, this.fadeOutDuration, this.fadeOutEase);
            this.context?.NotifyComplete();
        }

        private async UniTaskVoid TimeoutFallback()
        {
            await UniTask.WaitForSeconds(this.animationTimeout, ignoreTimeScale: true);

            if (!this.completed)
            {
                Debug.LogWarning("[DoorTransitionController] Animation timeout — forcing transition complete.");
                OnAnimationComplete();
            }
        }

        private UniTask Fade(float to, float duration, Ease ease)
        {
            if (this.fadeOverlay == null)
                return UniTask.CompletedTask;

            var tcs = new UniTaskCompletionSource();
            this.fadeOverlay
                .DOFade(to, duration)
                .SetEase(ease)
                .SetUpdate(true)
                .OnComplete(() => tcs.TrySetResult());
            return tcs.Task;
        }
    }
}
```

- [ ] **Step 4: Verify compilation**

Use `mcp__UnityMCP__refresh_unity` to force a domain reload, then `mcp__UnityMCP__read_console` filtered to errors. Expected: no errors referencing `DoorTransitionController`, `DoorAnimationRelay`, or `AK.Wwise`.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/DoorTransitionController.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/DoorAnimationRelay.cs Game/CrimsonDraft/Assets/Scripts/Navigation/CrimsonDraft.Navigation.asmdef
git commit -m "feat(audio): wire door open/close SFX into DoorTransitionController"
```

---

### Task 2: Add the Open SFX Animation Event to the door-opening clip

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Art/Animations/Doors/Openning.anim`

**Interfaces:**
- Consumes: `DoorAnimationRelay.OnDoorOpenSfx()` from Task 1 — must match this exact method name, since Unity resolves Animation Event `functionName` by string at runtime with no compile-time check.

This clip is used by the `TransitionDoor` prefab (`Assets/Prefabs/World/Interactables/Door/TransitionDoor.prefab`, referenced as `defaultDoorPrefab` on the `DoorSpawner` GameObject in `DoorTransition.unity`) via its `Cube.controller` Animator Controller, state `Openning`. The clip already has one Animation Event at `time: 4` calling `OnAnimationComplete` (this is what currently ends the transition naturally). Add a second event at `time: 0` calling `OnDoorOpenSfx`, so the Open SFX fires the instant the animation starts playing.

- [ ] **Step 1: Read the current `m_Events` block**

The file currently ends with:

```yaml
  m_Events:
  - time: 4
    functionName: OnAnimationComplete
    data: 
    objectReferenceParameter: {fileID: 0}
    floatParameter: 0
    intParameter: 0
    messageOptions: 0
```

Unity requires clip events to be sorted ascending by `time`, so the new `time: 0` event must be inserted **before** the existing `time: 4` event.

- [ ] **Step 2: Edit the file**

Replace the `m_Events` block above with:

```yaml
  m_Events:
  - time: 0
    functionName: OnDoorOpenSfx
    data: 
    objectReferenceParameter: {fileID: 0}
    floatParameter: 0
    intParameter: 0
    messageOptions: 0
  - time: 4
    functionName: OnAnimationComplete
    data: 
    objectReferenceParameter: {fileID: 0}
    floatParameter: 0
    intParameter: 0
    messageOptions: 0
```

- [ ] **Step 3: Verify Unity picks up the change**

Call `mcp__UnityMCP__refresh_unity`, then `mcp__UnityMCP__read_console` (errors filter). Expected: no import errors for `Openning.anim`. Optionally open the Animation window on `Openning.anim` in the Unity Editor and confirm two event markers appear on the timeline (one at frame 0, one at the end).

- [ ] **Step 4: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Art/Animations/Doors/Openning.anim"
git commit -m "content(audio): add door-open SFX animation event to Openning clip"
```

---

### Task 3: Assign the Wwise Event/Switch references in the DoorTransition scene

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scenes/Production/DoorTransition.unity`

This step must happen in the Unity Editor's Inspector using the Wwise Picker — the serialized `AK.Wwise.Event`/`AK.Wwise.Switch` fields store an `idInternal`/`valueGuidInternal` pair plus a `WwiseObjectReference` pointing at a generated ScriptableObject asset under `Assets/Wwise/ScriptableObjects/`. These ScriptableObjects are created/matched by Wwise's own tooling when you pick an event/switch in the Inspector; hand-authoring the GUIDs risks a broken or mismatched reference (unlike the animation event in Task 2, which is plain data Unity doesn't need Wwise to resolve).

- [ ] **Step 1: Open the scene and select the target GameObject**

Open `Assets/Scenes/Production/DoorTransition.unity`. In the Hierarchy, select the `DoorSpawner` GameObject (root-level, holds the `DoorTransitionController` component with `defaultDoorPrefab` already assigned).

- [ ] **Step 2: Assign the Event field**

In the Inspector, on the `Door Transition Controller` component, click the picker circle next to `Door Event` and select the event **`Play_DoorsSC`** (under Events, per the Wwise project's `Default Work Unit.wwu`).

- [ ] **Step 3: Assign the Open switch**

Click the picker circle next to `Open Switch` and select **`DoorType/Open`** (SwitchGroup `DoorType`, switch `Open`).

- [ ] **Step 4: Assign the Close switch**

Click the picker circle next to `Close Switch` and select **`DoorType/Close`** (SwitchGroup `DoorType`, switch `Close`).

- [ ] **Step 5: Save the scene and verify the serialized values**

Save the scene (Ctrl+S / `mcp__UnityMCP__manage_scene` save action). Then inspect the diff on `DoorTransition.unity`: the `DoorTransitionController` MonoBehaviour block should now show non-empty `doorEvent`, `openSwitch`, `closeSwitch` entries with real `idInternal`/`valueGuidInternal`/`WwiseObjectReference` values (matching the shape already used by `fireGunEvent`/`shellCasingEvent` on the operator prefabs, e.g. `Assets/Prefabs/Characters/Ethan_Combat_FBX.prefab:1333-1369`), not the default-constructed empty values.

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scenes/Production/DoorTransition.unity
git add Game/CrimsonDraft/Assets/Wwise/ScriptableObjects
git commit -m "content(audio): assign Play_DoorsSC event and DoorType switches to DoorTransitionController"
```

(The second `git add` picks up any new/regenerated Wwise ScriptableObject assets created by the picker — check `git status` first to confirm what actually changed before committing.)

---

### Task 4: End-to-end verification in Play Mode

**Files:** none (verification only)

- [ ] **Step 1: Verify the natural-completion path**

Enter Play Mode at a point that triggers a room transition through `DoorTransition.unity` (or load the scene directly via `mcp__UnityMCP__manage_scene` and simulate `RoomTransitionContext.Set(...)` being populated, matching how `RoomOrchestrator` normally does it). Let the door animation play to completion without pressing skip. Confirm via the Wwise Profiler (or a temporary `Debug.Log` if the profiler isn't connected) that:
- `Play_DoorsSC` posts once near the start of the animation with switch `DoorType = Open`.
- `Play_DoorsSC` posts once at the end with switch `DoorType = Close`.

- [ ] **Step 2: Verify the skip path**

Repeat, but press the skip input shortly after the transition starts (after the Open post has already fired). Confirm:
- Only one additional `Play_DoorsSC` post occurs, with switch `DoorType = Close`.
- The transition still completes and hands off to `RoomTransitionContext.NotifyComplete()` as before (no regression to existing skip behavior).

- [ ] **Step 3: Verify the no-door-prefab fallback still behaves**

Temporarily clear `defaultDoorPrefab` on `DoorSpawner` (or a scene copy) and re-run the transition. Confirm no `Play_DoorsSC` post occurs at all (the `hasDoor` guard prevents a phantom Close post), and the fade-only fallback still completes the transition. Revert this temporary change afterward — do not commit it.

- [ ] **Step 4: Report results**

Summarize pass/fail for all three checks. If any check fails, stop and debug before considering this plan complete — do not report success without having observed the Wwise posts (or logs) directly.
