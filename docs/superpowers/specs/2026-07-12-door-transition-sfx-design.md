# Door Transition SFX Design

## Context

`DoorTransitionController` (Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/DoorTransitionController.cs) runs the animated door cutscene loaded by `RoomOrchestrator` between rooms. It instantiates a door prefab, plays its `Animator`, fades a `CanvasGroup` overlay, and lets the player skip via an input action wired through `RoomTransitionContext`.

Wwise already has the event `Play_DoorsSC` and a switch group `DoorType` with switches `Open` and `Close` (the group also has `Metal`/`Wood` switches used elsewhere by the `DoorsSC` switch container for material-based sound variation — unrelated to this feature). No Wwise project changes are needed.

## Goal

Play door SFX at two points in the transition:
1. **Open** — when the door-opening animation actually plays.
2. **Close** — when the transition ends, regardless of how it ended (animation finished naturally, timeout fallback, or the player skipped).

## Design

### Fields on `DoorTransitionController`

```csharp
[SerializeField] private AK.Wwise.Event  doorEvent   = new(); // Play_DoorsSC
[SerializeField] private AK.Wwise.Switch openSwitch  = new(); // DoorType/Open
[SerializeField] private AK.Wwise.Switch closeSwitch = new(); // DoorType/Close
```

Add a private field `hasDoor` (bool), set in `Start()` right after resolving the `Animator`:

```csharp
this.hasDoor = animator != null;
```

This gates the Close sound so nothing plays when there was no door prefab to animate in the first place (the existing "no door prefab assigned" fallback path).

### Open SFX — Animation Event

The Open sound is **not** tied to instantiation timing. It's triggered by an Animation Event placed on the door's opening clip, forwarded the same way the existing `OnAnimationComplete` event is forwarded:

- `DoorAnimationRelay` (Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/DoorAnimationRelay.cs) gains a new method:
  ```csharp
  public void OnDoorOpenSfx() => this.controller.PlayDoorOpenSfx();
  ```
- `DoorTransitionController` gains:
  ```csharp
  internal void PlayDoorOpenSfx()
  {
      this.openSwitch.SetValue(gameObject);
      this.doorEvent.Post(gameObject);
  }
  ```
- An Animation Event calling `OnDoorOpenSfx()` must be added at the correct frame of the door prefab's opening animation clip (same authoring step used for the existing "animation complete" event).

### Close SFX — single choke point in `OnAnimationComplete`

`OnAnimationComplete()` is already the single point reached by all three end-of-transition paths (natural animation-end Animation Event, timeout fallback, and `OnSkip`). Moving the Close post there means no duplicated logic in `OnSkip`:

```csharp
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
```

No changes needed in `OnSkip` itself — it already funnels into `OnAnimationComplete()`.

## Out of scope

- No changes to the `Metal`/`Wood` switches or the `DoorsSC` switch container's material-based sound selection.
- No changes to `RoomTransitionContext`, `RoomOrchestrator`, or the skip input action itself.
- Placing the Animation Event on the actual clip is an editor authoring step, not pure code — the implementation plan should call this out explicitly as a manual/verifiable step (potentially doable via the UnityMCP `manage_animation` tool).
