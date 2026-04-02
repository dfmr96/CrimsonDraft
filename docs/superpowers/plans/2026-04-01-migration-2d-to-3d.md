# Migration 2D → 3D Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate Navigation and Combat scenes from 2D (orthographic, Rigidbody2D, Tilemap) to 3D top-down (perspective, Rigidbody, Plane, capsule primitives).

**Architecture:** Scripts are already 3D — all remaining work is in Unity Editor: Player prefab component swap, Navigation scene camera/floor/trigger migration, Combat scene camera migration. All changes are done via Unity MCP tools against the open Editor.

**Tech Stack:** Unity 6, Cinemachine, URP, Unity MCP tools.

---

## Current State (verified 2026-04-01)

Scripts already migrated (no script work needed):
- `Assets/Scripts/Navigation/Player/PlayerController.cs` — uses `Rigidbody`, `linearVelocity`, XZ plane ✅
- `Assets/Scripts/Navigation/Combat/CombatTrigger.cs` — uses `OnTriggerEnter(Collider)` ✅
- `Assets/Scripts/Combat/UI/BattlefieldView.cs` — uses `MeshRenderer`, `CreatePrimitive(Capsule)` ✅

Still 2D (work needed):
- `Assets/Prefabs/Characters/Player.prefab` — has `Rigidbody2D`, `CapsuleCollider2D`, `SpriteRenderer`
- `Assets/Scenes/Navigation.unity` — orthographic camera, `PixelPerfectCamera`, `CinemachinePixelPerfect`, `Tilemap`, 2D collider on trigger
- `Assets/Scenes/Combat.unity` — orthographic camera, `PixelPerfectCamera`, `CinemachinePixelPerfect`, `Tilemap`

---

## File Map

| File | Action | What changes |
|---|---|---|
| `Assets/Prefabs/Characters/Player.prefab` | Modify | Remove `Rigidbody2D`, `CapsuleCollider2D`, `SpriteRenderer`, `Animator`; add `Rigidbody` + `CapsuleCollider` + `MeshRenderer` |
| `Assets/Scenes/Navigation.unity` | Modify (via MCP) | Camera → perspective FOV 60; CinemachineCamera → remove PixelPerfect, set offset (0,15,0) rot (90,0,0); Grid/Tilemap → Plane primitive with MeshCollider; EnemyEncounterTrigger → BoxCollider 3D trigger |
| `Assets/Scenes/Combat.unity` | Modify (via MCP) | Camera → perspective FOV 60; remove PixelPerfectCamera; CinemachineCamera → remove PixelPerfect extension |

---

## Task 1: Player Prefab — Swap 2D → 3D Physics Components

**Files:**
- Modify: `Assets/Prefabs/Characters/Player.prefab`

**Context:** The Player prefab has `Rigidbody2D` + `CapsuleCollider2D` + `SpriteRenderer`. `PlayerController.cs` already references a `Rigidbody` (3D) via `[SerializeField] private Rigidbody rb`. The prefab must match.

- [ ] **Step 1: Load Navigation scene (or open prefab) and verify current components**

```
mcp tool: manage_scene(action="load", scene_path="Assets/Scenes/Navigation.unity")
```
Then find the Player prefab instance or open the prefab directly:
```
mcp tool: find_gameobjects(search_term="Player", search_method="by_name")
```
Read `mcpforunity://scene/gameobject/{instance_id}` to confirm components.

- [ ] **Step 2: Remove 2D physics components from Player**

Via `manage_prefabs` or `manage_components` on the Player prefab, remove:
- `Rigidbody2D`
- `CapsuleCollider2D`
- `SpriteRenderer`
- `Animator` (if present — no longer needed with primitive mesh)

```
mcp tool: manage_prefabs(action="open", prefab_path="Assets/Prefabs/Characters/Player.prefab")
mcp tool: manage_components(action="remove", target="Player", component_type="Rigidbody2D")
mcp tool: manage_components(action="remove", target="Player", component_type="CapsuleCollider2D")
mcp tool: manage_components(action="remove", target="Player", component_type="SpriteRenderer")
```

- [ ] **Step 3: Add 3D physics components to Player**

```
mcp tool: manage_components(action="add", target="Player", component_type="Rigidbody")
mcp tool: manage_components(action="add", target="Player", component_type="CapsuleCollider")
mcp tool: manage_components(action="add", target="Player", component_type="MeshRenderer")
```

- [ ] **Step 4: Configure Rigidbody — no gravity, freeze X/Z rotation**

