# Camera Zone Editor: one activation polygon per camera, edited in its own tool

## Problem

Fixed-camera activation is still authored the way the old box triggers were:

- zones are generic `FixedCameraZoneTrigger` objects edited in the Scene view, mixed with
  the rest of the level;
- one camera is activated by 1 to 4 zones (Deck_B: 76 zones for 59 cameras), so it is hard
  to see what activates a given camera;
- 33 zones use the two-sided switch (a divider line with a neutral band), which the
  designer finds confusing to maintain;
- commit `b6c442f8` switched detection from the player point to the player capsule
  (radius 0.5 m). Reproduced: walking A → B → back into A leaves the camera on B, and
  entering a normal zone next to a two-sided one leaves the camera on the two-sided one.

## Intent

A camera and the polygon that activates it are one thing, authored in a dedicated camera
tool: look through the camera, see its activation polygon, adjust both, without touching
the Scene view.

## Goals

- **One activation polygon per camera.** Entering it (player reference point) activates
  that camera. It fires on entry only; there is no "stay" re-evaluation and no divider.
- **Authored in the Camera Zone Viewer**, which gets a second view: a **plan (top-down)
  view of the room** showing every camera's polygon in its colour, editable there, next to
  the existing through-the-camera view.
- Existing data reaches the new model by **the designer's hand**: run the existing
  "Convert Boxes To Polygons" button, then set each camera's polygon in the tool.
- Detection goes back to the **player point** (as RE2 does and as the viewer draws).

## Non-goals

- No automatic merging of the several zones of a camera into one polygon, and no
  automatic split of two-sided zones (decision: the designer redraws them).
- RE2's "zones owned by the active camera" model is not adopted.
- No change to door arrivals, spawn migration or the completion gate semantics.

## Anti-goals

- The game must keep working while cameras are being redone one by one: a room with
  old multi-zone or two-sided triggers keeps behaving as today until its cameras are
  converted.
- Nothing is written to scenes or prefabs unless the designer acts in the tool.

## Design

### Runtime

- `FixedCameraZoneTrigger` stays the component that holds a polygon, height range and
  `zoneCamera`, and fires on entry (point-based, as before `b6c442f8`).
- **Body-overlap detection from `b6c442f8` is reverted.** Its inspection-shot guard in
  `FixedCameraZoneBootstrap.Tick` (skip zones while `CurrentZoneCamera != GameplayZoneCamera`)
  is kept: it fixes a real bug (two-sided zones stole the inspection camera).
- The two-sided switch keeps working at runtime for not-yet-converted zones but is
  **legacy**: the tool never creates it and flags it.
- A camera's *activation trigger* is the single non-two-sided `FixedCameraZoneTrigger`
  whose `zoneCamera` is that camera. The completion gate (socket / puzzle) stays on it.

### Tool (Camera Zone Viewer)

- **Plan view**, toggled or side by side with the camera view:
  - orthographic top-down render of the selected room (same unlit render path as the
    camera view), framed on the room's bounds, with pan and zoom;
  - every activation polygon of the room drawn flat, filled in its camera colour; the
    selected camera's polygon emphasised;
  - the selected camera's position and view frustum drawn on the plan, so the polygon can
    be drawn against what the camera sees;
  - the player marker, as in the camera view;
  - the same handles as today (move vertex, add vertex on an edge, Ctrl+click delete,
    move whole polygon) working on the plan.
- **Per camera status** in the camera list:
  - *ok* — exactly one activation trigger;
  - *no trigger* — button **Create Trigger**: adds a `FixedCameraZoneTrigger` under the
    room's `Cameras` object, `zoneCamera` set, a default square polygon under the camera's
    look point on the floor, single-sided;
  - *legacy* — the camera is referenced by more than one trigger, or by a two-sided one:
    list them, with **Keep this one** (make it the activation trigger, single-sided) and
    **Delete** per trigger.
- Selecting a trigger is never needed in the Scene view; the Scene-view handles may stay
  for convenience.

### Data path

1. Designer runs the existing **Convert Boxes To Polygons** (box → rectangle, already
   implemented).
2. In the tool, room by room: for each camera, resolve the legacy list down to one
   activation trigger and shape its polygon in the plan view.
3. Validation (existing toolbar) reports cameras with no or several activation triggers,
   and two-sided zones left.

## Testing

- EditMode: point-based entry restored — adjacent zones A → B → A cut back to A; zone next
  to a two-sided zone does not stick (the two cases reproduced against `b6c442f8`).
- EditMode: "activation trigger of a camera" resolution (none / one / several / two-sided).
- Plan-view math (world ↔ plan coordinates) as pure functions with tests.
- Manual: redo one room's cameras end to end in the tool, walk it in Play Mode.

## Calls made by me (not discussed)

- Revert of the body-overlap detection, keeping the inspection guard.
- Two-sided switch kept at runtime as legacy until every zone is redone, not deleted now.
- Activation polygon stays a separate `FixedCameraZoneTrigger` object linked by
  `zoneCamera`, rather than moving the polygon onto the camera GameObject (keeps existing
  data and the completion gate as they are).
- Plan view is an orthographic unlit render of the room, not a schematic.

## Left to the builder

- Plan view layout (tab vs split), zoom limits, frustum drawing style.
