# Enemy Damage SFX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Play the `Play_Zombie_Damage` Wwise event whenever the enemy's `Hit_1`/`Hit_2` flinch animation plays, via a Unity Animation Event — no code in `BattlefieldView` or anywhere else needs to know audio exists.

**Architecture:** A single new dependency-free MonoBehaviour, `EnemyCombatAudio`, lives on `EnemyCombatModel.prefab`. It exposes one public method that posts the assigned `AK.Wwise.Event`. That method gets wired as a Unity Animation Event directly on the `Armature|Hit_1` and `Armature|Hit_2` clips already used by the (already-implemented) enemy flinch feature — this mirrors the existing `FootstepController.OnWalkStep()`/`OnRunStep()` pattern exactly.

**Tech Stack:** C#, Wwise (`AK.Wwise.Event`), Unity Animation Events (content-only wiring, no code drives them).

## Global Constraints

- All files use `#nullable enable`.
- No `Co-Authored-By` trailers in commits.
- No changes to `BattlefieldView.cs`, `IBattlefieldView.cs`, or `AimingState.cs` — this feature is entirely new content plus one new, dependency-free component.
- No VContainer `[Inject]`, no MessagePipe — `EnemyCombatAudio` has zero dependencies, matching `FootstepController`.
- No automated test — this is a content-adjacent MonoBehaviour with no branching logic, consistent with `FootstepController`/`InventorySoundManager`, neither of which has EditMode tests.

---

## File Structure