```
mcp tool: manage_physics(action="configure_rigidbody",
    target="Player",
    use_gravity=False,
    constraints={
        "freezePositionX": False,
        "freezePositionY": True,
        "freezePositionZ": False,
        "freezeRotationX": True,
        "freezeRotationY": False,
        "freezeRotationZ": True
    }
)
```

Or via `manage_components(action="set_property", ...)`:
```
manage_components(action="set_property", target="Player", component="Rigidbody",
    property="useGravity", value=False)
manage_components(action="set_property", target="Player", component="Rigidbody",
    property="constraints", value=116)  // RigidbodyConstraints: FreezePositionY | FreezeRotationX | FreezeRotationZ = 4+16+64 = 84, check actual enum value
```
Note: `RigidbodyConstraints` flags: FreezePositionY=4, FreezeRotationX=16, FreezeRotationZ=64 → value=84.

- [ ] **Step 5: Wire Rigidbody reference in PlayerController**

After saving the prefab, in the Navigation scene the `PlayerController` component's `rb` field must point to the `Rigidbody` component. Verify via:
```
mcp tool: manage_components(action="get", target="Player", component_type="PlayerController")
```
If the `rb` field is null, set it:
```
mcp tool: manage_components(action="set_property", target="Player", 
    component="PlayerController", property="rb", 
    value={reference to Rigidbody on same GameObject})
```

- [ ] **Step 6: Save prefab, check console for errors**

```
mcp tool: manage_prefabs(action="save", prefab_path="Assets/Prefabs/Characters/Player.prefab")
mcp tool: read_console(types=["error"], count=10)
```
Expected: no compilation or NullRef errors.

---

## Task 2: Navigation Scene — Camera → Perspective

**Files:**
- Modify: `Assets/Scenes/Navigation.unity` (Main Camera GameObject)

**Context:** The Main Camera in Navigation.unity has `orthographic: 1`, Far Clip: 11, and a `PixelPerfectCamera` component. It needs to become a Perspective camera with FOV 60, Far Clip 100.

- [ ] **Step 1: Load Navigation scene**

```
mcp tool: manage_scene(action="load", scene_path="Assets/Scenes/Navigation.unity")
```

- [ ] **Step 2: Find Main Camera**

```
mcp tool: find_gameobjects(search_term="Main Camera", search_method="by_name")
```
Note the instance_id.

- [ ] **Step 3: Switch camera to Perspective, set FOV and Far Clip**

```
mcp tool: manage_camera(action="lens",
    camera="Main Camera",
    projection="Perspective",
    field_of_view=60,
    far_clip_plane=100
)
```

- [ ] **Step 4: Remove PixelPerfectCamera component**

```
mcp tool: manage_components(action="remove", 
    target="Main Camera", 
    component_type="PixelPerfectCamera")
```

- [ ] **Step 5: Check console**

```
mcp tool: read_console(types=["error", "warning"], count=10)
```

---

## Task 3: Navigation Scene — Cinemachine Camera → 3D Top-Down

**Files:**
- Modify: `Assets/Scenes/Navigation.unity` (CinemachineCamera GameObject)

**Context:** The CinemachineCamera has a `CinemachinePixelPerfect` extension and is positioned at Z=-10 (2D). It needs to follow the Player from directly above: offset (0, 15, 0), rotation (90, 0, 0), no PixelPerfect.

- [ ] **Step 1: Find CinemachineCamera**

```
mcp tool: find_gameobjects(search_term="CinemachineCamera", search_method="by_name")
```

- [ ] **Step 2: Remove CinemachinePixelPerfect extension**

```
mcp tool: manage_camera(action="extension",
    camera="CinemachineCamera",
    extension_action="remove",
    extension_type="CinemachinePixelPerfect"
)
```

- [ ] **Step 3: Set body to Transposer/HardLockToTarget with offset (0, 15, 0)**

```
mcp tool: manage_camera(action="body",
    camera="CinemachineCamera",
    body_type="HardLockToTarget"
)
```
Then set offset:
```
mcp tool: manage_components(action="set_property",
    target="CinemachineCamera",
    component="CinemachineTransposer",  // or CinemachineFollow
    property="m_FollowOffset",
    value=[0, 15, 0]
)
```

- [ ] **Step 4: Set CinemachineCamera rotation to look straight down**

The CinemachineCamera's own transform should face down (90° on X):
```
mcp tool: manage_gameobject(action="modify",
    target="CinemachineCamera",
    rotation=[90, 0, 0]
)
```

