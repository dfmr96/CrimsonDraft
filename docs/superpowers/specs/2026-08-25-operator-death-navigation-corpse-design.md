# Operator Death Navigation Corpse — Design Spec

## Problem

When an operator dies in combat, the death is only reflected in combat-scene UI (dimmed portrait, flat ECG) and inside the Combat scene itself (`BattlefieldView.PlayOperatorDeath`). Once combat ends and the Combat scene unloads, there is no trace in Navigation that this operator died — the world gives no visual acknowledgement of the loss.

## Goal

When an operator dies (in a combat that may end in victory or defeat), a corpse — the operator's shared combat model, holding a death pose — appears in Navigation at the exact spot the player was standing when that combat happened. The corpse is permanent: it stays in that room for the rest of the playthrough, and survives save/load.

## Architecture

Combat and Navigation only communicate through `CombatEndedEvent` (published from `CombatOrchestrator`/`CombatSessionController`/`CombatMenuController`), which already fires regardless of victory or defeat. Rather than adding a new per-death event out of Combat, a new Navigation-side bootstrap subscribes to that existing event and diffs the operator roster's alive/dead state against what has already been recorded. This keeps Combat scripts completely untouched.

Persistence and room-scoping reuse the project's existing registry pattern (`EnemyStateRegistry`, `RoomStateRegistry`, `DoorStateRegistry`, all bundled in `WorldStateRegistries` and round-tripped through `SaveGameData`/`SaveController`/`SaveGameLoader`). A corpse is spawned as a child GameObject of the `RoomController` it belongs to; since `RoomController.Activate()`/`Deactivate()` already just toggles `gameObject.SetActive` on the whole room hierarchy, parenting the corpse there is sufficient to make it show/hide with the room — no separate spawn-on-room-transition listener is needed.

A single new prefab (shared by all four operators, matching the existing single shared `Ethan_Combat_FBX.prefab` used across all `OperatorData` assets) holds just the model and a minimal Animator Controller with one state, playing the `Rig|Soldier_Death_27` clip that already exists in the FBX but is currently unwired. It carries none of the combat-only components (`OperatorCombatAudio`, hit-fx markers, combat Animator params).

## Components

### New prefab (created via UnityMCP)

- `Assets/Prefabs/Characters/OperatorNavCorpse.prefab`
- Same skinned mesh as `Ethan_Combat_FBX.prefab`.
- New minimal Animator Controller (e.g. `Assets/Animations/OperatorCorpse_Controller.controller`) with a single state playing `Rig|Soldier_Death_27`, no parameters.
- No `OperatorCombatAudio`, no hit-fx marker components, no combat animator params (`Aim`/`Shoot`/`Flinch`/`Death` triggers) — those belong to the combat-only prefab.

### `OperatorCorpseRegistry` (new — `Assets/Scripts/Infrastructure/OperatorCorpseRegistry.cs`)

Mirrors `EnemyStateRegistry`'s shape but stores a richer entry per dead operator:

```csharp
public sealed class OperatorCorpseRegistry
{
    public readonly struct Entry
    {
        public int        SlotIndex { get; }
        public string     RoomId    { get; }
        public Vector3    Position  { get; }
        public Quaternion Rotation  { get; }

        public Entry(int slotIndex, string roomId, Vector3 position, Quaternion rotation)
        {
            SlotIndex = slotIndex;
            RoomId    = roomId;
            Position  = position;
            Rotation  = rotation;
        }
    }

    public bool IsRecorded(int slotIndex);
    public void Record(int slotIndex, string roomId, Vector3 position, Quaternion rotation);
    public IReadOnlyCollection<Entry> GetAll();
    public void LoadState(IEnumerable<Entry> saved);
    public void ClearAll();
}
```

### `SaveGameData` (`Assets/Scripts/Infrastructure/Save/SaveGameData.cs`)

New serializable entry type and list, following the existing `RoomStateEntry`/`DoorStateEntry` pattern:

```csharp
[Serializable]
public sealed class OperatorCorpseEntry
{
    public int        slotIndex;
    public string     roomId = "";
    public Vector3    position;
    public Quaternion rotation = Quaternion.identity;
}
```

```csharp
public List<OperatorCorpseEntry> operatorCorpses = new List<OperatorCorpseEntry>();
```

### `WorldStateRegistries` (`Assets/Scripts/Infrastructure/Save/WorldStateRegistries.cs`)

