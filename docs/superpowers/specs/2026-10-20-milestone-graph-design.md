# Milestone Graph — Design Spec

**Date:** 2026-10-20
**Status:** Draft — awaiting review
**Branch:** `feat/pushables-rework` (to be moved to its own branch before implementation)
**Scope:** Navigation / progression — an editor tool to draw the game's progression diagram (milestones, arrows, lanes, notes, frames) and a runtime system that marks milestones completed from game state, saves them, and lets code query them.

---

## Overview

Designers already sketch the Deck B progression as a flow diagram: boxes such as "Take Sheep Key in Save Room" or "Place Emblems in Pig Portrait", arrows between them, colour lanes per route, frames, and notes like "Player choice" or "Must have Beeper". Nothing in the game knows about those milestones today.

This spec adds:

1. A `MilestoneGraph` asset and a **GraphView editor window** to draw that diagram.
2. A **trigger** on every milestone node: a condition over state the game already records (pickup collected, item held, socket filled / door unlocked, note read, puzzle solved).
3. A runtime **`MilestoneTracker`** that marks a milestone completed the moment its trigger holds, a **`MilestoneRegistry`** that saves completed milestones, and an **`IMilestoneService`** for code to query them.

The arrows are documentation only. They never block or gate anything; doors, keys and puzzles keep gating progress on their own.

The reference for the "milestones are named bits checked by room logic" idea is RE2's `Scenario_flg` / `Common_flg` (see `Design/Reference/RE2/jugador/hitos.md`). Unlike RE2, milestones here complete automatically from game state rather than from hand-written `set` calls.

### Decisions (from brainstorming)

| # | Decision |
|---|----------|
| 1 | The graph is a **map + tracking**: arrows are documentation, nothing is gated by them. |
| 2 | A milestone is completed **from its node**: the node declares a trigger condition; scenes are not wired by hand. |
| 3 | v1 trigger kinds: **pickup collected**, **item held**, **used in place** (socket complete or door unlocked), **note read**, **puzzle solved**. |
| 4 | Queries are **code only** (`IMilestoneService`). No Yarn function, no scene component, no Play Mode debug view in v1. |
| 5 | Editor built on **GraphView** (`UnityEditor.Experimental.GraphView`). Graph Toolkit was evaluated and rejected: experimental, deprecated as a package in 6.4, and no per-node / per-edge colours or dashed edges in its API. |
| 6 | Triggers are **conditions over the existing state registries**, re-evaluated when a registry changes, not one-off event listeners. |
| 7 | Once completed, a milestone **stays completed**, even if its condition later stops holding (e.g. the key is used up). |
| 8 | A new **`PuzzleStateRegistry`** gives every puzzle one shared, saved "solved" flag. |
| 9 | Graphs reach the runtime through a **cached serialized array** on `NavigationScope` (no runtime scene searches), included in **Cache Everything**. |

---

## 1. Data

### `MilestoneGraph` (new ScriptableObject)

`Navigation/Milestones/MilestoneGraph.cs` — `[CreateAssetMenu(menuName = "CrimsonDraft/Milestone Graph")]`. One asset per area or act (e.g. `Deck_B_Milestones`). It is read both by the editor window and by the runtime; there is no importer step.

| Field | Type | Purpose |
|-------|------|---------|
| `lanes` | `List<MilestoneLane>` | Named colours for routes (cyan, magenta, yellow, blue…) |
| `nodes` | `List<MilestoneNode>` | The milestones |
| `edges` | `List<MilestoneEdge>` | Arrows (documentation only) |
| `notes` | `List<MilestoneNote>` | Sticky notes ("Player choice", "Must have Beeper") |
| `frames` | `List<MilestoneFrame>` | Titled frames around a set of nodes (the yellow box in the sketch) |

```csharp
[Serializable] public sealed class MilestoneLane    { public string id; public string name; public Color color; }
[Serializable] public sealed class MilestoneNode    { public string id; public string title; public string laneId; public Vector2 position; public MilestoneTrigger trigger; }
[Serializable] public sealed class MilestoneEdge    { public string fromNodeId; public string toNodeId; public EdgeStyle style; public string laneId; public bool useCustomColor; public Color customColor; }
[Serializable] public sealed class MilestoneNote    { public string id; public string text; public Rect rect; }
[Serializable] public sealed class MilestoneFrame   { public string id; public string title; public string laneId; public List<string> nodeIds; }
public enum EdgeStyle { Solid, Dashed }
```

- `MilestoneNode.id` is a GUID string created with the node. It is what goes into the save file. Renaming the title never changes it.
- Edge colour = its lane colour, unless `useCustomColor`.

### `MilestoneTrigger`

```csharp
public enum MilestoneTriggerKind { None, PickupCollected, ItemHeld, SocketCompleted, DoorUnlocked, NoteRead, PuzzleSolved }

[Serializable]
public sealed class MilestoneTrigger
{
    public MilestoneTriggerKind kind;
    public string   targetId = "";   // pickupId / socketId / doorId / puzzleId
    public ItemData? item;           // ItemHeld only
    public DocumentData? note;       // NoteRead only (its NoteId is used)
}
```

