# Map Editor: Straighten Room Lines to 90°/180°

## Problem

Room polygons authored in `MapRoomShape.LocalPoints` sometimes end up with vertices
that are almost, but not quite, axis-aligned (small hand-authoring noise). This makes
walls in the 2D map view look slightly diagonal instead of the clean orthogonal
silhouette the rooms are meant to have. There is currently no way to fix this from the
Map Editor Window — it only supports dragging a room's offset and rotating it in 90°
steps.

## Scope

Add a cleanup operation to `MapEditorWindow` that snaps every edge of a room's
polygon to the nearest cardinal direction (0°/90°/180°/270°), without a per-vertex
manual editing mode. Two entry points:

- **Straighten Room** — applies to the currently selected `MapRoomShape`.
- **Straighten All** — applies to every `MapRoomShape` in the open scene.

All room polygons in this project are rectilinear by design, so the operation applies
unconditionally to every edge — no threshold/exception handling for intentional
diagonal walls.

## Algorithm

Private method in `MapEditorWindow.cs`:

```
Vector2[] StraightenPolygon(Vector2[] points)
```

- If `points.Length < 3`, return unchanged (matches the existing guard in `DrawRoom`).
- Vertex 0 is the anchor and is never modified.
- Walk forward through vertices 1..N-1. For each pair (previous vertex — already
  processed, current vertex), compare `abs(delta.x)` vs `abs(delta.y)`:
  - If the edge is more horizontal than vertical, set `current.y = previous.y`
    (snap to a perfectly horizontal edge).
  - Otherwise set `current.x = previous.x` (snap to a perfectly vertical edge).
  - This preserves which axis each edge was already closest to — it only removes
    the diagonal noise, it doesn't flip a wall from horizontal to vertical.
- After processing all vertices, check the closing edge (last vertex → vertex 0).
  If it is not axis-aligned within a `0.01` unit epsilon (i.e. both `abs(delta.x)`
  and `abs(delta.y)` exceed `0.01`), log a `Debug.LogWarning`
  naming the room so the author knows that polygon isn't cleanly rectilinear and
  may need manual attention — the tool does not force a fix here, since bending
  this last edge to close is what would otherwise silently distort a shape that
  wasn't actually meant to be orthogonal.

This is a position-snapping approach (not a "preserve edge length, snap direction"
approach) because it self-corrects instead of accumulating drift, and it guarantees
exact closure for any polygon that was already close to rectilinear.

## UI

Two new buttons in `DrawToolbar`, next to the existing "Bake Now" button:

- **Straighten Room**: `GUI.enabled` is only true when `Selection.activeGameObject`
  has a `MapRoomShape` component. On click: `Undo.RecordObject(shape, "Straighten
  Room")`, replace `shape.LocalPoints` with the straightened result,
  `EditorUtility.SetDirty(shape)`.
- **Straighten All**: iterates every `MapRoomShape` found via
  `FindObjectsByType<MapRoomShape>` in the scene. Wraps all per-shape
  `Undo.RecordObject` calls in one undo group
  (`Undo.SetCurrentGroupName("Straighten All Rooms")` +
  `Undo.CollapseUndoOperations(group)`) so a single Ctrl+Z undoes every room at once.

No new files, no separate testable utility class — logic lives directly in
`MapEditorWindow.cs` alongside the existing drag/rotate input handlers, matching how
those are implemented today.

## Verification

Manual, in the Unity Editor (this is an Editor-only GUI tool, no automated test):

1. Open the Map Editor Window on a scene with an authored room whose polygon has
   slightly off-axis vertices.
2. Select that room, click **Straighten Room**, confirm in the 2D canvas that every
   wall segment is now perfectly horizontal or vertical.
3. Repeat with multiple rooms selected/unselected and click **Straighten All**;
   confirm all rooms are corrected.
4. Press Ctrl+Z after each operation and confirm the room(s) revert to their
   original polygon in one step.
5. Confirm the Console shows a warning (and does not throw) for a room whose
   closing edge can't align — e.g. a temporarily test polygon with a genuinely
   non-rectilinear point.