Add `OperatorCorpses` alongside the existing five registries, same constructor-injection pattern.

### `OperatorCorpseSettings` (new — `Assets/Scripts/Navigation/OperatorCorpseSettings.cs`, ScriptableObject)

Tiny asset holding just the prefab reference, following the same "plain data asset registered as a DI instance" pattern as `CombatSfxData` in `CombatScope`:

```csharp
[CreateAssetMenu(menuName = "CrimsonDraft/Operator Corpse Settings")]
public sealed class OperatorCorpseSettings : ScriptableObject
{
    [SerializeField] private GameObject corpsePrefab = null!;
    public GameObject CorpsePrefab => this.corpsePrefab;
}
```

Registered in `NavigationScope` via `builder.RegisterInstance(this.corpseSettings)` (a `[SerializeField]` on the scope, same as `CombatScope.sfxData`).

### `OperatorCorpseSpawner` (new — `Assets/Scripts/Navigation/OperatorCorpseSpawner.cs`)

Pure C# service, registered in `NavigationScope`, constructor-injected with `OperatorCorpseSettings`:

```csharp
public sealed class OperatorCorpseSpawner
{
    private readonly OperatorCorpseSettings settings;

    public OperatorCorpseSpawner(OperatorCorpseSettings settings) => this.settings = settings;

    public void Spawn(RoomController room, Vector3 position, Quaternion rotation)
        => Object.Instantiate(this.settings.CorpsePrefab, position, rotation, room.transform);
}
```

Used both by fresh-death capture and save-game restore, so instantiation logic exists in exactly one place.

### `OperatorCorpseBootstrap` (new — `Assets/Scripts/Navigation/OperatorCorpseBootstrap.cs`, `IInitializable`)

Constructed with `IOperatorRoster`, `IRoomOrchestrator`, `PlayerController`, `ISubscriber<CombatEndedEvent>`, `OperatorCorpseRegistry`, `OperatorCorpseSpawner`.

```csharp
void IInitializable.Initialize()
{
    this.combatEndedSubscriber.Subscribe(OnCombatEnded);
}

private void OnCombatEnded(CombatEndedEvent ev)
{
    var room = this.roomOrchestrator.CurrentRoom;
    if (room == null) return;

    for (int i = 0; i < this.roster.Count; i++)
    {
        if (this.roster[i].IsAlive) continue;
        if (this.registry.IsRecorded(i)) continue;

        Vector3    pos = this.player.transform.position;
        Quaternion rot = this.player.transform.rotation;

        this.registry.Record(i, room.RoomId, pos, rot);
        this.spawner.Spawn(room, pos, rot);
    }
}
```

### `SaveGameLoader` (`Assets/Scripts/Navigation/SaveGameLoader.cs`)

After the existing registry restores (`ApplyDoors`/`ApplyRooms`/etc.), add a method following the same shape as `ApplyDoors`/`ApplyRooms`:

```csharp
private void ApplyOperatorCorpses(SaveGameData data)
{
    var entries = new List<OperatorCorpseRegistry.Entry>();
    foreach (var e in data.operatorCorpses)
        entries.Add(new OperatorCorpseRegistry.Entry(e.slotIndex, e.roomId, e.position, e.rotation));
    this.world.OperatorCorpses.LoadState(entries);

    var rooms = Object.FindObjectsOfType<RoomController>(true);
    foreach (var entry in entries)
    {
        RoomController? room = System.Array.Find(rooms, r => r.RoomId == entry.RoomId);
        if (room == null)
        {
            Debug.LogWarning($"[SaveGameLoader] No room '{entry.RoomId}' for saved operator corpse (slot {entry.SlotIndex}).");
            continue;
        }
        this.corpseSpawner.Spawn(room, entry.Position, entry.Rotation);
    }
}
```

Called from `Initialize()` alongside the other `Apply*` calls. Runs regardless of which room is currently active — instantiating under an inactive room's transform just means the corpse spawns inactive too, matching the rest of the room's contents. Same `FindObjectsOfType` approach `RoomOrchestrator.ActivateRoomImmediate` already uses to look up a room by id.

### `SaveController` (`Assets/Scripts/Navigation/Interactables/UI/SaveController.cs`)

Alongside the existing registry dumps (~line 108), following the same shape as `data.rooms.Add(new RoomStateEntry { ... })`:

