# Sprite Map Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the polygon/3D-camera map with two hand-drawn sprites per room (incomplete/complete), authored in an asset-based editor window, shown centred in the MAP tab with vertical input switching floors.

**Architecture:**
- **Data:** `MapData` gains per-room sprites, a pixel `Position` and `QuarterTurns`. The scene bake only upserts content ids (`MapLayoutMerge`) and never touches layout.
- **Pure logic:** visibility (`MapRoomVisuals`), floors (`MapFloors`), bounds (`MapLayoutBounds`), the sprite import standard (`MapSpriteStandard`) and input stepping (`AxisStepper`) are pure and EditMode-tested.
- **Runtime:** `MapScreenView` becomes a uGUI sprite view. `MapRenderer`, its camera, `MapRoomShape` and the polygon code are deleted.

**Tech Stack:** Unity 6000.3 (C# 9), VContainer, uGUI + TextMeshPro, NUnit EditMode, Unity CLI (`unity recompile`, `unity command run_tests`, `unity command eval_file`, `editor_play`/`editor_stop`, `capture_game_view`).

**Spec:** `docs/superpowers/specs/2026-10-05-sprite-map-rework-design.md`

## Global Constraints

- **Branch** `feature/sprite-map`. **No commits until Task 5** (user preference "deja los commits para lo ultimo"). No `Co-Authored-By` trailer (CLAUDE.md).
- **No scene edits.** Prefabs only, edited through the Editor via `unity command eval_file` (`PrefabUtility.LoadPrefabContents` → edit → `SaveAsPrefabAsset` → `UnloadPrefabContents`).
- **Staging:** before staging, run `git status` and stage only this feature's files. Never stage `Art/UI/Boot*` or `_Recovery/`.
- **Code conventions:**
  - `#nullable enable` in every file; serialized fields `= null!` (or `?` for optional sprites); `[Preserve]` on DI constructors.
  - Editor code in `Scripts/Navigation/Editor/`. Runtime code uses no `UnityEditor` outside `#if UNITY_EDITOR`.
- **Editor-tool standards:**
  - Window state is `[SerializeField]` on the window; snap is also stored in `EditorPrefs`.
  - Every edit is wrapped in `BeginChangeCheck`/`EndChangeCheck` with `Undo.RecordObject(map, …)`, and `SetDirty` is called only after a change.
- **Unity housekeeping:** delete `Assets/Temp` after any screenshot. Restore `LiberationSans SDF - Fallback.asset` with `git checkout` if Play Mode dirties it.
- **Known baseline failures:** 5 × `CrimsonDraft.Tests.ItemSocketInteractableTests.*` and the flaky `ShotResolutionStrategyTests.PelletSpreadStrategy_positions_fallWithinEllipseBounds`.
- **Compile + tests:** `bash .superpowers/sdd/2026-10-05-sprite-map-rework/ut.sh [Fully.Qualified.TestClass ...]`. With no args it runs the full suite. The script is created in Task 1, Step 0.
- **Sprite geometry:**
  - The map unit is the **sprite pixel**: `RectTransform.sizeDelta = sprite.rect.size`, not `SetNativeSize`, so PPU never changes on-screen size.
  - `Position` is the **sprite centre**, and the pivot is the centre.
  - UI rotation is `Z = QuarterTurns × 90` (counter-clockwise).

## Review Focus

1. **Bake idempotency and layout safety.** Saving the scene twice, or entering Play Mode, must not move rooms or clear sprites. `MapLayoutMerge` tests cover it, and Task 4, Step 6 checks the real assets by diff.
2. **Room visible but missing the resolved sprite.** For example, a visited room with a pending pickup but no `IncompleteSprite`: it must be skipped, not throw or draw a white square. Covered by a test in Task 1.
3. **Stick held when the tab opens, or while the tab bar is active.** Floors must not step until the stick is released. `AxisStepper.Reset` plus a test covers it, and Task 2 re-checks it in Play Mode.
4. **A floor larger than the panel** must shrink to fit and never overflow. A small floor must not be scaled up. Covered by a `FitScale` test.
5. **Player's floor not in the available list**, for example when nothing has been visited yet in a fresh save: the tab must show the first available floor, or hide cleanly when none is available, with no null-reference exception. Covered by `MapFloors` tests and the `ShowCurrent` null path.

---

### Task 1: Data fields + pure map logic

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map/MapData.cs`
- Create:
  - `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map/MapRoomVisuals.cs`
  - `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map/MapLayoutBounds.cs`
  - `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map/MapFloors.cs`
  - `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map/MapLayoutMerge.cs`
  - `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map/MapSpriteStandard.cs`
  - `Game/CrimsonDraft/Assets/Scripts/UI/Inventory/AxisStepper.cs`
- Test, create:
  - `Game/CrimsonDraft/Assets/Tests/EditMode/MapTestData.cs`
  - `MapRoomVisualsTests.cs`
  - `MapLayoutBoundsTests.cs`
  - `MapFloorsTests.cs`
  - `MapLayoutMergeTests.cs`
  - `MapSpriteStandardTests.cs`
  - `AxisStepperTests.cs`

  All test files go in `Tests/EditMode/`.

**Interfaces:**
- **Produces (`CrimsonDraft.Infrastructure.Map`):**
  - **`MapRoomData`** gains these fields:
    - `Sprite? IncompleteSprite`;
    - `Sprite? CompleteSprite`;
    - `Vector2Int Position`;
    - `int QuarterTurns`;
    - `bool IsOrphan`.

    `Polygon` and `Transform` stay until Task 4.
  - **`MapData`** (`#if UNITY_EDITOR`): `bool EditorRemoveRoom(string roomId)`.
  - **`readonly struct MapRoomVisual(string RoomId, Sprite Sprite, Vector2Int Position, int QuarterTurns, bool IsCurrent)`**, with read-only properties of the same names.
  - **`static MapRoomVisuals`:**
    - `IReadOnlyList<MapRoomVisual> Resolve(MapData map, RoomStateRegistry rooms, PickupRegistry pickups, KnownMapsRegistry knownMaps, string? currentRoomId)`;
    - `bool IsComplete(MapRoomData room, PickupRegistry pickups)`;
    - `IReadOnlyList<MapRoomVisual> Preview(MapData map, bool complete)`.
  - **`static MapLayoutBounds`:** `Rect Compute(IReadOnlyList<MapRoomVisual> visuals)` and `Vector2 SizeOf(Sprite sprite, int quarterTurns)`.
  - **`sealed class MapFloors`:**
    - constructor `(IReadOnlyList<MapData> available, MapData? playerFloor)`;
    - `MapData? Current`, `bool HasUp`, `bool HasDown`;
    - `bool Step(int direction)`, where -1 means up (towards index 0) and +1 means down;
    - `static IReadOnlyList<MapData> Available(IEnumerable<MapData?> maps, RoomStateRegistry rooms, KnownMapsRegistry knownMaps)`.
  - **`static MapLayoutMerge`:** `List<MapRoomData> Upsert(IReadOnlyList<MapRoomData> existing, IReadOnlyList<MapRoomData> baked)`.
  - **`static MapSpriteStandard`:** `(float PixelsPerUnit, FilterMode Filter)? Majority(IEnumerable<Sprite?> sprites)` and `bool Matches(Sprite sprite, (float PixelsPerUnit, FilterMode Filter) standard)`.
- **Produces (`CrimsonDraft.UI`):** `sealed class AxisStepper` with `const float PressThreshold = 0.5f`, `const float ReleaseThreshold = 0.2f`, `int Update(float y)` (returns -1 for up, +1 for down, 0 for none) and `void Reset()`.

- [ ] **Step 0: Create the verification script** `.superpowers/sdd/2026-10-05-sprite-map-rework/ut.sh` (repo-root relative, git-ignored). Create the workspace with `../subagent-driven-development/scripts/sdd-workspace` if the executor uses it.

```bash
#!/usr/bin/env bash
set -uo pipefail
proj="/d/Proyectos Unity/CrimsonDraft/CrimsonDraft/Game/CrimsonDraft"
ws="$(cd "$(dirname "$0")" && pwd)"
cd "$proj"
if ! unity recompile --no-banner > "$ws/last-compile.log" 2>&1; then
  echo "COMPILE FAILED:"; tail -n 40 "$ws/last-compile.log"; exit 1
fi
filters=("$@"); [ ${#filters[@]} -eq 0 ] && filters=("")
rc=0
for f in "${filters[@]}"; do
  args=(--mode EditMode --timeout 900 --result-only); [ -n "$f" ] && args+=(--filter "$f")
  unity command run_tests "${args[@]}" > "$ws/last-tests.json" 2>&1
  python - "$ws/last-tests.json" "${f:-ALL}" <<'PY' || rc=1
import json, sys
raw = open(sys.argv[1], encoding="utf-8", errors="replace").read()
try: data = json.loads(raw[raw.index("{"):])
except Exception: print("NO JSON RESULT:\n" + raw[-2000:]); sys.exit(1)
s = data.get("Summary", {})
fails = [r for r in data.get("Results", []) if r.get("Status") == "Failed"]
known = [r for r in fails if r["FullName"].startswith("CrimsonDraft.Tests.ItemSocketInteractableTests.") or r["FullName"] == "CrimsonDraft.Tests.ShotResolutionStrategyTests.PelletSpreadStrategy_positions_fallWithinEllipseBounds"]
new = [r for r in fails if r not in known]
print(f"[{sys.argv[2]}] total={s.get('Total')} passed={s.get('Passed')} failed={s.get('Failed')} (known-baseline={len(known)}, new={len(new)})")
for r in new: print("  FAIL", r["FullName"], "::", (r.get("Message") or "").strip()[:300])
sys.exit(1 if new or s.get("Total", 0) == 0 else 0)
PY
done
exit $rc
```

Run it with no args. Expected: `[ALL] total=<N> … new=0`. Record `<N>` in the ledger as the baseline.

- [ ] **Step 1: Write the failing tests.**

`MapTestData.cs`:

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    internal static class MapTestData
    {
        public static Sprite Sprite(int width = 10, int height = 10, float ppu = 100f, FilterMode filter = FilterMode.Point)
        {
            var texture = new Texture2D(width, height) { filterMode = filter };
            return UnityEngine.Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), ppu);
        }

        public static MapRoomData Room(
            string id,
            Vector2Int position = default,
            int quarterTurns = 0,
            string[]? pickups = null,
            Sprite? incomplete = null,
            Sprite? complete = null)
            => new()
            {
                RoomId           = id,
                Position         = position,
                QuarterTurns     = quarterTurns,
                PickupIds        = pickups ?? Array.Empty<string>(),
                IncompleteSprite = incomplete,
                CompleteSprite   = complete,
            };

        public static MapData Map(string sceneName, params MapRoomData[] rooms)
        {
            var map = ScriptableObject.CreateInstance<MapData>();
            var so  = new SerializedObject(map);
            so.FindProperty("sceneName").stringValue = sceneName;
            so.ApplyModifiedPropertiesWithoutUndo();
            map.EditorSetBakedContent(new List<MapRoomData>(rooms), new List<MapDoorData>());
            return map;
        }
    }
}
```

`MapRoomVisualsTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapRoomVisualsTests
    {
        private Sprite incomplete = null!;
        private Sprite complete   = null!;
        private RoomStateRegistry rooms     = null!;
        private PickupRegistry    pickups   = null!;
        private KnownMapsRegistry knownMaps = null!;

        [SetUp]
        public void SetUp()
        {
            this.incomplete = MapTestData.Sprite();
            this.complete   = MapTestData.Sprite();
            this.rooms      = new RoomStateRegistry();
            this.pickups    = new PickupRegistry();
            this.knownMaps  = new KnownMapsRegistry();
        }

        private MapRoomData Room(string id, params string[] pickupIds)
            => MapTestData.Room(id, pickups: pickupIds, incomplete: this.incomplete, complete: this.complete);

        private System.Collections.Generic.IReadOnlyList<MapRoomVisual> Resolve(MapData map, string? current = null)
            => MapRoomVisuals.Resolve(map, this.rooms, this.pickups, this.knownMaps, current);

        [Test]
        public void Unvisited_withoutMapItem_isHidden()
        {
            Assert.IsEmpty(Resolve(MapTestData.Map("deck-a", Room("a"))));
        }

        [Test]
        public void Visited_isDrawn()
        {
            this.rooms.MarkVisited("a");
            Assert.AreEqual(1, Resolve(MapTestData.Map("deck-a", Room("a"))).Count);
        }

        [Test]
        public void Unvisited_withMapItem_isDrawn()
        {
            this.knownMaps.MarkKnown("deck-a");
            Assert.AreEqual(1, Resolve(MapTestData.Map("deck-a", Room("a"))).Count);
        }

        [Test]
        public void PendingPickup_usesIncompleteSprite()
        {
            this.rooms.MarkVisited("a");
            Assert.AreSame(this.incomplete, Resolve(MapTestData.Map("deck-a", Room("a", "p1")))[0].Sprite);
        }

        [Test]
        public void AllPickupsCollected_usesCompleteSprite()
        {
            this.rooms.MarkVisited("a");
            this.pickups.SetCollected("p1");
            Assert.AreSame(this.complete, Resolve(MapTestData.Map("deck-a", Room("a", "p1")))[0].Sprite);
        }

        [Test]
        public void NoPickups_unvisitedWithMapItem_usesCompleteSprite()
        {
            this.knownMaps.MarkKnown("deck-a");
            Assert.AreSame(this.complete, Resolve(MapTestData.Map("deck-a", Room("a")))[0].Sprite);
        }

        [Test]
        public void MissingResolvedSprite_isNotDrawn()
        {
            this.rooms.MarkVisited("a");
            var room = MapTestData.Room("a", pickups: new[] { "p1" }, complete: this.complete);
            Assert.IsEmpty(Resolve(MapTestData.Map("deck-a", room)));
        }

        [Test]
        public void CurrentRoom_isFlagged_othersAreNot()
        {
            this.rooms.MarkVisited("a");
            this.rooms.MarkVisited("b");
            var visuals = Resolve(MapTestData.Map("deck-a", Room("a"), Room("b")), current: "b");
            Assert.IsFalse(visuals[0].IsCurrent);
            Assert.IsTrue(visuals[1].IsCurrent);
        }

        [Test]
        public void NullCurrentRoom_flagsNone()
        {
            this.rooms.MarkVisited("a");
            Assert.IsFalse(Resolve(MapTestData.Map("deck-a", Room("a")))[0].IsCurrent);
        }

        [Test]
        public void Visual_carriesPositionAndQuarterTurns()
        {
            this.rooms.MarkVisited("a");
            var room = MapTestData.Room("a", new Vector2Int(3, -4), 3, incomplete: this.incomplete, complete: this.complete);
            var visual = Resolve(MapTestData.Map("deck-a", room))[0];
            Assert.AreEqual(new Vector2Int(3, -4), visual.Position);
            Assert.AreEqual(3, visual.QuarterTurns);
        }

        [Test]
        public void Preview_ignoresDiscovery_andPicksRequestedSprite()
        {
            var map = MapTestData.Map("deck-a", Room("a", "p1"));
            Assert.AreSame(this.complete,   MapRoomVisuals.Preview(map, complete: true)[0].Sprite);
            Assert.AreSame(this.incomplete, MapRoomVisuals.Preview(map, complete: false)[0].Sprite);
        }
    }
}
```

`MapLayoutBoundsTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapLayoutBoundsTests
    {
        private static MapRoomVisual Visual(int w, int h, int x, int y, int turns = 0)
            => new("r", MapTestData.Sprite(w, h), new Vector2Int(x, y), turns, false);

        [Test]
        public void Empty_isZeroRect()
        {
            Assert.AreEqual(Rect.zero, MapLayoutBounds.Compute(new MapRoomVisual[0]));
        }

        [Test]
        public void SingleRoom_isCentredOnPosition()
        {
            var bounds = MapLayoutBounds.Compute(new[] { Visual(10, 20, 5, 5) });
            Assert.AreEqual(new Vector2(5, 5), bounds.center);
            Assert.AreEqual(new Vector2(10, 20), bounds.size);
        }

        [Test]
        public void TwoRooms_centreBetweenThem()
        {
            var bounds = MapLayoutBounds.Compute(new[] { Visual(10, 10, 0, 0), Visual(10, 10, 20, 0) });
            Assert.AreEqual(new Vector2(10, 0), bounds.center);
            Assert.AreEqual(new Vector2(30, 10), bounds.size);
        }

        [Test]
        public void OddQuarterTurns_swapWidthAndHeight()
        {
            Assert.AreEqual(new Vector2(20, 10), MapLayoutBounds.Compute(new[] { Visual(10, 20, 0, 0, turns: 1) }).size);
            Assert.AreEqual(new Vector2(10, 20), MapLayoutBounds.Compute(new[] { Visual(10, 20, 0, 0, turns: 2) }).size);
        }
    }
}
```

`MapFloorsTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapFloorsTests
    {
        private MapData a = null!, b = null!, c = null!;

        [SetUp]
        public void SetUp()
        {
            this.a = MapTestData.Map("deck-a");
            this.b = MapTestData.Map("deck-b");
            this.c = MapTestData.Map("deck-c");
        }

        [Test]
        public void Available_keepsSetOrder_andSkipsUnknownAndNull()
        {
            var known = new KnownMapsRegistry();
            known.MarkKnown("deck-c");
            known.MarkKnown("deck-a");
            var result = MapFloors.Available(new MapData?[] { this.a, null, this.b, this.c }, new RoomStateRegistry(), known);
            CollectionAssert.AreEqual(new[] { this.a, this.c }, result);
        }

        [Test]
        public void Start_isPlayerFloor()
        {
            Assert.AreSame(this.b, new MapFloors(new[] { this.a, this.b, this.c }, this.b).Current);
        }

        [Test]
        public void Start_whenPlayerFloorUnavailable_isFirst()
        {
            Assert.AreSame(this.a, new MapFloors(new[] { this.a, this.c }, this.b).Current);
        }

        [Test]
        public void Empty_hasNoCurrent_andNoArrows()
        {
            var floors = new MapFloors(new MapData[0], this.a);
            Assert.IsNull(floors.Current);
            Assert.IsFalse(floors.HasUp);
            Assert.IsFalse(floors.HasDown);
            Assert.IsFalse(floors.Step(+1));
        }

        [Test]
        public void Step_movesAndReportsArrows()
        {
            var floors = new MapFloors(new[] { this.a, this.b, this.c }, this.b);
            Assert.IsTrue(floors.HasUp);
            Assert.IsTrue(floors.HasDown);
            Assert.IsTrue(floors.Step(-1));
            Assert.AreSame(this.a, floors.Current);
            Assert.IsFalse(floors.HasUp);
        }

        [Test]
        public void Step_clampsAtBothEnds_withoutWrapping()
        {
            var floors = new MapFloors(new[] { this.a, this.b }, this.a);
            Assert.IsFalse(floors.Step(-1));
            Assert.AreSame(this.a, floors.Current);
            Assert.IsTrue(floors.Step(+1));
            Assert.IsFalse(floors.Step(+1));
            Assert.AreSame(this.b, floors.Current);
        }
    }
}
```

`MapLayoutMergeTests.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapLayoutMergeTests
    {
        private static MapRoomData Baked(string id, string[]? pickups = null, string[]? doors = null)
            => new() { RoomId = id, PickupIds = pickups ?? new string[0], DoorIds = doors ?? new string[0] };

        [Test]
        public void Upsert_keepsLayout_andReplacesIds()
        {
            var sprite   = MapTestData.Sprite();
            var existing = MapTestData.Room("a", new Vector2Int(3, 4), 1, new[] { "p1" }, sprite, sprite);
            var result   = MapLayoutMerge.Upsert(new[] { existing }, new[] { Baked("a", new[] { "p2" }, new[] { "d1" }) });

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(sprite, result[0].IncompleteSprite);
            Assert.AreSame(sprite, result[0].CompleteSprite);
            Assert.AreEqual(new Vector2Int(3, 4), result[0].Position);
            Assert.AreEqual(1, result[0].QuarterTurns);
            CollectionAssert.AreEqual(new[] { "p2" }, result[0].PickupIds);
            CollectionAssert.AreEqual(new[] { "d1" }, result[0].DoorIds);
            Assert.IsFalse(result[0].IsOrphan);
        }

        [Test]
        public void Upsert_appendsNewRooms_afterExistingOnes()
        {
            var result = MapLayoutMerge.Upsert(new[] { MapTestData.Room("a") }, new[] { Baked("b"), Baked("a") });
            CollectionAssert.AreEqual(new[] { "a", "b" }, new[] { result[0].RoomId, result[1].RoomId });
            Assert.AreEqual(Vector2Int.zero, result[1].Position);
            Assert.IsNull(result[1].IncompleteSprite);
        }

        [Test]
        public void Upsert_keepsMissingRooms_markedAsOrphans()
        {
            var result = MapLayoutMerge.Upsert(new[] { MapTestData.Room("a"), MapTestData.Room("gone") }, new[] { Baked("a") });
            Assert.AreEqual(2, result.Count);
            Assert.IsFalse(result[0].IsOrphan);
            Assert.IsTrue(result[1].IsOrphan);
        }

        [Test]
        public void Upsert_clearsOrphan_whenRoomReturns()
        {
            var room = MapTestData.Room("a");
            room.IsOrphan = true;
            Assert.IsFalse(MapLayoutMerge.Upsert(new[] { room }, new[] { Baked("a") })[0].IsOrphan);
        }

        [Test]
        public void Upsert_isIdempotent()
        {
            var baked = new[] { Baked("a", new[] { "p1" }), Baked("b") };
            var once  = MapLayoutMerge.Upsert(new List<MapRoomData>(), baked);
            var twice = MapLayoutMerge.Upsert(once, baked);

            Assert.AreEqual(once.Count, twice.Count);
            for (int i = 0; i < once.Count; i++)
            {
                Assert.AreEqual(once[i].RoomId, twice[i].RoomId);
                CollectionAssert.AreEqual(once[i].PickupIds, twice[i].PickupIds);
                Assert.AreEqual(once[i].Position, twice[i].Position);
                Assert.AreEqual(once[i].IsOrphan, twice[i].IsOrphan);
            }
        }
    }
}
```

`MapSpriteStandardTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapSpriteStandardTests
    {
        [Test]
        public void Majority_picksMostCommonPpuAndFilter()
        {
            var standard = MapSpriteStandard.Majority(new[]
            {
                MapTestData.Sprite(ppu: 16f, filter: FilterMode.Point),
                MapTestData.Sprite(ppu: 16f, filter: FilterMode.Point),
                MapTestData.Sprite(ppu: 100f, filter: FilterMode.Bilinear),
            });
            Assert.AreEqual((16f, FilterMode.Point), standard);
        }

        [Test]
        public void Majority_ignoresNulls_andIsNullWhenEmpty()
        {
            Assert.IsNull(MapSpriteStandard.Majority(new Sprite?[] { null }));
        }

        [Test]
        public void Matches_comparesPpuAndFilter()
        {
            var sprite = MapTestData.Sprite(ppu: 16f, filter: FilterMode.Point);
            Assert.IsTrue(MapSpriteStandard.Matches(sprite, (16f, FilterMode.Point)));
            Assert.IsFalse(MapSpriteStandard.Matches(sprite, (32f, FilterMode.Point)));
            Assert.IsFalse(MapSpriteStandard.Matches(sprite, (16f, FilterMode.Bilinear)));
        }
    }
}
```

`AxisStepperTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.UI;