"Used in place" from the brainstorming is two kinds, `SocketCompleted` and `DoorUnlocked`, because they read different registries.

| Kind | Holds when | Source |
|------|------------|--------|
| `PickupCollected` | `PickupRegistry.IsCollected(targetId)` | pickups |
| `ItemHeld` | `IInventoryService.HasItem(item.ItemId)` | inventory (covers items made by combining or given by puzzles) |
| `SocketCompleted` | every entry of `ItemSocketStateRegistry.GetInserted(targetId)` is `true` (and there is at least one) | item sockets |
| `DoorUnlocked` | `DoorStateRegistry.IsUnlocked(targetId)` | doors opened with a key |
| `NoteRead` | `NoteRegistry.IsCollected(note.NoteId)` | documents / notes |
| `PuzzleSolved` | `PuzzleStateRegistry.IsSolved(targetId)` | puzzles (new registry, §3) |
| `None` | never | node still being designed |

`MilestoneTriggerEvaluator` (pure static class) implements this table against an `IMilestoneWorldState` interface, so it is unit-testable with fakes.

### `MilestoneRef`

```csharp
[Serializable] public struct MilestoneRef { public MilestoneGraph? graph; public string nodeId; }
```

A property drawer shows the graph field plus a dropdown of that graph's node titles, so code-side consumers never type IDs.

---

## 2. Editor window (GraphView)

`Navigation/Editor/Milestones/MilestoneGraphWindow.cs` (assembly `CrimsonDraft.Navigation.Editor`) — menu `Tools/CrimsonDraft/Milestone Graph`; also opens on double-clicking a `MilestoneGraph` asset (`[OnOpenAsset]`).

- **Toolbar:** the open asset, a search field that frames the first node whose title matches, "Frame All", and a lane manager (add / rename / recolour / delete lanes).
- **Milestone node** (`MilestoneNodeView : Node`):
  - The header uses the lane colour and holds the editable title.
  - The body holds the trigger: a kind dropdown plus the matching ID picker.
  - One input port and one output port, both `Port.Capacity.Multi`.
- **ID pickers** by kind:
  - `ItemHeld`: an `ItemData` object field.
  - `NoteRead`: a `DocumentData` object field.
  - `PickupCollected` / `SocketCompleted` / `DoorUnlocked` / `PuzzleSolved`: a text field, plus a "…" button. The button lists the IDs of that kind found on `PickupInteractable` / `MapPickupInteractable`, `ItemSocketInteractable`, door interactables, and puzzles (components implementing `IPuzzle`, §3) in the open scenes.
- **Edges** (`MilestoneEdgeView : Edge`): created by dragging between ports. The context menu sets Solid / Dashed and the lane or a custom colour. Dashed edges are drawn by hand with `generateVisualContent` / `Painter2D`, because GraphView has no dashed edge.
- **Notes:** GraphView `StickyNote`. **Frames:** GraphView `Group`, titled and tinted by lane.
- **Editing:** move, copy/paste, delete, undo. Every change does `Undo.RecordObject(graph)`, writes the asset and calls `EditorUtility.SetDirty`. There is no separate Save button.
- **Validation badges** on a node:
  - no trigger (`None`);
  - a text ID not found in any open scene;
  - two nodes sharing the same trigger;
  - a node ID duplicated in another `MilestoneGraph` asset in the project.

The window stores nothing outside the asset, so the asset is the only source of truth.

Out of scope: text labels on edges (use a sticky note next to the edge) and a live Play Mode view.

---

## 3. Runtime

### `MilestoneRegistry` (new, global)

`Infrastructure/MilestoneRegistry.cs`, registered as a singleton in `GameLifetimeScope`:

```csharp
public bool Complete(string milestoneId);              // true if newly completed
public bool IsCompleted(string milestoneId);
public event Action<string>? Completed;
public IReadOnlyCollection<string> CompletedIds { get; }
public void LoadState(IEnumerable<string> saved);      // does not raise Completed
public void ClearAll();
```

### `PuzzleStateRegistry` (new, global)

`Infrastructure/PuzzleStateRegistry.cs`, also a `GameLifetimeScope` singleton: `SetSolved(id)`, `IsSolved(id)`, `event Action? Changed`, `SolvedIds`, `LoadState`, `ClearAll`.

Puzzles do not share a DI path today:
- `GeneratorSwitchPanel` uses `[Inject]`;
- `MusicBoxPanel` and `PuzzleInteractable` use the `InteractionContext`;
- `BeeperDoorMechanism` has neither.

So every puzzle implements a small interface:

```csharp
public interface IPuzzle
{
    string PuzzleId { get; }                            // serialized field, unique per puzzle
    void BindPuzzleState(PuzzleStateRegistry registry); // called once per scene load
}
```