```csharp
foreach (var entry in this.world.OperatorCorpses.GetAll())
{
    data.operatorCorpses.Add(new OperatorCorpseEntry
    {
        slotIndex = entry.SlotIndex,
        roomId    = entry.RoomId,
        position  = entry.Position,
        rotation  = entry.Rotation,
    });
}
```

### `GameStateResetter`

Add `world.OperatorCorpses.ClearAll()` alongside the other registries' resets for New Game.

### DI registration order (`NavigationScope.cs`)

`OperatorCorpseBootstrap` must be registered **after** `SaveGameLoader`, mirroring the existing fix applied to `EnemyBootstrap` for the same reason: on a loaded save, the registry must already reflect previously-recorded deaths before this bootstrap starts listening for new ones, so a restored death is never re-recorded/re-spawned as if it were new.

## Data Flow

1. Player enters a room, triggers combat; Navigation scene stays loaded underneath, `PlayerController`'s transform stays put at the point combat started.
2. Combat plays out in the additively-loaded Combat scene; an operator's HP reaches 0 — `CombatOrchestrator.MarkDead` flags them, `BattlefieldView.PlayOperatorDeath` plays the combat-only death reaction. None of this touches Navigation.
3. Combat ends (`CombatEndedEvent`, victory or defeat) → Combat scene unloads.
4. `OperatorCorpseBootstrap.OnCombatEnded` scans the roster; for each newly-dead, not-yet-recorded slot, it records `{slotIndex, roomOrchestrator.CurrentRoom.RoomId, player position/rotation}` in `OperatorCorpseRegistry` and spawns the corpse prefab as a child of that room's `RoomController`.
5. Leaving and re-entering that room is just `RoomController.Deactivate()`/`Activate()` — the corpse, being a child of the room, is hidden/shown automatically with everything else in it.
6. On save, `SaveController` dumps `OperatorCorpseRegistry.GetAll()` into `SaveGameData.operatorCorpses`.
7. On load, `SaveGameLoader` restores the registry and re-instantiates each recorded corpse under its matching room by `roomId`, before the player can trigger any new combat.

## Edge Cases

- **Same operator "dies again" in a later combat:** impossible — a dead operator is permanently dead (`IsAlive` requires `Hp > 0`, and dead operators are excluded from all future combat participation per the existing ATB/roster rules). `IsRecorded(slot)` guards this regardless.
- **Two operators die in the same combat:** both are captured in the same `OnCombatEnded` pass; both corpses spawn at the same player position (acceptable — narratively they fell together; no offset logic is added since it's easy to tune visually later via the prefab or a small serialized offset if needed).
- **Party wipe (all operators die):** `CombatEndedEvent` for the defeat path is a pre-existing gap in the codebase (`CombatSessionController.EndCombat(false)` has no caller today) — out of scope for this feature. If/when that gap is fixed, this feature needs no changes: it already reacts to `CombatEndedEvent` regardless of the `Victory` flag.
- **Loading a save into a room that no longer exists / roomId typo:** `RestoreOperatorCorpses` skips entries whose `roomId` matches no `RoomController`, logging a warning — same defensive style as `RoomOrchestrator.ActivateRoomImmediate`.
- **New Game after a previous playthrough had corpses:** `GameStateResetter.ClearAll()` wipes the registry; no corpse prefabs exist yet in a fresh scene load so nothing needs destroying.

## Testing

- `OperatorCorpseRegistry`: EditMode tests for `Record`/`IsRecorded`/`LoadState`/`ClearAll`, following the plain-C#-fake style already used for the other registries.
- `OperatorCorpseBootstrap`: EditMode tests using a `FakeOperatorRoster`, fake `IRoomOrchestrator`, and a fake spawner/registry — verify it records+spawns exactly once per newly-dead slot, ignores already-recorded slots, and ignores alive slots, matching the existing `CombatMenuControllerTests`/`SaveGameLoaderTests` fake-based pattern.
- Prefab/Animator Controller and the room-child spawn behavior are Unity-asset/MonoBehaviour integration concerns, verified manually in Play Mode (matching the project's existing testing boundary for scene-dependent MonoBehaviours like `RoomController.Activate()`/`EnemyNavAgent`): kill an operator in combat, confirm the corpse appears at the right spot on return to Navigation, leave and re-enter the room to confirm it persists, save and reload to confirm it's restored.