- Create `Game/CrimsonDraft/Assets/Scripts/Audio/EnemyCombatAudio.cs` — the new component.
- Content-only change (no new C# file): `Game/CrimsonDraft/Assets/Prefabs/Enemies/EnemyCombatModel.prefab` gets the component added and its Wwise event assigned; the `Armature|Hit_1`/`Armature|Hit_2` clips get an Animation Event added calling `EnemyCombatAudio.PlayDamageSfx()`.

---

### Task 1: `EnemyCombatAudio` component + content wiring

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Audio/EnemyCombatAudio.cs`
- Modify (content only): `Game/CrimsonDraft/Assets/Prefabs/Enemies/EnemyCombatModel.prefab`
- Modify (content only): the animation clips referenced by `Armature|Hit_1` (`fileID: 486530989320235938, guid: d083d173d2fd7b34ca58efdc8ce4f4e4`) and `Armature|Hit_2` (`fileID: -2943417924466204503, guid: d083d173d2fd7b34ca58efdc8ce4f4e4`) in `Game/CrimsonDraft/Assets/Animations/Enemy_Combat_Controller.controller`

**Interfaces:**
- Produces: `public void EnemyCombatAudio.PlayDamageSfx()` — a public, no-argument method, callable as a Unity Animation Event target. No other code in the codebase calls or references this method; it is invoked exclusively by the Animation Event system.

- [ ] **Step 1: Create the component**

Create `Game/CrimsonDraft/Assets/Scripts/Audio/EnemyCombatAudio.cs` with exactly this content:

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    public sealed class EnemyCombatAudio : MonoBehaviour
    {
        [SerializeField] private AK.Wwise.Event damageEvent = new();

        // Called by Animation Event on the Hit_1 / Hit_2 clips.
        public void PlayDamageSfx() => this.damageEvent.Post(gameObject);
    }
}
```

This matches `Game/CrimsonDraft/Assets/Scripts/Audio/FootstepController.cs`'s existing pattern: a serialized `AK.Wwise.Event` field, posted on `gameObject` for correct 3D spatialization, with no other dependencies.

- [ ] **Step 2: Verify it compiles**

Use the UnityMCP tools available to you (`mcp__UnityMCP__refresh_unity`, then `mcp__UnityMCP__read_console`) to force a recompile and check for errors. Expected: no new compile errors or warnings referencing `EnemyCombatAudio.cs`.

- [ ] **Step 3: Add the component to the enemy prefab and assign the Wwise event**

Using the UnityMCP tools available to you (e.g. `mcp__UnityMCP__manage_prefabs`, `mcp__UnityMCP__manage_components`, `mcp__UnityMCP__manage_asset`, or `mcp__UnityMCP__execute_code` if the dedicated tools don't cover this exact operation):

1. Open `Game/CrimsonDraft/Assets/Prefabs/Enemies/EnemyCombatModel.prefab` for editing.
2. Add an `EnemyCombatAudio` component to its root GameObject (the same GameObject that already carries the `Animator` referencing `Enemy_Combat_Controller` — confirm this by checking the prefab's existing `Animator` component placement first).
3. Find the Wwise event named `Play_Zombie_Damage` in the project's Wwise data (it should already exist — the human has already created it in Wwise and generated the corresponding SoundBank/`AK.Wwise.Event` asset reference; search `Assets/Wwise/GeneratedSoundBanks` or the `AK.Wwise.Event` picker for an event with that exact name). Assign it to the new component's `damageEvent` field.
4. Save the prefab.

If any of these three actions cannot be performed through available tools (e.g. no tool exposes "assign an AK.Wwise.Event reference to a component field", or the `Play_Zombie_Damage` event cannot be located because the SoundBank hasn't been generated yet), STOP and report exactly which action is blocked and why — do not guess at a GUID or leave the field unassigned without saying so clearly. This is a legitimate stopping point; the human may need to do this specific step by hand in the Unity Editor Inspector.

- [ ] **Step 4: Add the Animation Event on `Armature|Hit_1` and `Armature|Hit_2`**

Using `mcp__UnityMCP__manage_animation` (or `execute_code` if that tool doesn't cover Animation Event authoring), open each of the two clips referenced by `Enemy_Combat_Controller.controller`'s `Armature|Hit_1` and `Armature|Hit_2` states (guid `d083d173d2fd7b34ca58efdc8ce4f4e4`, fileIDs `486530989320235938` and `-2943417924466204503` respectively — these are sub-assets of the same imported FBX) and add one Animation Event per clip:

- Function name: `PlayDamageSfx`
- Time: place it at whatever frame in each clip visually reads as the moment of impact (there is no single correct numeric value — use your judgment watching the clip, or place it at ~30-50% through the clip if you cannot preview it, and say so in your report so a human can adjust the exact timing later).
- No parameters (the method takes none).

If Animation Event authoring isn't reachable through any available UnityMCP tool, STOP and report exactly that — this is a legitimate stopping point requiring manual Editor work (Animation window → Events track → right-click → Add Event → select `PlayDamageSfx` from the function dropdown, once the `EnemyCombatAudio` component from Step 3 exists on the GameObject that's animated by this clip).

- [ ] **Step 5: Verify in Play Mode if possible**

If a Unity Editor instance is connected and Steps 3-4 succeeded, use `mcp__UnityMCP__manage_editor` to enter Play Mode, populate a combat encounter with an enemy present (mirroring the verification approach already used for the flinch feature — direct invocation of `BattlefieldView.PlayOperatorShootBurstAsync` against real content is an acceptable substitute for a full UI click-path), and confirm via `read_console` that no errors occur when the enemy's `Hit_1`/`Hit_2` animation plays. Audibly confirming the SFX plays isn't verifiable through these tools — confirming the absence of errors when the Animation Event fires is the achievable bar here.

- [ ] **Step 6: Commit**

Stage exactly the files that changed as a result of this task (the new `.cs` file, its `.meta`, and — only if Steps 3/4 actually completed — the modified prefab and animation clip assets). Do not stage unrelated files; check `git status` and review the diff before committing, since this branch (`misc/wwise-mix`) may have other unrelated Wwise-authoring files sitting in the working tree or index that are not part of this task.

```bash
git status
git add Game/CrimsonDraft/Assets/Scripts/Audio/EnemyCombatAudio.cs Game/CrimsonDraft/Assets/Scripts/Audio/EnemyCombatAudio.cs.meta
# Add the prefab/animation files here too ONLY if Steps 3-4 completed and modified them —
# check `git status` output first and list the exact paths that actually changed.
git commit -m "feat(audio): play Play_Zombie_Damage when the enemy's Hit_1/Hit_2 flinch animation plays"
```

Report in your final summary exactly which of Steps 3/4/5 completed via tools versus which are left for the human to finish manually, and exactly which files ended up in the commit.