Binding works like the pushables:
- `NavigationScope` caches the scene's puzzles in `[SerializeField] MonoBehaviour[] cachedPuzzles`. It uses a "Cache Scene Puzzles" button, which keeps only components implementing `IPuzzle` and is included in **Cache Everything**.
- A `PuzzleBootstrap` (`IInitializable`) calls `BindPuzzleState` on each one.

When a puzzle is solved it calls `registry.SetSolved(PuzzleId)`. Affected scripts:

| Script | Where it is solved |
|--------|--------------------|
| `GeneratorSwitchPanel` | next to its existing `registry.SetInserted(SolvedKey, …)` |
| `MusicBoxPanel` | where it sets `IsSolved = true` |
| `PuzzleInteractable` | in the `PuzzleViewController.Open(…, onSolved)` callback |
| `BeeperDoorMechanism` | the first `OnCodeAccepted` after the beeper was collected |

`JewelryBoxPanel` is not a puzzle: the box is an `ItemSocketInteractable`, so `SocketCompleted` covers it.

Their existing restore logic is **unchanged**. The new call is additive, and moving their own persistence onto the new registry is out of scope.

### Change notifications on the existing registries

Each source gains an `event Action? Changed`, raised only when its state actually changes:
- `PickupRegistry`
- `NoteRegistry`
- `ItemSocketStateRegistry`
- `DoorStateRegistry`

`IInventoryService` gains `event Action? ContentsChanged`, raised whenever any of its `ItemContainer.Changed` fires; today it only exposes `HeldChanged`. `LoadState` / `ClearAll` do not raise `Changed`.

### `MilestoneTracker` (new, per scene)

`Navigation/Milestones/MilestoneTracker.cs`, registered in `NavigationScope`, `IInitializable` + `IDisposable`, and registered **after** `SaveGameLoader`:

1. **On `Initialize`:** evaluates every node of every cached graph whose milestone isn't completed yet. This also completes milestones added to the graph after a save was made.
2. **On a source's `Changed`:** re-evaluates only the pending nodes whose trigger kind reads that source.
3. **When a trigger holds:** calls `MilestoneRegistry.Complete(node.id)`. It never un-completes.
4. **On `Dispose`:** unsubscribes from every source.

### `IMilestoneService`

`MilestoneService` (registered in `NavigationScope`) wraps the registry for consumers:

```csharp
bool IsCompleted(MilestoneRef milestone);
bool IsCompleted(string milestoneId);
event Action<MilestoneRef>? Completed;   // resolved back to graph + node for listeners
```

### Wiring

- `NavigationScope` gets `[SerializeField] MilestoneGraph[] cachedMilestoneGraphs` and a "Cache Scene Milestone Graphs" button. **Cache Everything** includes it. Because graphs are assets, not scene objects, the button collects every `MilestoneGraph` asset in the project (`AssetDatabase.FindAssets`) instead of using `FindObjectsByType`.
- `WorldStateRegistries` gains `Milestones` and `Puzzles`, and `GameStateResetter.ResetAll` clears both.
- `SaveGameData` gains `List<string> completedMilestoneIds` and `List<string> solvedPuzzleIds`. `SaveController` captures them and `SaveGameLoader` restores them. Old saves load with both lists empty.

### Edge cases

| Case | Behaviour |
|------|-----------|
| Save contains an ID no graph has (node deleted) | Kept in the registry, ignored by queries of real nodes; harmless |
| Same node ID in two graphs | Editor validation badge; at runtime both refer to the same completion |
| Trigger target doesn't exist in any scene | Editor badge; at runtime the milestone simply never completes |
| Trigger kind `None` | Never completes |
| Condition true at load time | Completed in `Initialize` (no `Completed` spam: one event per milestone) |

---

## 4. Testing

EditMode, plain C# fakes, matching the project's existing pattern:

- `MilestoneRegistryTests`:
  - `Complete` returns true only the first time;
  - `Completed` fires once;
  - `LoadState` doesn't fire it;
  - `ClearAll`.
- `PuzzleStateRegistryTests`: same shape.
- `MilestoneTriggerEvaluatorTests`: every trigger kind, true and false, against a fake `IMilestoneWorldState`. Includes a socket with an empty array (false) and a partially filled one (false).
- `MilestoneTrackerTests`:
  - completes on `Initialize` when the condition already holds;
  - completes on the source's `Changed`;
  - never un-completes;
  - ignores `None`;
  - only re-evaluates the kinds tied to the source that changed;
  - unsubscribes on dispose.
- Save round trip: extend `SaveControllerTests`, `SaveGameLoaderTests` and `GameStateResetterTests` for both new lists.
- `MilestoneGraph` asset: node IDs stay stable when the title changes; removing a node removes its edges and frame membership.

The GraphView window is checked by hand. A short manual checklist goes in the implementation plan.
