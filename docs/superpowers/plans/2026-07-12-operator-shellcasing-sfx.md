# Operator Shell Casing SFX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Play `Play_ShellCasing` via a second Animation Event on `ShootPistolFlexed2`, placed later than the existing muzzle-flash event, so the casing sound plays shortly after the gunshot.

**Architecture:** Add one field + one method to the already-implemented `OperatorCombatAudio` component, then add a second Animation Event on the same clip already used by `PlayFireGunSfx`.

**Tech Stack:** C#, Wwise (`AK.Wwise.Event`), Unity Animation Events.

## Global Constraints

- All files use `#nullable enable`.
- No `Co-Authored-By` trailers in commits.
- No `GunType` switching for this sound — a single event posted on `gameObject`, matching `FootstepController`/`EnemyCombatAudio`'s simplest pattern.
- No changes to `BattlefieldView.cs` or any other file — entirely self-contained in `OperatorCombatAudio.cs` + content wiring.
- No automated test — consistent with the rest of this file's Animator/Wwise-driven methods.

---

### Task 1: `PlayShellCasingSfx` + Animation Event

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Audio/OperatorCombatAudio.cs`
- Content-only: `Operator_Combat_Controller.controller`'s `ShootPistolFlexed2` clip (source FBX: `Assets/Art/Models/FBX_Export/RE1Aim&ShootPistol.fbx`)

**Interfaces:**
- Produces: `public void OperatorCombatAudio.PlayShellCasingSfx()`, called exclusively by a Unity Animation Event.

- [ ] **Step 1: Add the field and method**

In `Game/CrimsonDraft/Assets/Scripts/Audio/OperatorCombatAudio.cs`, replace:

```csharp
        [SerializeField] private AK.Wwise.Event       fireGunEvent    = new();
        [SerializeField] private GunTypeSwitchEntry[] gunTypeSwitches = Array.Empty<GunTypeSwitchEntry>();
```

with:

```csharp
        [SerializeField] private AK.Wwise.Event       fireGunEvent     = new();
        [SerializeField] private GunTypeSwitchEntry[] gunTypeSwitches  = Array.Empty<GunTypeSwitchEntry>();
        [SerializeField] private AK.Wwise.Event       shellCasingEvent = new();
```

And add the new method right after `PlayFireGunSfx()`'s closing brace:

```csharp
        // Called by Animation Event on the ShootPistolFlexed2 clip, later than PlayFireGunSfx's event.
        public void PlayShellCasingSfx() => this.shellCasingEvent.Post(gameObject);
```

- [ ] **Step 2: Verify it compiles**

Use `mcp__UnityMCP__refresh_unity` (compile: request, mode: force) then `mcp__UnityMCP__read_console` (types: error). Expected: no new errors.

- [ ] **Step 3: Add the second Animation Event on `ShootPistolFlexed2`**

Using `mcp__UnityMCP__execute_code`, load `Assets/Art/Models/FBX_Export/RE1Aim&ShootPistol.fbx`'s `ModelImporter`, find the `ShootPistolFlexed2` entry in `clipAnimations` (0-15 frames at 24fps, 0.625s — already has one event, `PlayFireGunSfx` at time `4f/24f`), and add a second `AnimationEvent` to its `events` array (do not remove the existing one) with `functionName = "PlayShellCasingSfx"` and `time = 10f/24f` (frame 10 of 15, ~0.417s — later than the muzzle-flash event, an approximate casing-eject point; adjust in the Editor's Animation window if it doesn't read right once the human can preview it). Assign the updated `clipAnimations` back and call `importer.SaveAndReimport()`. Verify both events are present afterward by re-loading the clip and reading `clip.events`.

- [ ] **Step 4: Run the full EditMode suite**

Use `mcp__UnityMCP__run_tests` (mode: EditMode). Expected: 248 tests, 230 passed, 18 failed — the same pre-existing, unrelated failure set already established in this branch's history (`CombatMenuControllerTests.ShotCount_cancel_returnsToCommandPanel` + 17 `InventoryServiceTests.*`).

- [ ] **Step 5: Commit**

Check `git status` first (this branch has unrelated Wwise-authoring churn sitting in the working tree). Stage only:

```bash
git add Game/CrimsonDraft/Assets/Scripts/Audio/OperatorCombatAudio.cs "Game/CrimsonDraft/Assets/Art/Models/FBX_Export/RE1Aim&ShootPistol.fbx.meta"
git commit -m "feat(audio): play Play_ShellCasing shortly after the operator's gunfire SFX"
```

The `shellCasingEvent` field will be assigned by the human directly in the Inspector once `Play_ShellCasing` is recognized on the Wwise/Unity side (same recurring SoundBank gap as prior events in this branch) — do not attempt to resolve that gap as part of this task.