- [ ] **Step 5: Ensure Follow target is Player**

```
mcp tool: manage_camera(action="target",
    camera="CinemachineCamera",
    follow_target="Player"
)
```

- [ ] **Step 6: Screenshot to verify top-down view**

```
mcp tool: manage_camera(action="screenshot", include_image=True, max_resolution=512)
```
Expected: Player capsule visible from above.

---

## Task 4: Navigation Scene — Replace Tilemap Floor with Plane

**Files:**
- Modify: `Assets/Scenes/Navigation.unity` (Grid/Tilemap → destroy; create Plane)

**Context:** The Navigation scene has a `Grid` GameObject with `Tilemap` + `TilemapRenderer`. This must be replaced with a `Plane` primitive that has a `MeshCollider` so the Player can walk on it.

- [ ] **Step 1: Delete the Grid (Tilemap) GameObject**

```
mcp tool: find_gameobjects(search_term="Grid", search_method="by_name")
// Note instance_id
mcp tool: manage_gameobject(action="delete", target={instance_id})
```

- [ ] **Step 2: Create Plane primitive as floor**

```
mcp tool: manage_gameobject(action="create",
    name="Floor",
    primitive_type="Plane",
    position=[0, 0, 0]
)
```
Default Plane is 10×10 units. Scale up if needed:
```
mcp tool: manage_gameobject(action="modify", target="Floor", scale=[5, 1, 5])
```
(50×50 units — enough for player to walk around)

- [ ] **Step 3: Add MeshCollider to Floor**

```
mcp tool: manage_components(action="add", target="Floor", component_type="MeshCollider")
```

- [ ] **Step 4: Verify Player doesn't fall through floor**

Enter Play Mode and check:
```
mcp tool: manage_editor(action="enter_play_mode")
// Wait ~2 seconds
mcp tool: manage_camera(action="screenshot", include_image=True, max_resolution=512)
mcp tool: manage_editor(action="exit_play_mode")
```
Expected: Player capsule resting on the plane.

---

## Task 5: Navigation Scene — EnemyEncounterTrigger → 3D BoxCollider

**Files:**
- Modify: `Assets/Scenes/Navigation.unity` (EnemyEncounterTrigger GameObject)

**Context:** `EnemyEncounterTrigger` has a 2D collider. `CombatTrigger.cs` already uses `OnTriggerEnter(Collider)`. The trigger needs a 3D `BoxCollider` with `isTrigger=true`.

- [ ] **Step 1: Find EnemyEncounterTrigger and check components**

```
mcp tool: find_gameobjects(search_term="EnemyEncounterTrigger", search_method="by_name")
// Read: mcpforunity://scene/gameobject/{id}
```

- [ ] **Step 2: Remove any 2D collider**

```
mcp tool: manage_components(action="remove", target="EnemyEncounterTrigger", component_type="BoxCollider2D")
// Also remove SpriteRenderer if present:
mcp tool: manage_components(action="remove", target="EnemyEncounterTrigger", component_type="SpriteRenderer")
```

- [ ] **Step 3: Add BoxCollider 3D trigger**

```
mcp tool: manage_components(action="add", target="EnemyEncounterTrigger", component_type="BoxCollider")
mcp tool: manage_components(action="set_property",
    target="EnemyEncounterTrigger",
    component="BoxCollider",
    property="isTrigger",
    value=True
)
mcp tool: manage_components(action="set_property",
    target="EnemyEncounterTrigger",
    component="BoxCollider",
    property="size",
    value=[3, 1, 3]
)
```

- [ ] **Step 4: Save Navigation scene**

```
mcp tool: manage_scene(action="save")
```

---

## Task 6: Combat Scene — Camera → Perspective

**Files:**
- Modify: `Assets/Scenes/Combat.unity` (Main Camera and CinemachineCamera)

**Context:** Same pattern as Navigation: orthographic cameras + `PixelPerfectCamera` + `CinemachinePixelPerfect` need to become perspective top-down.

- [ ] **Step 1: Load Combat scene**

```
mcp tool: manage_scene(action="load", scene_path="Assets/Scenes/Combat.unity")
```

- [ ] **Step 2: Find all cameras**

```
mcp tool: find_gameobjects(search_term="Camera", search_method="by_component")
```

- [ ] **Step 3: Switch Main Camera to Perspective**