namespace CrimsonDraft.Tests
{
    public sealed class AxisStepperTests
    {
        [Test]
        public void Up_returnsMinusOne_down_returnsPlusOne()
        {
            Assert.AreEqual(-1, new AxisStepper().Update(0.9f));
            Assert.AreEqual(+1, new AxisStepper().Update(-0.9f));
        }

        [Test]
        public void Holding_doesNotRepeat()
        {
            var stepper = new AxisStepper();
            stepper.Update(-0.9f);
            Assert.AreEqual(0, stepper.Update(-0.9f));
        }

        [Test]
        public void BelowPress_doesNothing()
        {
            Assert.AreEqual(0, new AxisStepper().Update(0.4f));
        }

        [Test]
        public void Rearms_onlyBelowReleaseThreshold()
        {
            var stepper = new AxisStepper();
            stepper.Update(-0.9f);
            stepper.Update(-0.3f);
            Assert.AreEqual(0, stepper.Update(-0.9f));
            stepper.Update(-0.1f);
            Assert.AreEqual(+1, stepper.Update(-0.9f));
        }

        [Test]
        public void Reset_requiresReleaseBeforeNextStep()
        {
            var stepper = new AxisStepper();
            stepper.Reset();
            Assert.AreEqual(0, stepper.Update(0.9f));
            stepper.Update(0f);
            Assert.AreEqual(-1, stepper.Update(0.9f));
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails.**
  - Run: `ut.sh CrimsonDraft.Tests.MapRoomVisualsTests`.
  - Expected: COMPILE FAILED, because `MapRoomVisual`, `MapRoomVisuals`, `MapFloors`, `MapLayoutMerge`, `MapSpriteStandard`, `AxisStepper` and the new `MapRoomData` fields do not exist yet.

- [ ] **Step 3: Implement.**

In `MapData.cs`, add the fields to `MapRoomData`. Keep the old fields for now: `Transform` and `Polygon` are removed in Task 4.

```csharp
    [Serializable]
    public sealed class MapRoomData
    {
        public string RoomId = "";
        public Sprite? IncompleteSprite;
        public Sprite? CompleteSprite;
        public Vector2Int Position;
        public int QuarterTurns;
        public bool IsOrphan;
        public Vector2[] Polygon = Array.Empty<Vector2>();
        public MapElementTransform Transform = new();
        public string[] DoorIds = Array.Empty<string>();
        public string[] PickupIds = Array.Empty<string>();
    }
```

In `MapData`, inside the existing `#if UNITY_EDITOR` block:

```csharp
        public bool EditorRemoveRoom(string roomId) => this.rooms.RemoveAll(r => r.RoomId == roomId) > 0;
```

`MapRoomVisuals.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    public readonly struct MapRoomVisual
    {
        public MapRoomVisual(string roomId, Sprite sprite, Vector2Int position, int quarterTurns, bool isCurrent)
        {
            this.RoomId       = roomId;
            this.Sprite       = sprite;
            this.Position     = position;
            this.QuarterTurns = quarterTurns;
            this.IsCurrent    = isCurrent;
        }

        public string     RoomId       { get; }
        public Sprite     Sprite       { get; }
        public Vector2Int Position     { get; }
        public int        QuarterTurns { get; }
        public bool       IsCurrent    { get; }
    }

    /// <summary>Which rooms a floor draws and with which sprite. A room shows when visited or
    /// when the floor's map item is owned; it uses the complete sprite once every pickup in it is
    /// collected (no pickups = complete). Pass currentRoomId only for the player's own floor.</summary>
    public static class MapRoomVisuals
    {
        public static IReadOnlyList<MapRoomVisual> Resolve(
            MapData map,
            RoomStateRegistry rooms,
            PickupRegistry pickups,
            KnownMapsRegistry knownMaps,
            string? currentRoomId)
        {
            bool hasMapItem = knownMaps.IsKnown(map.SceneName);
            var result = new List<MapRoomVisual>();

            foreach (var room in map.Rooms)
            {
                bool visited = rooms.GetState(room.RoomId) == RoomMapState.Visited;
                if (!visited && !hasMapItem)
                    continue;

                var sprite = IsComplete(room, pickups) ? room.CompleteSprite : room.IncompleteSprite;
                if (sprite == null)
                    continue;

                result.Add(new MapRoomVisual(room.RoomId, sprite, room.Position, room.QuarterTurns, room.RoomId == currentRoomId));
            }

            return result;
        }

        public static bool IsComplete(MapRoomData room, PickupRegistry pickups)
            => room.PickupIds.All(pickups.IsCollected);

        public static IReadOnlyList<MapRoomVisual> Preview(MapData map, bool complete)
            => map.Rooms
                .Select(room => (room, sprite: complete ? room.CompleteSprite : room.IncompleteSprite))
                .Where(pair => pair.sprite != null)
                .Select(pair => new MapRoomVisual(pair.room.RoomId, pair.sprite!, pair.room.Position, pair.room.QuarterTurns, false))
                .ToList();
    }
}
```

`MapLayoutBounds.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>Axis-aligned bounds of a floor in map pixels. Each sprite is centred on its
    /// Position; odd quarter turns swap its width and height.</summary>
    public static class MapLayoutBounds
    {
        public static Rect Compute(IReadOnlyList<MapRoomVisual> visuals)
        {
            if (visuals.Count == 0)
                return Rect.zero;

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            foreach (var visual in visuals)
            {
                var half   = SizeOf(visual.Sprite, visual.QuarterTurns) * 0.5f;
                var centre = (Vector2)visual.Position;
                min = Vector2.Min(min, centre - half);
                max = Vector2.Max(max, centre + half);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static Vector2 SizeOf(Sprite sprite, int quarterTurns)
        {
            var size = sprite.rect.size;
            return quarterTurns % 2 != 0 ? new Vector2(size.y, size.x) : size;
        }
    }
}
```

`MapFloors.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>The floors the MAP tab can show, in MapDataSet order (index 0 = top floor), and
    /// which one is on screen. Steps clamp at both ends.</summary>
    public sealed class MapFloors
    {
        private readonly IReadOnlyList<MapData> available;
        private int index;

        public MapFloors(IReadOnlyList<MapData> available, MapData? playerFloor)
        {
            this.available = available;
            int playerIndex = playerFloor == null ? -1 : IndexOf(available, playerFloor);
            this.index = playerIndex >= 0 ? playerIndex : 0;
        }

        public MapData? Current => this.available.Count > 0 ? this.available[this.index] : null;
        public bool     HasUp   => this.available.Count > 0 && this.index > 0;
        public bool     HasDown => this.index < this.available.Count - 1;

        public bool Step(int direction)
        {
            int next = this.index + direction;
            if (direction == 0 || next < 0 || next >= this.available.Count)
                return false;

            this.index = next;
            return true;
        }

        public static IReadOnlyList<MapData> Available(
            IEnumerable<MapData?> maps,
            RoomStateRegistry rooms,
            KnownMapsRegistry knownMaps)
            => maps
                .Where(map => map != null && MapStateResolver.IsDeckKnown(map, rooms, knownMaps))
                .Select(map => map!)
                .ToList();

        private static int IndexOf(IReadOnlyList<MapData> maps, MapData target)
        {
            for (int i = 0; i < maps.Count; i++)
                if (ReferenceEquals(maps[i], target))
                    return i;
            return -1;
        }
    }
}
```

`MapStateResolver` is in namespace `CrimsonDraft.Infrastructure`, while `MapFloors` is in `CrimsonDraft.Infrastructure.Map`. The parent namespace resolves without a `using`.

`MapLayoutMerge.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>Folds a scene bake into a floor's room list. The bake owns content ids
    /// (pickups, doors); the Map Editor owns layout (sprites, position, rotation), which this
    /// never touches. Rooms no longer in the scene are kept and flagged as orphans.</summary>
    public static class MapLayoutMerge
    {
        public static List<MapRoomData> Upsert(IReadOnlyList<MapRoomData> existing, IReadOnlyList<MapRoomData> baked)
        {
            var bakedById = new Dictionary<string, MapRoomData>();
            foreach (var room in baked)
                bakedById[room.RoomId] = room;

            var result = new List<MapRoomData>(existing.Count);
            foreach (var room in existing)
            {
                if (bakedById.TryGetValue(room.RoomId, out var fresh))
                {
                    room.PickupIds = fresh.PickupIds.ToArray();
                    room.DoorIds   = fresh.DoorIds.ToArray();
                    room.IsOrphan  = false;
                }
                else
                {
                    room.IsOrphan = true;
                }
                result.Add(room);
            }

            var known = new HashSet<string>(existing.Select(room => room.RoomId));
            foreach (var room in baked)
            {
                if (!known.Add(room.RoomId))
                    continue;

                result.Add(new MapRoomData
                {
                    RoomId    = room.RoomId,
                    PickupIds = room.PickupIds.ToArray(),
                    DoorIds   = room.DoorIds.ToArray(),
                });
            }

            return result;
        }
    }
}
```

`MapSpriteStandard.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>Map room sprites share one pixels-per-unit and filter mode. The standard of a
    /// floor is whatever most of its sprites already use; the Map Editor flags the rest.</summary>
    public static class MapSpriteStandard
    {
        public static (float PixelsPerUnit, FilterMode Filter)? Majority(IEnumerable<Sprite?> sprites)
        {
            var group = sprites
                .Where(sprite => sprite != null)
                .GroupBy(sprite => (sprite!.pixelsPerUnit, sprite.texture.filterMode))
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            return group == null ? null : group.Key;
        }

        public static bool Matches(Sprite sprite, (float PixelsPerUnit, FilterMode Filter) standard)
            => Mathf.Approximately(sprite.pixelsPerUnit, standard.PixelsPerUnit)
               && sprite.texture.filterMode == standard.Filter;
    }
}
```

`AxisStepper.cs`:

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI
{
    /// <summary>Turns an analog vertical axis into one step per press: fires past
    /// PressThreshold, re-arms only once the axis falls back under ReleaseThreshold.
    /// Up (+y) returns -1 (towards the top floor), down returns +1.</summary>
    public sealed class AxisStepper
    {
        public const float PressThreshold   = 0.5f;
        public const float ReleaseThreshold = 0.2f;

        private bool armed = true;

        public int Update(float y)
        {
            float magnitude = Mathf.Abs(y);

            if (!this.armed)
            {
                if (magnitude < ReleaseThreshold)
                    this.armed = true;
                return 0;
            }

            if (magnitude < PressThreshold)
                return 0;

            this.armed = false;
            return y > 0f ? -1 : +1;
        }

        public void Reset() => this.armed = false;
    }
}
```

- [ ] **Step 4: Run.**
  - Run: `ut.sh CrimsonDraft.Tests.MapRoomVisualsTests CrimsonDraft.Tests.MapLayoutBoundsTests CrimsonDraft.Tests.MapFloorsTests CrimsonDraft.Tests.MapLayoutMergeTests CrimsonDraft.Tests.MapSpriteStandardTests CrimsonDraft.Tests.AxisStepperTests`.
  - Expected: every class passes, with `new=0`.

- [ ] **Step 5: Full suite.** `ut.sh`. Expected: `new=0`, and the total equals the baseline plus 34.

---

### Task 2: Runtime — sprite `MapScreenView`, floor-stepping `MapTabController`, `MapRenderer` removed

**Files:**
- Modify (rewrite): `Game/CrimsonDraft/Assets/Scripts/Navigation/UI/MapScreenView.cs`
- Modify (rewrite): `Game/CrimsonDraft/Assets/Scripts/UI/Inventory/MapTabController.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs` (remove `RegisterComponentInHierarchy<MapRenderer>()`, line ~91)
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/MapStateResolver.cs` (delete `MapRoomVisualState` and `ResolveRoom`; keep `IsDeckKnown`)
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/MapStateResolverTests.cs` (delete the six `ResolveRoom_*` tests; keep `IsDeckKnown_whenMapRegistered_returnsTrue`)
- Delete: `Game/CrimsonDraft/Assets/Scripts/Navigation/Map/MapRenderer.cs` (+ `.meta`), `Game/CrimsonDraft/Assets/Tests/EditMode/MapRendererTests.cs` (+ `.meta`)
- Prefab: `Game/CrimsonDraft/Assets/Prefabs/Core/__GAMEPLAYCORE.prefab`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/MapScreenViewTests.cs`

**Interfaces:**
- **Consumes:** `MapRoomVisual`, `MapRoomVisuals.Resolve`, `MapLayoutBounds.Compute`, `MapFloors`, `AxisStepper` (all from Task 1).
- **Produces:** `MapScreenView`, with these members:
  - `void Show(IReadOnlyList<MapRoomVisual> visuals, string floorName, bool hasUp, bool hasDown)`;
  - `void Hide()`;
  - `bool IsVisible`;
  - `static float FitScale(Vector2 contentSize, Vector2 viewportSize)`.

  Its serialized fields are `root`, `viewport`, `content`, `deckName`, `upArrow`, `downArrow`, `currentTint` and `pulseSpeed`.
- **Ruling (spec deviation):** the spec names a new `MapView`. This plan reworks the existing `MapScreenView` instead, which keeps the prefab's component and its `mapScreenView` reference on `MapTabController`. The spec is updated to match.

- [ ] **Step 1: Write the failing test** `MapScreenViewTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.Tests
{
    public sealed class MapScreenViewTests
    {
        private GameObject host = null!;
        private RectTransform content = null!;
        private GameObject up = null!, down = null!;
        private TextMeshProUGUI label = null!;
        private MapScreenView view = null!;

        [SetUp]
        public void SetUp()
        {
            this.host = new GameObject("Host", typeof(RectTransform));
            var root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(this.host.transform, false);

            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false);
            viewport.sizeDelta = new Vector2(100f, 100f);

            this.content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            this.content.SetParent(viewport, false);

            this.label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            this.label.transform.SetParent(root.transform, false);
            this.up   = new GameObject("Up");
            this.down = new GameObject("Down");
            this.up.transform.SetParent(root.transform, false);
            this.down.transform.SetParent(root.transform, false);

            this.view = this.host.AddComponent<MapScreenView>();
            var so = new SerializedObject(this.view);
            so.FindProperty("root").objectReferenceValue      = root;
            so.FindProperty("viewport").objectReferenceValue  = viewport;
            so.FindProperty("content").objectReferenceValue   = this.content;
            so.FindProperty("deckName").objectReferenceValue  = this.label;
            so.FindProperty("upArrow").objectReferenceValue   = this.up;
            so.FindProperty("downArrow").objectReferenceValue = this.down;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.host);

        private static MapRoomVisual Visual(int x, int y, int turns = 0, bool current = false)
            => new("r", MapTestData.Sprite(10, 10), new Vector2Int(x, y), turns, current);

        private RectTransform Room(int i) => (RectTransform)this.content.GetChild(i);

        [Test]
        public void Show_placesOneImagePerVisual_relativeToBoundsCentre()
        {
            this.view.Show(new[] { Visual(0, 0), Visual(20, 0) }, "DECK B", false, false);

            Assert.AreEqual(2, this.content.childCount);
            Assert.AreEqual(new Vector2(-10f, 0f), Room(0).anchoredPosition);
            Assert.AreEqual(new Vector2(10f, 0f), Room(1).anchoredPosition);
            Assert.AreEqual(new Vector2(10f, 10f), Room(0).sizeDelta);
            Assert.IsNotNull(Room(0).GetComponent<Image>().sprite);
            Assert.IsTrue(this.view.IsVisible);
        }

        [Test]
        public void Show_again_hidesSurplusImages()
        {
            this.view.Show(new[] { Visual(0, 0), Visual(20, 0) }, "DECK B", false, false);
            this.view.Show(new[] { Visual(0, 0) }, "DECK B", false, false);

            Assert.IsTrue(Room(0).gameObject.activeSelf);
            Assert.IsFalse(Room(1).gameObject.activeSelf);
        }

        [Test]
        public void Show_rotatesByQuarterTurns()
        {
            this.view.Show(new[] { Visual(0, 0, turns: 1) }, "DECK B", false, false);
            Assert.AreEqual(90f, Room(0).localEulerAngles.z, 0.01f);
        }

        [Test]
        public void Show_setsHeaderAndArrows()
        {
            this.view.Show(new[] { Visual(0, 0) }, "DECK C", hasUp: true, hasDown: false);
            Assert.AreEqual("DECK C", this.label.text);
            Assert.IsTrue(this.up.activeSelf);
            Assert.IsFalse(this.down.activeSelf);
        }

        [Test]
        public void Show_nonCurrentRooms_areWhite()
        {
            this.view.Show(new[] { Visual(0, 0), Visual(20, 0, current: true) }, "DECK B", false, false);
            Assert.AreEqual(Color.white, Room(0).GetComponent<Image>().color);
        }

        [Test]
        public void Hide_deactivatesRoot()
        {
            this.view.Show(new[] { Visual(0, 0) }, "DECK B", false, false);
            this.view.Hide();
            Assert.IsFalse(this.view.IsVisible);
        }

        [Test]
        public void FitScale_onlyShrinks()
        {
            Assert.AreEqual(0.5f, MapScreenView.FitScale(new Vector2(200f, 100f), new Vector2(100f, 100f)), 0.0001f);
            Assert.AreEqual(1f,   MapScreenView.FitScale(new Vector2(50f, 50f),   new Vector2(100f, 100f)), 0.0001f);
            Assert.AreEqual(1f,   MapScreenView.FitScale(Vector2.zero,             new Vector2(100f, 100f)), 0.0001f);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails.**
  - Run: `ut.sh CrimsonDraft.Tests.MapScreenViewTests`.
  - Expected: COMPILE FAILED, because `Show` has the old signature and `FitScale` does not exist.

- [ ] **Step 3: Implement `MapScreenView`.** Replace the whole file:

```csharp
#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Navigation.UI
{
    /// <summary>Draws one floor of the map as room sprites, centred on the floor's bounds and
    /// shrunk to fit the viewport. The player's current room tints back and forth.</summary>
    public sealed class MapScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject      root      = null!;
        [SerializeField] private RectTransform   viewport  = null!;
        [SerializeField] private RectTransform   content   = null!;
        [SerializeField] private TextMeshProUGUI deckName  = null!;
        [SerializeField] private GameObject      upArrow   = null!;
        [SerializeField] private GameObject      downArrow = null!;

        [Header("Current room")]
        [SerializeField] private Color currentTint = new(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private float pulseSpeed  = 3f;

        private readonly List<Image> images = new();
        private Image? currentImage;

        public bool IsVisible => this.root.activeSelf;

        public void Show(IReadOnlyList<MapRoomVisual> visuals, string floorName, bool hasUp, bool hasDown)
        {
            this.root.SetActive(true);
            this.deckName.text = floorName;
            this.upArrow.SetActive(hasUp);
            this.downArrow.SetActive(hasDown);

            var bounds = MapLayoutBounds.Compute(visuals);
            this.currentImage = null;

            for (int i = 0; i < visuals.Count; i++)
            {
                var visual = visuals[i];
                var image  = Acquire(i);
                var rect   = image.rectTransform;

                image.sprite           = visual.Sprite;
                image.color            = Color.white;
                rect.sizeDelta         = visual.Sprite.rect.size;
                rect.anchoredPosition  = (Vector2)visual.Position - bounds.center;
                rect.localEulerAngles  = new Vector3(0f, 0f, visual.QuarterTurns * 90f);

                if (visual.IsCurrent)
                    this.currentImage = image;
            }

            for (int i = visuals.Count; i < this.images.Count; i++)
                this.images[i].gameObject.SetActive(false);

            float scale = FitScale(bounds.size, this.viewport.rect.size);
            this.content.localScale = new Vector3(scale, scale, 1f);
        }

        public void Hide()
        {
            this.root.SetActive(false);
            this.currentImage = null;
        }

        public static float FitScale(Vector2 contentSize, Vector2 viewportSize)
        {
            if (contentSize.x <= 0f || contentSize.y <= 0f)
                return 1f;

            return Mathf.Min(1f, viewportSize.x / contentSize.x, viewportSize.y / contentSize.y);
        }

        private void Update()
        {
            if (this.currentImage == null)
                return;

            float t = (Mathf.Sin(Time.unscaledTime * this.pulseSpeed) + 1f) * 0.5f;
            this.currentImage.color = Color.Lerp(Color.white, this.currentTint, t);
        }

        private Image Acquire(int index)
        {
            if (index < this.images.Count)
            {
                this.images[index].gameObject.SetActive(true);
                return this.images[index];
            }

            var go = new GameObject($"Room_{index}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(this.content, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            this.images.Add(image);
            return image;
        }
    }
}
```

- [ ] **Step 4: Implement `MapTabController`.** Replace the whole file:

```csharp
#nullable enable

using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Navigation.Map;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.UI
{
    /// <summary>Drives the MAP tab: opens on the player's floor and steps between the
    /// available floors with the vertical axis (one step per press). Cancel is handled
    /// centrally by TabManager; nothing here reacts while the tab bar is active.</summary>
    public sealed class MapTabController : MonoBehaviour
    {
        [SerializeField] private MapScreenView mapScreenView = null!;

        [Inject] private IInputService     inputService     = null!;
        [Inject] private TabManager        tabManager       = null!;
        [Inject] private MapSceneConfig    sceneConfig      = null!;
        [Inject] private MapDataSet        mapSet           = null!;
        [Inject] private IRoomOrchestrator roomOrchestrator = null!;
        [Inject] private RoomStateRegistry rooms            = null!;
        [Inject] private PickupRegistry    pickups          = null!;
        [Inject] private KnownMapsRegistry knownMaps        = null!;

        private readonly AxisStepper stepper = new();
        private MapFloors? floors;

        void OnEnable()
        {
            if (this.inputService == null) return;

            this.floors = new MapFloors(
                MapFloors.Available(this.mapSet.Maps, this.rooms, this.knownMaps),
                this.sceneConfig != null ? this.sceneConfig.Map : null);
            this.stepper.Reset();
            ShowCurrentFloor();
        }

        void OnDisable()
        {
            this.mapScreenView?.Hide();
            this.floors = null;
        }

        void Update()
        {
            if (this.tabManager == null || this.floors == null) return;

            if (this.tabManager.IsTabBarActive)
            {
                this.stepper.Reset();
                return;
            }

            int step = this.stepper.Update(this.inputService.InventoryNavigate.ReadValue<Vector2>().y);
            if (step != 0 && this.floors.Step(step))
                ShowCurrentFloor();
        }

        private void ShowCurrentFloor()
        {
            var map = this.floors?.Current;
            if (map == null)
            {
                this.mapScreenView.Hide();
                return;
            }

            string? currentRoomId = ReferenceEquals(map, this.sceneConfig.Map)
                ? this.roomOrchestrator.CurrentRoom?.RoomId
                : null;

            var visuals = MapRoomVisuals.Resolve(map, this.rooms, this.pickups, this.knownMaps, currentRoomId);
            this.mapScreenView.Show(visuals, map.DisplayName, this.floors!.HasUp, this.floors.HasDown);
        }
    }
}
```

- [ ] **Step 5: Run.**
  - Run: `ut.sh CrimsonDraft.Tests.MapScreenViewTests`.
  - Expected: `total=7 passed=7 new=0`. `MapRenderer.cs` still compiles here (it uses `ResolveRoom`, which is only removed in Step 7), so Step 6 can still find the component by type.

- [ ] **Step 6: Prefab edit (while `MapRenderer` still compiles).**

Write this eval file to the scratchpad and run it with `unity command eval_file --file <path>`:

```csharp
var path = "Assets/Prefabs/Core/__GAMEPLAYCORE.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
var log  = new System.Text.StringBuilder();
try
{
    foreach (var r in root.GetComponentsInChildren<CrimsonDraft.Navigation.Map.MapRenderer>(true))
    {
        log.AppendLine("destroy " + r.gameObject.name);
        UnityEngine.Object.DestroyImmediate(r.gameObject);
    }

    var view = root.GetComponentInChildren<CrimsonDraft.Navigation.UI.MapScreenView>(true);
    var so   = new UnityEditor.SerializedObject(view);
    var mapImage = view.transform.Find("MapImage");
    var raw = mapImage.GetComponent<UnityEngine.UI.RawImage>();
    if (raw != null) UnityEngine.Object.DestroyImmediate(raw);
    if (mapImage.GetComponent<UnityEngine.UI.RectMask2D>() == null) mapImage.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();

    var content = mapImage.Find("Content") as UnityEngine.RectTransform;
    if (content == null)
    {
        content = new UnityEngine.GameObject("Content", typeof(UnityEngine.RectTransform)).GetComponent<UnityEngine.RectTransform>();
        content.SetParent(mapImage, false);
    }
    content.anchorMin = content.anchorMax = content.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
    content.anchoredPosition = UnityEngine.Vector2.zero;
    content.sizeDelta = UnityEngine.Vector2.zero;

    var deck = view.transform.Find("DeckNameText");
    UnityEngine.GameObject Arrow(string name, string glyph, float dy)
    {
        var existing = view.transform.Find(name);
        var go = existing != null ? existing.gameObject : UnityEngine.Object.Instantiate(deck.gameObject, view.transform);
        go.name = name;
        var rt = (UnityEngine.RectTransform)go.transform;
        rt.anchoredPosition = ((UnityEngine.RectTransform)deck).anchoredPosition + new UnityEngine.Vector2(0f, dy);
        go.GetComponent<TMPro.TextMeshProUGUI>().text = glyph;
        return go;
    }
    var up   = Arrow("UpArrow", "▲", 28f);
    var down = Arrow("DownArrow", "▼", -28f);

    so.FindProperty("viewport").objectReferenceValue  = mapImage;
    so.FindProperty("content").objectReferenceValue   = content;
    so.FindProperty("upArrow").objectReferenceValue   = up;
    so.FindProperty("downArrow").objectReferenceValue = down;
    so.ApplyModifiedPropertiesWithoutUndo();

    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
    log.AppendLine("viewport=" + mapImage.name + " content=" + content.name + " up=" + up.name + " down=" + down.name);
}
finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
return log.ToString();
```

Expected output:

```
destroy MapRenderer
viewport=MapImage content=Content up=UpArrow down=DownArrow
```

Re-load the prefab in a second eval and print `MapScreenView`'s serialized refs. Expected: all six refs are non-null, and there is no `MapRenderer` in the hierarchy. If `MapImage` or `DeckNameText` is not a direct child of the `MapScreenView` object, locate it by name with `GetComponentsInChildren<Transform>(true)` and ledger the ruling.

- [ ] **Step 7: Delete the old renderer.**
  - Delete `Navigation/Map/MapRenderer.cs` (+ `.meta`) and `Tests/EditMode/MapRendererTests.cs` (+ `.meta`).
  - In `NavigationScope.cs`, delete the line `builder.RegisterComponentInHierarchy<MapRenderer>();`.
  - In `MapStateResolver.cs`, delete the `MapRoomVisualState` enum and `ResolveRoom`.
  - In `MapStateResolverTests.cs`, delete the six `ResolveRoom_*` tests.
  - Run `grep -rn "MapRenderer\|MapRoomVisualState\|ResolveRoom" Game/CrimsonDraft/Assets/Scripts Game/CrimsonDraft/Assets/Tests`. Expected: no matches.

- [ ] **Step 8: Full suite.** `ut.sh`. Expected: `new=0`.

- [ ] **Step 9: Play Mode smoke.** `MapData` has no sprites yet, so the floor shows empty, with its header only.
  1. Run `unity command editor_play`.
  2. Open the inventory and go to the MAP tab, then run `capture_game_view --source screen` (decode it to the scratchpad).
  3. Expected: the header shows the floor name with ▲/▼ visible only where a floor exists; no exceptions with `console --level error`, apart from the pre-existing `MusicManagerController` / `WeatherAmbienceController` NREs.
  4. If ▲/▼ render as □ (glyph missing in the font), change them to `^` / `v` in the prefab and ledger the ruling.
  5. Run `editor_stop`, delete `Assets/Temp`, and restore the TMP fallback font if it is dirty.

---

### Task 3: Map Editor window + upsert bake

**Files:**
- Modify (rewrite): `Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/MapEditorWindow.cs`
- Modify (rewrite): `Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/MapBaker.cs`

**Interfaces:**
- **Consumes:** `MapLayoutMerge.Upsert`, `MapRoomVisuals.Preview`, `MapLayoutBounds`, `MapSpriteStandard`, `MapData.EditorSetBakedContent` and `MapData.EditorRemoveRoom` (all from Task 1).
- **Produces:** the menu item `Tools/CrimsonDraft/Map Editor`. `MapBaker.Bake(MapSceneConfig)` now upserts.

The editor code cannot be unit-tested beyond its pure helpers, which Task 1 covered. This task is verified by compiling and by an editor probe.

- [ ] **Step 1: Rewrite `MapBaker.cs`.** Keep the class header, `[InitializeOnLoad]`, the static constructor, `OnSceneSaved`, `OnPlayModeChanged` and `BakeAllInOpenScenes` exactly as they are. Replace `Bake` and add `Sorted`:

```csharp
        public static void Bake(MapSceneConfig config)
        {
            if (config.Map == null)
                return;

            var controllers = Object.FindObjectsByType<RoomController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var markers = Object.FindObjectsByType<MapDoorMarker>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var pickups = Object.FindObjectsByType<PickupInteractable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var pickupIds = new Dictionary<string, SortedSet<string>>();
            var doorIds   = new Dictionary<string, SortedSet<string>>();

            foreach (var controller in controllers)
            {
                if (string.IsNullOrWhiteSpace(controller.RoomId))
                {
                    Debug.LogWarning("[MapBaker] RoomController has no RoomId; it will not appear on the map.", controller);
                    continue;
                }

                pickupIds.TryAdd(controller.RoomId, new SortedSet<string>());
                doorIds.TryAdd(controller.RoomId, new SortedSet<string>());
            }

            var doors = new List<MapDoorData>(markers.Length);
            foreach (var marker in markers)
            {
                if (marker.ExcludeFromMap)
                    continue;

                var doorId = marker.ResolveDoorId();
                if (string.IsNullOrWhiteSpace(doorId))
                {
                    Debug.LogWarning("[MapBaker] MapDoorMarker is missing a DoorId.", marker);
                    continue;
                }

                doors.Add(new MapDoorData
                {
                    DoorId = doorId!,
                    Transform = new MapElementTransform
                    {
                        Offset = marker.MapOffset,
                        Rotation = marker.MapRotation,
                        Scale = Vector2.one,
                        ZOrder = 0f,
                    },
                    Size = marker.Size,
                });

                var room = marker.GetComponentInParent<RoomController>();
                if (room != null && doorIds.TryGetValue(room.RoomId, out var set))
                    set.Add(doorId!);
            }

            foreach (var pickup in pickups)
            {
                if (string.IsNullOrWhiteSpace(pickup.PickupId))
                    continue;

                var room = pickup.GetComponentInParent<RoomController>();
                if (room != null && pickupIds.TryGetValue(room.RoomId, out var set))
                    set.Add(pickup.PickupId);
            }

            var baked = pickupIds.Keys
                .OrderBy(id => id, System.StringComparer.Ordinal)
                .Select(id => new MapRoomData
                {
                    RoomId    = id,
                    PickupIds = pickupIds[id].ToArray(),
                    DoorIds   = doorIds[id].ToArray(),
                })
                .ToList();

            var rooms = MapLayoutMerge.Upsert(config.Map.Rooms, baked);
            config.Map.EditorSetBakedContent(rooms, doors.OrderBy(d => d.DoorId, System.StringComparer.Ordinal).ToList());
            EditorUtility.SetDirty(config.Map);
            AssetDatabase.SaveAssets();
        }
```

Add `using System.Linq;` and keep the existing usings. `MapRoomShape` is no longer referenced here.

- [ ] **Step 2: Rewrite `MapEditorWindow.cs`.** Replace the whole file:

```csharp
#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Lays out a floor's room sprites on a free canvas. Edits the MapData asset only
    /// (never the scene): sprites, pixel position and 90° rotation. Rooms and their pickup/door
    /// ids come from the scene bake (MapBaker); rooms no longer in the scene show as orphans.</summary>
    public sealed class MapEditorWindow : EditorWindow
    {
        private const string SnapPrefKey = "CrimsonDraft.MapEditor.Snap";
        private const float  ListWidth   = 280f;
        private const float  MinZoom     = 0.1f;
        private const float  MaxZoom     = 8f;
        private static readonly int[]    SnapOptions = { 1, 4, 8 };
        private static readonly string[] SnapLabels  = { "Snap 1px", "Snap 4px", "Snap 8px" };

        [SerializeField] private MapData? map;
        [SerializeField] private string   selectedRoomId = "";
        [SerializeField] private Vector2  pan;
        [SerializeField] private float    zoom = 1f;
        [SerializeField] private int      snap = 1;
        [SerializeField] private bool     previewComplete;
        [SerializeField] private Vector2  listScroll;

        private bool       dragging;
        private Vector2    dragStartMouse;
        private Vector2Int dragStartPosition;

        [MenuItem("Tools/CrimsonDraft/Map Editor")]
        public static void Open()
        {
            var window = GetWindow<MapEditorWindow>("Map Editor");
            window.minSize = new Vector2(720f, 420f);
        }

        private void OnEnable()
        {
            this.snap = EditorPrefs.GetInt(SnapPrefKey, this.snap);
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable() => Undo.undoRedoPerformed -= Repaint;

        private void OnGUI()
        {
            DrawToolbar();

            if (this.map == null)
            {
                EditorGUILayout.HelpBox("Pick a MapData asset in the toolbar.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawRoomPanel(this.map);
            var canvas = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndHorizontal();

            GUI.BeginGroup(canvas);
            var local = new Rect(Vector2.zero, canvas.size);
            HandleCanvasInput(this.map, local);
            DrawCanvas(this.map, local);
            GUI.EndGroup();
        }

        // ---------- Toolbar ----------

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var maps   = AssetDatabase.FindAssets("t:MapData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<MapData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(m => m != null)
                .ToArray();
            int index  = System.Array.IndexOf(maps, this.map);
            int picked = EditorGUILayout.Popup(index, maps.Select(m => m.name).ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(200f));
            if (picked != index && picked >= 0)
            {
                this.map = maps[picked];
                this.selectedRoomId = "";
            }

            this.previewComplete = GUILayout.Toggle(this.previewComplete, this.previewComplete ? "Preview: Complete" : "Preview: Incomplete", EditorStyles.toolbarButton, GUILayout.Width(130f));

            int snapIndex = Mathf.Max(0, System.Array.IndexOf(SnapOptions, this.snap));
            EditorGUI.BeginChangeCheck();
            snapIndex = EditorGUILayout.Popup(snapIndex, SnapLabels, EditorStyles.toolbarPopup, GUILayout.Width(80f));
            if (EditorGUI.EndChangeCheck())
            {
                this.snap = SnapOptions[snapIndex];
                EditorPrefs.SetInt(SnapPrefKey, this.snap);
            }

            if (GUILayout.Button("Frame", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                Frame();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void Frame()
        {
            if (this.map == null) return;
            var bounds = MapLayoutBounds.Compute(MapRoomVisuals.Preview(this.map, this.previewComplete));
            this.pan = new Vector2(-bounds.center.x, bounds.center.y) * this.zoom;
        }

        // ---------- Room list + inspector ----------

        private void DrawRoomPanel(MapData target)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));
            var standard = MapSpriteStandard.Majority(target.Rooms.SelectMany(r => new[] { r.IncompleteSprite, r.CompleteSprite }));

            this.listScroll = EditorGUILayout.BeginScrollView(this.listScroll, GUILayout.ExpandHeight(true));
            foreach (var room in target.Rooms)
            {
                var style = room.RoomId == this.selectedRoomId ? EditorStyles.boldLabel : EditorStyles.label;
                if (GUILayout.Button($"{Status(room, standard)}  {room.RoomId}", style))
                    this.selectedRoomId = room.RoomId;
            }
            EditorGUILayout.EndScrollView();

            var selected = target.Rooms.FirstOrDefault(r => r.RoomId == this.selectedRoomId);
            if (selected != null)
                DrawRoomInspector(target, selected, standard);

            EditorGUILayout.EndVertical();
        }

        private static string Status(MapRoomData room, (float PixelsPerUnit, FilterMode Filter)? standard)
        {
            if (room.IsOrphan) return "✖";
            if (room.IncompleteSprite == null || room.CompleteSprite == null) return "⚠";
            if (standard != null && (!MapSpriteStandard.Matches(room.IncompleteSprite, standard.Value) || !MapSpriteStandard.Matches(room.CompleteSprite, standard.Value))) return "⚠ PPU";
            return "✔";
        }

        private void DrawRoomInspector(MapData target, MapRoomData room, (float PixelsPerUnit, FilterMode Filter)? standard)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(room.RoomId, EditorStyles.boldLabel);
            if (room.IsOrphan)
                EditorGUILayout.HelpBox("Not found in the scene at the last bake.", MessageType.Warning);

            bool hadSprite = room.IncompleteSprite != null || room.CompleteSprite != null;

            EditorGUI.BeginChangeCheck();
            var incomplete = (Sprite?)EditorGUILayout.ObjectField("Incomplete", room.IncompleteSprite, typeof(Sprite), false);
            var complete   = (Sprite?)EditorGUILayout.ObjectField("Complete",   room.CompleteSprite,   typeof(Sprite), false);
            var position   = EditorGUILayout.Vector2IntField("Position", room.Position);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Edit map room");
                room.IncompleteSprite = incomplete;
                room.CompleteSprite   = complete;
                room.Position         = position;
                if (!hadSprite && (incomplete != null || complete != null))
                    room.Position = VisibleCentre();
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⟲ 90°")) Rotate(target, room, +1);
            if (GUILayout.Button("⟳ 90°")) Rotate(target, room, -1);
            EditorGUILayout.EndHorizontal();

            if (standard != null)
            {
                FixButton(room.IncompleteSprite, standard.Value);
                FixButton(room.CompleteSprite, standard.Value);
            }

            if (room.IsOrphan && GUILayout.Button("Delete orphan"))
            {
                Undo.RecordObject(target, "Delete map room");
                target.EditorRemoveRoom(room.RoomId);
                this.selectedRoomId = "";
                EditorUtility.SetDirty(target);
            }
        }

        private static void Rotate(MapData target, MapRoomData room, int quarterTurns)
        {
            Undo.RecordObject(target, "Rotate map room");
            room.QuarterTurns = ((room.QuarterTurns + quarterTurns) % 4 + 4) % 4;
            EditorUtility.SetDirty(target);
        }

        private static void FixButton(Sprite? sprite, (float PixelsPerUnit, FilterMode Filter) standard)
        {
            if (sprite == null || MapSpriteStandard.Matches(sprite, standard))
                return;

            if (!GUILayout.Button($"Fix {sprite.name} → {standard.PixelsPerUnit} PPU, {standard.Filter}"))
                return;

            var path = AssetDatabase.GetAssetPath(sprite);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return;

            importer.spritePixelsPerUnit = standard.PixelsPerUnit;
            importer.filterMode          = standard.Filter;
            importer.SaveAndReimport();
            Debug.LogWarning($"[MapEditor] {path}: set to {standard.PixelsPerUnit} PPU, {standard.Filter} to match the floor.", sprite);
        }

        // ---------- Canvas ----------

        private Vector2 ToScreen(Rect canvas, Vector2 map)
            => canvas.center + this.pan + new Vector2(map.x, -map.y) * this.zoom;

        private Vector2 ToMap(Rect canvas, Vector2 screen)
        {
            var v = (screen - canvas.center - this.pan) / this.zoom;
            return new Vector2(v.x, -v.y);
        }

        private Vector2Int VisibleCentre()
        {
            var centre = new Vector2(-this.pan.x, this.pan.y) / this.zoom;
            return Snap(centre);
        }

        private Vector2Int Snap(Vector2 p)
            => new(Mathf.RoundToInt(p.x / this.snap) * this.snap, Mathf.RoundToInt(p.y / this.snap) * this.snap);

        private Rect ScreenRect(Rect canvas, MapRoomVisual visual)
        {
            var size   = MapLayoutBounds.SizeOf(visual.Sprite, visual.QuarterTurns) * this.zoom;
            var centre = ToScreen(canvas, visual.Position);
            return new Rect(centre - size * 0.5f, size);
        }

        private void DrawCanvas(MapData target, Rect canvas)
        {
            EditorGUI.DrawRect(canvas, new Color(0.13f, 0.13f, 0.13f));

            var visuals = MapRoomVisuals.Preview(target, this.previewComplete);
            foreach (var visual in visuals)
                DrawSprite(canvas, visual);

            foreach (var visual in visuals)
                if (visual.RoomId == this.selectedRoomId)
                    DrawOutline(ScreenRect(canvas, visual), Color.yellow);

            if (visuals.Count > 0)
            {
                var c = ToScreen(canvas, MapLayoutBounds.Compute(visuals).center);
                EditorGUI.DrawRect(new Rect(c.x - 6f, c.y - 0.5f, 12f, 1f), Color.cyan);
                EditorGUI.DrawRect(new Rect(c.x - 0.5f, c.y - 6f, 1f, 12f), Color.cyan);
            }
        }

        private void DrawSprite(Rect canvas, MapRoomVisual visual)
        {
            var sprite  = visual.Sprite;
            var size    = sprite.rect.size * this.zoom;
            var centre  = ToScreen(canvas, visual.Position);
            var rect    = new Rect(centre - size * 0.5f, size);
            var tex     = sprite.texture;
            var uv      = new Rect(
                sprite.textureRect.x / tex.width,
                sprite.textureRect.y / tex.height,
                sprite.textureRect.width / tex.width,
                sprite.textureRect.height / tex.height);

            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(-visual.QuarterTurns * 90f, centre);
            GUI.DrawTextureWithTexCoords(rect, tex, uv);
            GUI.matrix = matrix;
        }

        private static void DrawOutline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.yMin, 1f, rect.height), color);
        }

        private void HandleCanvasInput(MapData target, Rect canvas)
        {
            var e = Event.current;
            if (!canvas.Contains(e.mousePosition) && !this.dragging)
                return;

            switch (e.type)
            {
                case EventType.ScrollWheel:
                {
                    var before = ToMap(canvas, e.mousePosition);
                    this.zoom = Mathf.Clamp(this.zoom * (1f - e.delta.y * 0.05f), MinZoom, MaxZoom);
                    var after = ToScreen(canvas, before);
                    this.pan += e.mousePosition - after;
                    e.Use();
                    break;
                }
                case EventType.MouseDrag when e.button == 2:
                    this.pan += e.delta;
                    e.Use();
                    break;
                case EventType.MouseDown when e.button == 0:
                {
                    var hit = MapRoomVisuals.Preview(target, this.previewComplete)
                        .Reverse()
                        .FirstOrDefault(v => ScreenRect(canvas, v).Contains(e.mousePosition));
                    this.selectedRoomId = hit.Sprite != null ? hit.RoomId : "";
                    var room = target.Rooms.FirstOrDefault(r => r.RoomId == this.selectedRoomId);
                    if (room != null)
                    {
                        Undo.RecordObject(target, "Move map room");
                        this.dragging          = true;
                        this.dragStartMouse    = e.mousePosition;
                        this.dragStartPosition = room.Position;
                    }
                    e.Use();
                    break;
                }
                case EventType.MouseDrag when e.button == 0 && this.dragging:
                {
                    var room = target.Rooms.FirstOrDefault(r => r.RoomId == this.selectedRoomId);
                    if (room != null)
                    {
                        var delta = (e.mousePosition - this.dragStartMouse) / this.zoom;
                        var moved = Snap(this.dragStartPosition + new Vector2(delta.x, -delta.y));
                        if (moved != room.Position)
                        {
                            room.Position = moved;
                            EditorUtility.SetDirty(target);
                        }
                    }
                    e.Use();
                    break;
                }
                case EventType.MouseUp when e.button == 0:
                    this.dragging = false;
                    e.Use();
                    break;
            }

            if (e.type == EventType.Used)
                Repaint();
        }
    }
}
```

- [ ] **Step 3: Compile.**
  - Run: `ut.sh CrimsonDraft.Tests.MapLayoutMergeTests`.
  - Expected: it compiles and the class passes.
  - `MapRoomShapeEditor.cs` still compiles, because it references only `MapRoomShape`, which is still present.

- [ ] **Step 4: Editor probe.** Run an eval that does the following:
  1. Open the window with `CrimsonDraft.Navigation.Editor.MapEditorWindow.Open()`.
  2. Set `map` via reflection to `Assets/Data/Map/MapData_DeckB.asset`.
  3. Call `Repaint()`.
  4. Return `map.Rooms.Count` and the list of `RoomId`/`IsOrphan`.

  Expected: no exceptions, and the room count matches the asset. Then:
  - Run `console --level error --tail 20`. Expected: no new errors.
  - Close the window with `EditorWindow.GetWindow<MapEditorWindow>().Close()`.

- [ ] **Step 5: Full suite.** `ut.sh`. Expected: `new=0`.

---

### Task 4: Remove `MapRoomShape`, the polygon data and the old grid fields

**Files:**
- Prefabs: every prefab that contains `MapRoomShape`, 27 in total:
  - `Prefabs/World/Rooms/Deck_B/*.prefab`;
  - `Prefabs/World/Rooms/Deck_C/*.prefab`;
  - `Prefabs/World/Rooms/Room_PRF.prefab`;
  - `Prefabs/World/ProtoWalls/PrefabRooms/Port_Stairs.prefab`.
- Delete:
  - `Game/CrimsonDraft/Assets/Scripts/Navigation/Map/MapRoomShape.cs`;
  - `Game/CrimsonDraft/Assets/Scripts/Navigation/Editor/MapRoomShapeEditor.cs`;
  - `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map/PolygonTriangulator.cs`;
  - `Game/CrimsonDraft/Assets/Tests/EditMode/PolygonTriangulatorTests.cs`.

  Delete each one with its `.meta`.
- Modify:
  - `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Map/MapData.cs`;
  - `Game/CrimsonDraft/Assets/Tests/EditMode/MapDataTests.cs`.
- Assets: `Game/CrimsonDraft/Assets/Data/Map/MapData_DeckB.asset`, `MapData_DeckC.asset` (re-serialized)

**Interfaces:**
- **Produces:** `MapRoomData` without `Polygon` and `Transform`. `MapData` without `GridSize`, `CellSize` and `Center`. `MapElementTransform` stays, because doors use it.

- [ ] **Step 1: Update the failing test.** In `MapDataTests.MapData_defaults_are_empty_and_grid_is_sane`:
  - Rename it to `MapData_defaults_are_empty`.
  - Delete the `GridSize` and `CellSize` asserts.
  - Add a new test:

```csharp
        [Test]
        public void MapRoomData_defaults_haveNoLayout()
        {
            var room = new MapRoomData();
            Assert.IsNull(room.IncompleteSprite);
            Assert.IsNull(room.CompleteSprite);
            Assert.AreEqual(Vector2Int.zero, room.Position);
            Assert.AreEqual(0, room.QuarterTurns);
            Assert.IsFalse(room.IsOrphan);
            Assert.IsEmpty(room.PickupIds);
            Assert.IsEmpty(room.DoorIds);
        }
```

The new test passes immediately, because Task 1 added these fields. It is a characterization guard for the removal, not a RED test. Ledger that.

- [ ] **Step 2: Strip `MapRoomShape` from prefabs (while the class still exists).** Run this eval:

```csharp
var log = new System.Text.StringBuilder();
foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
{
    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    if (!System.IO.File.ReadAllText(path).Contains("6a2c6ff8d8ad4d1eb1e4f71d2f0a6c21")) continue;
    var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
    try
    {
        var shapes = root.GetComponentsInChildren<CrimsonDraft.Navigation.Map.MapRoomShape>(true);
        foreach (var s in shapes) UnityEngine.Object.DestroyImmediate(s);
        if (shapes.Length > 0) UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
        log.AppendLine($"{path}: removed {shapes.Length}");
    }
    finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
}
return log.ToString();
```

Expected: 27 lines, each with `removed 1`.

Then run `grep -rl 6a2c6ff8d8ad4d1eb1e4f71d2f0a6c21 Game/CrimsonDraft/Assets/Prefabs`. Expected: no output.

If a prefab line says `removed 0` but the grep still finds the guid, it is a nested-prefab override. Remove it with `PrefabUtility.RemoveUnusedOverrides` or by hand, and ledger the ruling.

- [ ] **Step 3: Delete code.**
  - Delete `MapRoomShape.cs`, `MapRoomShapeEditor.cs`, `PolygonTriangulator.cs` and `PolygonTriangulatorTests.cs`, each with its `.meta`.
  - In `MapData.cs`:
    - remove `MapRoomData.Polygon` and `MapRoomData.Transform`;
    - remove the `gridSize`, `cellSize` and `center` fields, their properties and the tooltip;
    - keep `MapElementTransform` and `MapDoorData`.
  - Run `grep -rn "MapRoomShape\|PolygonTriangulator\|GridSize\|CellSize\|\.Center\b\|\.Polygon\b" Game/CrimsonDraft/Assets/Scripts Game/CrimsonDraft/Assets/Tests`. Expected: no matches.

- [ ] **Step 4: Run.**
  - Run: `ut.sh CrimsonDraft.Tests.MapDataTests CrimsonDraft.Tests.MapLayoutMergeTests CrimsonDraft.Tests.MapRoomVisualsTests`.
  - Expected: all pass, with `new=0`.

- [ ] **Step 5: Re-serialize the map assets.** Run this eval:

```csharp
foreach (var p in new[] { "Assets/Data/Map/MapData_DeckB.asset", "Assets/Data/Map/MapData_DeckC.asset" })
    UnityEditor.EditorUtility.SetDirty(UnityEditor.AssetDatabase.LoadAssetAtPath<CrimsonDraft.Infrastructure.Map.MapData>(p));
UnityEditor.AssetDatabase.SaveAssets();
return "ok";
```

Then run `git diff --stat Game/CrimsonDraft/Assets/Data/Map`. Expected: `Polygon:`, `Transform:`, `gridSize`, `cellSize` and `center` are gone, while `RoomId`, `PickupIds` and `DoorIds` are unchanged.

- [ ] **Step 6: Bake idempotency on real data.**
  1. Copy both assets to the scratchpad.
  2. Run an eval that calls `CrimsonDraft.Navigation.Editor.MapBaker.Bake` for every `MapSceneConfig` found with `Object.FindObjectsByType<MapSceneConfig>(FindObjectsInactive.Include, FindObjectsSortMode.None)` in the open scene. This only reads the scene and writes the asset.
  3. Diff the assets against the copies. Expected: identical, or only new rooms appended with `IsOrphan: 0`. No `RoomId` loses its `PickupIds`.
  4. Run the bake a second time and diff again. Expected: no change.

- [ ] **Step 7: Full suite.** `ut.sh`. Expected: `new=0`.

---

### Task 5: End-to-end verification, spec sync, final review, commit

**Files:**
- Modify: `docs/superpowers/specs/2026-10-05-sprite-map-rework-design.md`, to sync the rulings:
  - `MapScreenView` is reworked instead of adding a new `MapView`;
  - `sizeDelta = sprite.rect.size` is used instead of `SetNativeSize`;
  - the orphan flag is stored as `MapRoomData.IsOrphan`;
  - the room materials that only `MapRenderer` used are left in the project, no longer referenced.

- [ ] **Step 1: Author a probe layout (temporary).** Use an eval to assign a test sprite to two Deck B rooms, which must include the player's starting room: any existing `Sprite` asset in the project. To find one, use `AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/UI" })`. Place the rooms at `(0,0)` and `(40,0)`. **Copy `MapData_DeckB.asset` first, so it can be restored in Step 3.**
- [ ] **Step 2: Play Mode.**
  1. Run `editor_play` and open the MAP tab, then take a screenshot.
  2. Expected:
     - the two sprites are centred;
     - the current room's tint changes between two screenshots taken a moment apart;
     - the header shows the floor name.
  3. Press down or up on the navigate axis via `unity command eval_file`. Otherwise, verify `MapFloors` behaviour in the tests and in the floor arrows. With only Deck B known, there is no step.
  4. Run `console --level error`. Expected: no new errors.
  5. Run `editor_stop`, delete `Assets/Temp`, and restore the TMP fallback font if it is dirty.
- [ ] **Step 3: Restore `MapData_DeckB.asset`** from the copy, so the probe sprites never ship. Then run `git diff Game/CrimsonDraft/Assets/Data/Map`. Expected: only the Task 4 field removals.
- [ ] **Step 4: Final review.** Run the whole-branch review, per the executing skill, with `git merge-base refactor/inventory HEAD` as the base. Then run the fix pass.
- [ ] **Step 5: Full suite.** `ut.sh`. Expected: `new=0`.
- [ ] **Step 6: Commit.**
  1. Run `git status`.
  2. Stage only:
     - the files listed in Tasks 1–5;
     - the deleted files;
     - the 27 room prefabs;
     - `__GAMEPLAYCORE.prefab`;
     - the two `MapData` assets;
     - the spec and plan docs.

     Do **not** stage `Art/UI/Boot*` or `_Recovery/`.
  3. Commit with:

```bash
git commit -m "feat(map): sprite-based map with asset editor and floor switching

Rooms draw an incomplete/complete sprite laid out in the new Map Editor
(MapData only, no scene). The scene bake upserts pickup/door ids without
touching layout. The MAP tab is centred, fits to the panel, tints the
current room and steps floors with the vertical axis. Removes MapRenderer,
its camera, MapRoomShape and the polygon pipeline."
```