```
mcp tool: manage_camera(action="lens",
    camera="Main Camera",
    projection="Perspective",
    field_of_view=60,
    far_clip_plane=100
)
mcp tool: manage_components(action="remove",
    target="Main Camera",
    component_type="PixelPerfectCamera")
```

- [ ] **Step 4: Remove CinemachinePixelPerfect from CinemachineCamera**

```
mcp tool: find_gameobjects(search_term="CinemachineCamera", search_method="by_name")
mcp tool: manage_camera(action="extension",
    camera="CinemachineCamera",
    extension_action="remove",
    extension_type="CinemachinePixelPerfect"
)
```

- [ ] **Step 5: Remove Tilemap from Combat scene (optional cleanup)**

```
mcp tool: find_gameobjects(search_term="Grid", search_method="by_name")
mcp tool: manage_gameobject(action="delete", target={instance_id})
```

- [ ] **Step 6: Save Combat scene and check console**

```
mcp tool: manage_scene(action="save")
mcp tool: read_console(types=["error"], count=15)
```
Expected: no errors.

---

## Task 7: End-to-End Acceptance Check

**Goal:** Verify all 6 acceptance criteria from the spec.

- [ ] **Criterion 1 — Player moves in 4 directions on XZ plane**

```
mcp tool: manage_scene(action="load", scene_path="Assets/Scenes/Navigation.unity")
mcp tool: manage_editor(action="enter_play_mode")
// Manually move player or verify via console that rb.linearVelocity changes
mcp tool: read_console(types=["error"], count=10)
mcp tool: manage_editor(action="exit_play_mode")
```
Expected: No NullRef. Player capsule moves.

- [ ] **Criterion 2 — Cinemachine perspective top-down follows Player**

In play mode, take screenshot:
```
mcp tool: manage_camera(action="screenshot", include_image=True, max_resolution=512)
```
Expected: Top-down view, Player visible from above.

- [ ] **Criterion 3 — Walking over EnemyEncounterTrigger starts Combat transition without errors**

Enter play mode in Navigation. Walk Player over trigger.
```
mcp tool: read_console(types=["error"], count=15)
```
Expected: No errors. Scene transition to Combat initiates.

- [ ] **Criterion 4 — In Combat, enemies = red capsules, operators = blue capsules**

```
mcp tool: manage_scene(action="load", scene_path="Assets/Scenes/Combat.unity")
mcp tool: manage_editor(action="enter_play_mode")
mcp tool: manage_camera(action="screenshot", include_image=True, max_resolution=512)
mcp tool: manage_editor(action="exit_play_mode")
```
Expected: Capsule GameObjects with red/blue materials visible.

- [ ] **Criterion 5 — No 2D references in migrated scripts**

```bash
grep -rn "Rigidbody2D\|Collider2D\|OnTriggerEnter2D" \
  "Assets/Scripts/Navigation/Player/PlayerController.cs" \
  "Assets/Scripts/Navigation/Combat/CombatTrigger.cs" \
  "Assets/Scripts/Combat/UI/BattlefieldView.cs"
```
Expected: 0 matches.

- [ ] **Criterion 6 — Console clean in Play Mode**

```
mcp tool: manage_editor(action="enter_play_mode")
// wait 3 seconds
mcp tool: read_console(types=["error"], count=20)
mcp tool: manage_editor(action="exit_play_mode")
```
Expected: 0 errors.

---

## Self-Review Against Spec

| Spec requirement | Covered by |
|---|---|
| Player (cápsula) se mueve en 4 sentidos sobre XZ | Task 1 (Rigidbody + CapsuleCollider) + Task 7 criterion 1 |
| Cámara perspective top-down sigue al Player via Cinemachine | Task 2 + Task 3 + Task 7 criterion 2 |
| EnemyEncounterTrigger dispara transición a Combat sin errores | Task 5 + Task 7 criterion 3 |
| Enemigos/operadores como cápsulas con colores diferenciados | Scripts already done; Task 6 + Task 7 criterion 4 |
| Sin referencias 2D en scripts migrados | Task 7 criterion 5 |
| Console limpia en Play Mode | Task 7 criterion 6 |
| Rigidbody `useGravity: false`, rotación congelada X/Z | Task 1 step 4 |
| Main Camera Far Clip 100 | Task 2 step 3, Task 6 step 3 |
| CinemachineCamera offset (0,15,0) rot (90,0,0) | Task 3 steps 3–4 |
| Tilemap → Plane primitive + MeshCollider | Task 4 |
| Combat: remove Sorting layers, use 3D colliders | Task 6 |
