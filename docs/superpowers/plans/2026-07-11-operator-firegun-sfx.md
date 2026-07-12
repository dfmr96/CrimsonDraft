# Operator Fire-Gun SFX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Play `Play_FireGunsSC`, switched by the `GunType` Wwise Switch Group, whenever the operator's `ShootPistolFlexed2` animation plays — the correct gunfire sound for whichever weapon that operator currently has equipped.

**Architecture:** A new `GunType` enum flows from a new `WeaponData` field through `IWeaponSlot`/`WeaponItem` (mirroring the existing `Caliber` field exactly). A new `OperatorCombatAudio` component lives on each operator's battlefield prefab, bound once (at spawn, by `BattlefieldView`) to the live `IOperatorRoster` and its own slot index — after that one bind, it is self-sufficient: it looks up its own current weapon's `GunType` itself whenever its own Animation Event fires, sets the matching `AK.Wwise.Switch`, and posts the event. `BattlefieldView`'s only involvement is that one-time reference handoff at spawn time.

**Tech Stack:** C#, VContainer (`[Inject]`), Wwise (`AK.Wwise.Event`, `AK.Wwise.Switch`), Unity Animation Events.

## Global Constraints

- All files use `#nullable enable`.
- No `Co-Authored-By` trailers in commits.
- No changes to `AimingState.cs` or `IBattlefieldView.cs` — the existing burst-timing/flinch-sync logic is untouched; this feature only adds a one-time roster bind inside `BattlefieldView.Populate()`.
- `GunType` enum values must be exactly `Pistols`, `Shotgun`, `REPistols`, `REShotgun` — matching the Wwise `GunType` Switch Group's four switches verbatim (verified against `CrimsonDraft_WwiseProject/Switches/Default Work Unit.wwu`).
- No automated test — `OperatorCombatAudio` is a content-adjacent MonoBehaviour with no branching logic beyond a small fixed-size lookup, consistent with `FootstepController`/`EnemyCombatAudio`, neither of which has EditMode tests. Verify via compilation and Play Mode instead.

---

## File Structure

- Create `Game/CrimsonDraft/Assets/Scripts/Operators/GunType.cs` — the new enum.
- Modify `Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponData.cs` — add the `gunType` field.
- Modify `Game/CrimsonDraft/Assets/Scripts/Operators/IWeaponSlot.cs` — add the `GunType` property.
- Modify `Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponItem.cs` — implement the new property.
- Create `Game/CrimsonDraft/Assets/Scripts/Audio/OperatorCombatAudio.cs` — the new component.
- Modify `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs` — inject `IOperatorRoster`, bind the spawned operator's audio component.
- Content-only changes (no new C# files): the four operator battlefield prefabs, the `WeaponData` assets under `Assets/Data/Inventory/Weapons/`, and the `ShootPistolFlexed2` clip in `Operator_Combat_Controller.controller`.

---

### Task 1: `GunType` data model

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Operators/GunType.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponData.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Operators/IWeaponSlot.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponItem.cs`

**Interfaces:**
- Produces: `CrimsonDraft.Operators.GunType` enum (`Pistols`, `Shotgun`, `REPistols`, `REShotgun`); `WeaponData.GunType` property; `IWeaponSlot.GunType` property; `WeaponItem.GunType` implementation. Consumed by Task 2's `OperatorCombatAudio`.

- [ ] **Step 1: Create the enum**

Create `Game/CrimsonDraft/Assets/Scripts/Operators/GunType.cs` with exactly this content:

```csharp
#nullable enable

namespace CrimsonDraft.Operators
{
    public enum GunType
    {
        Pistols,
        Shotgun,
        REPistols,
        REShotgun,
    }
}
```

- [ ] **Step 2: Add the field to `WeaponData`**

In `Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponData.cs`, add the field and property. Replace:

```csharp
        [SerializeField] private Caliber           caliber                = Caliber.None;
        [SerializeField] private int               magazineCapacity       = 1;
```

with:

```csharp
        [SerializeField] private Caliber           caliber                = Caliber.None;
        [SerializeField] private GunType           gunType                = GunType.Pistols;
        [SerializeField] private int               magazineCapacity       = 1;
```

And replace:

```csharp
        public Caliber           Caliber                => this.caliber;
        public int               MagazineCapacity       => this.magazineCapacity;
```

with:

```csharp
        public Caliber           Caliber                => this.caliber;
        public GunType           GunType                => this.gunType;
        public int               MagazineCapacity       => this.magazineCapacity;
```

- [ ] **Step 3: Add the property to `IWeaponSlot`**

In `Game/CrimsonDraft/Assets/Scripts/Operators/IWeaponSlot.cs`, replace:

```csharp
    public interface IWeaponSlot
    {
        Caliber Caliber    { get; }
        int     BaseDamage { get; }
        int     CurrentAmmo { get; }
        int     MaxAmmo     { get; }
        void    SetAmmo(int value);
    }
```

with:

```csharp
    public interface IWeaponSlot
    {
        Caliber Caliber    { get; }
        GunType GunType    { get; }
        int     BaseDamage { get; }
        int     CurrentAmmo { get; }
        int     MaxAmmo     { get; }
        void    SetAmmo(int value);
    }
```

- [ ] **Step 4: Implement it in `WeaponItem`**

In `Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponItem.cs`, replace:

```csharp
        public new WeaponData Data    => (WeaponData)base.Data;
        public Caliber Caliber       => this.Data.Caliber;
        public int     BaseDamage    => this.Data.Damage;
```

with:

```csharp
        public new WeaponData Data    => (WeaponData)base.Data;
        public Caliber Caliber       => this.Data.Caliber;
        public GunType GunType       => this.Data.GunType;
        public int     BaseDamage    => this.Data.Damage;
```

- [ ] **Step 5: Verify it compiles**

Use `mcp__UnityMCP__refresh_unity` (compile: request, mode: force) then `mcp__UnityMCP__read_console` (types: error, warning). Expected: no new errors. Note that `CombatMenuControllerTests.cs`'s `FakeWeaponSlot` (in the test file) implements `IWeaponSlot` and will now fail to compile until it also implements `GunType` — this is expected and is NOT part of this task's scope to fix silently; if the compiler reports this specific error, that confirms the interface change took effect correctly. Do not touch the test file in this task.

- [ ] **Step 6: Fix the now-broken test fake**

Since `IWeaponSlot` gained a required member, `CombatMenuControllerTests.cs`'s `FakeWeaponSlot` (nested inside `FakeOperatorRoster`) no longer compiles. This is a mechanical, same-task fix (not a scope violation — an interface change and its direct implementers must land together to keep the build green). In `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`, find the `FakeWeaponSlot` class and add the missing property. Replace:

```csharp
            private sealed class FakeWeaponSlot : IWeaponSlot
            {
                public Caliber Caliber    => Caliber._9mm;
                public int     BaseDamage => 20;
```

with:

```csharp
            private sealed class FakeWeaponSlot : IWeaponSlot
            {
                public Caliber Caliber    => Caliber._9mm;
                public GunType GunType    => GunType.Pistols;
                public int     BaseDamage => 20;
```

- [ ] **Step 7: Run the full EditMode suite**

Use `mcp__UnityMCP__run_tests` (mode: EditMode). Expected: 248 tests, 230 passed, 18 failed — exactly the pre-existing, unrelated failure set (`CombatMenuControllerTests.ShotCount_cancel_returnsToCommandPanel` + 17 `InventoryServiceTests.*`). No new failures.

- [ ] **Step 8: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Operators/GunType.cs Game/CrimsonDraft/Assets/Scripts/Operators/GunType.cs.meta Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponData.cs Game/CrimsonDraft/Assets/Scripts/Operators/IWeaponSlot.cs Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponItem.cs Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(inventory): add GunType to WeaponData and IWeaponSlot"
```

---

### Task 2: `OperatorCombatAudio` component + `BattlefieldView` roster bind

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Audio/OperatorCombatAudio.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs`

**Interfaces:**
- Consumes: `IWeaponSlot.GunType` (Task 1); `IOperatorRoster` (`Count`, `this[int]`, already registered as a Singleton in `NavigationScope`, a parent of `CombatScope` — resolvable from `BattlefieldView` with no new DI registration needed).
- Produces: `public void OperatorCombatAudio.Bind(IOperatorRoster roster, int slotIndex)`; `public void OperatorCombatAudio.PlayFireGunSfx()` (called exclusively by a Unity Animation Event — Task 3).

- [ ] **Step 1: Create `OperatorCombatAudio`**

Create `Game/CrimsonDraft/Assets/Scripts/Audio/OperatorCombatAudio.cs` with exactly this content:

```csharp
#nullable enable

using System;
using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Audio
{
    public sealed class OperatorCombatAudio : MonoBehaviour
    {
        [Serializable]
        public struct GunTypeSwitchEntry
        {
            public GunType         GunType;
            public AK.Wwise.Switch WwiseSwitch;
        }

        [SerializeField] private AK.Wwise.Event       fireGunEvent    = new();
        [SerializeField] private GunTypeSwitchEntry[] gunTypeSwitches = Array.Empty<GunTypeSwitchEntry>();

        private IOperatorRoster? roster;
        private int              slotIndex = -1;

        public void Bind(IOperatorRoster roster, int slotIndex)
        {
            this.roster    = roster;
            this.slotIndex = slotIndex;
        }

        // Called by Animation Event on the ShootPistolFlexed2 clip.
        public void PlayFireGunSfx()
        {
            if (this.roster == null || this.slotIndex < 0 || this.roster.Count <= this.slotIndex)
                return;

            var weapon = this.roster[this.slotIndex].ActiveWeapon;
            if (weapon == null)
                return;

            foreach (var entry in this.gunTypeSwitches)
            {
                if (entry.GunType == weapon.GunType)
                {
                    entry.WwiseSwitch.SetValue(gameObject);
                    break;
                }
            }

            this.fireGunEvent.Post(gameObject);
        }
    }
}
```

- [ ] **Step 2: Inject `IOperatorRoster` into `BattlefieldView` and bind at spawn**

In `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs`, replace the using block (lines 3-9):

```csharp
using System;
using System.Collections.Generic;
using System.Collections;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
```

with:

```csharp
using System;
using System.Collections.Generic;
using System.Collections;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using VContainer;
using CrimsonDraft.Operators;
using CrimsonDraft.Audio;
```

Add the field and `[Inject] Construct` method right after the `Hit2Hash` field (after line 47, before `private void Awake()`) — this matches the exact attribute convention already used by `Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerController.cs:44-51`:

```csharp
        private static readonly int Hit2Hash = Animator.StringToHash("Hit2");

        private IOperatorRoster? roster;

        [Inject]
        public void Construct(IOperatorRoster roster)
        {
            this.roster = roster;
        }

        private void Awake()
```

In the operator spawn loop inside `Populate()` (currently lines 121-123), add the bind call right after the existing `operatorAnimatorBySlot` assignment. Replace:

```csharp
                var operatorAnimator = go.GetComponentInChildren<Animator>();
                if (operatorAnimator != null)
                    this.operatorAnimatorBySlot[i] = operatorAnimator;
            }
        }
```

with:

```csharp
                var operatorAnimator = go.GetComponentInChildren<Animator>();
                if (operatorAnimator != null)
                    this.operatorAnimatorBySlot[i] = operatorAnimator;

                var operatorAudio = go.GetComponentInChildren<OperatorCombatAudio>();
                if (operatorAudio != null && this.roster != null)
                    operatorAudio.Bind(this.roster, i);
            }
        }
```

- [ ] **Step 3: Verify it compiles**

Use `mcp__UnityMCP__refresh_unity` (compile: request, mode: force) then `mcp__UnityMCP__read_console`. Expected: no new errors.

- [ ] **Step 4: Run the full EditMode suite**

Use `mcp__UnityMCP__run_tests` (mode: EditMode). Expected: same 248/230/18 result as Task 1 — no regressions, since `BattlefieldView` isn't directly instantiated in these tests (only its `IBattlefieldView` interface is faked).

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Audio/OperatorCombatAudio.cs Game/CrimsonDraft/Assets/Scripts/Audio/OperatorCombatAudio.cs.meta Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs
git commit -m "feat(audio): bind operator battlefield audio to its roster slot at spawn"
```

---

### Task 3: Content wiring + verification

**Files:**
- Content-only: `Game/CrimsonDraft/Assets/Prefabs/Characters/Ethan_Combat_FBX.prefab`, `RestPoseMarcusFBX.prefab`, `Lilou_Combat_FBX.prefab`, `Darius_Combat_FBX 1.prefab`
- Content-only: `Game/CrimsonDraft/Assets/Data/Inventory/Weapons/Benelli_M4.asset`, `Mk18.asset`, `MP7.asset`, `P226.asset`, `P229.asset`
- Content-only: `Game/CrimsonDraft/Assets/Animations/Operator_Combat_Controller.controller`'s `ShootPistolFlexed2` clip

**Interfaces:**
- Consumes: `OperatorCombatAudio.PlayFireGunSfx()` (Task 2) as the Animation Event target function name.

- [ ] **Step 1: Add `OperatorCombatAudio` to each operator battlefield prefab**

For each of `Ethan_Combat_FBX.prefab`, `RestPoseMarcusFBX.prefab`, `Lilou_Combat_FBX.prefab`, `Darius_Combat_FBX 1.prefab`: add an `OperatorCombatAudio` component to the same GameObject that already carries the `Animator` referencing `Operator_Combat_Controller`. Assign `Play_FireGunsSC` to `fireGunEvent`. Configure `gunTypeSwitches` with 4 entries, one per `GunType` value, each pointing at the matching Wwise switch under the `GunType` Switch Group (`Pistols` / `Shotgun` / `REPistols` / `REShotgun`).

If the `AK.Wwise.Event`/`AK.Wwise.Switch` assets for `Play_FireGunsSC` or the `GunType` switches aren't available to assign (empty picker, same class of issue encountered with `Play_Zombie_Damage` in the prior SFX task — requiring a SoundBank regeneration and/or opening the Wwise Picker window to repopulate), STOP and report exactly that rather than leaving fields silently unassigned without saying so. This is a legitimate stopping point for a human to resolve in the Wwise/Unity editor.

- [ ] **Step 2: Assign `GunType` on each weapon asset**

For each of `Benelli_M4.asset`, `Mk18.asset`, `MP7.asset`, `P226.asset`, `P229.asset` (under `Assets/Data/Inventory/Weapons/`), set the new `Gun Type` field in the Inspector to whichever of `Pistols`/`Shotgun`/`REPistols`/`REShotgun` matches that weapon (e.g. a shotgun asset gets `Shotgun`; pistols get `Pistols` or `REPistols` depending on which visual/sound family they belong to). This is a content/design decision — if it's ambiguous which value a given weapon should get, stop and ask rather than guessing.

- [ ] **Step 3: Add the Animation Event on `ShootPistolFlexed2`**

Add a Unity Animation Event calling `PlayFireGunSfx` (no parameters) on the `ShootPistolFlexed2` clip referenced by `Operator_Combat_Controller.controller`, at the frame that reads as the muzzle-flash/firing moment. Use the same technique already proven in the prior SFX task if the clip is FBX-imported (via the `ModelImporter.clipAnimations`/`SaveAndReimport()` approach, inspected and set through `mcp__UnityMCP__execute_code`) — first locate which FBX asset `ShootPistolFlexed2`'s `m_Motion` guid (`23c583711ea7972448a8ce8501e31d9d`, per `Operator_Combat_Controller.controller`) belongs to, confirm the clip's exact name and frame count via `AssetDatabase.LoadAllAssetsAtPath`, then set the event through the importer, matching the pattern used for `Armature|Hit_1`/`Armature|Hit_2` in the enemy damage SFX task.

- [ ] **Step 4: Verify in Play Mode**

If a Unity Editor instance is connected, enter Play Mode, populate a combat encounter with at least one operator present, and directly invoke `BattlefieldView.PlayOperatorShootBurstAsync(operatorSlotIndex, -1, shots)` (or reuse whatever direct-invocation approach proved reliable in the prior SFX verification) to drive the operator's Animator into `ShootPistolFlexed2`, confirming via `read_console` that `PlayFireGunSfx` fires without error and that Wwise attempts to post `Play_FireGunsSC` with the switch matching that operator's equipped weapon. A "SoundBank not loaded" warning (like the one seen with `Play_Zombie_Damage`) is an acceptable, already-understood outcome here — it does not indicate a code defect, only that the runtime SoundBank-loading gap from the prior task is still unresolved.

- [ ] **Step 5: Run the full EditMode suite one more time**

Use `mcp__UnityMCP__run_tests` (mode: EditMode). Expected: same 248/230/18 result — content-only changes plus one clip's Animation Event shouldn't affect any C# test.

- [ ] **Step 6: Commit**

Check `git status` first — this branch has a history of unrelated Wwise-authoring files and scene-file churn from Play Mode round-trips sitting in the working tree; stage only the files this task actually touched (the four prefabs, the five weapon assets, and whichever FBX + `.meta` holds `ShootPistolFlexed2`'s Animation Event — likely `Operator_Combat_Controller.controller`'s referenced source FBX, not the controller file itself, mirroring how the Hit_1/Hit_2 events landed in `Zombie.fbx.meta` rather than `Enemy_Combat_Controller.controller` in the prior task). Do not include `Combat.unity`/`Deck_B_Development.unity` or any Wwise ScriptableObject deletions unrelated to this specific feature unless you've verified they're actually required for `Play_FireGunsSC`/`GunType` to resolve (the same way `Wwise_IDs.h` and `AkWwiseProjectData.asset` were legitimately required in the prior task).

```bash
git status
git add "Game/CrimsonDraft/Assets/Prefabs/Characters/Ethan_Combat_FBX.prefab" \
        "Game/CrimsonDraft/Assets/Prefabs/Characters/RestPoseMarcusFBX.prefab" \
        "Game/CrimsonDraft/Assets/Prefabs/Characters/Lilou_Combat_FBX.prefab" \
        "Game/CrimsonDraft/Assets/Prefabs/Characters/Darius_Combat_FBX 1.prefab" \
        "Game/CrimsonDraft/Assets/Data/Inventory/Weapons/Benelli_M4.asset" \
        "Game/CrimsonDraft/Assets/Data/Inventory/Weapons/Mk18.asset" \
        "Game/CrimsonDraft/Assets/Data/Inventory/Weapons/MP7.asset" \
        "Game/CrimsonDraft/Assets/Data/Inventory/Weapons/P226.asset" \
        "Game/CrimsonDraft/Assets/Data/Inventory/Weapons/P229.asset"
# Add the FBX (+ .meta) that Step 3 identified as holding ShootPistolFlexed2's Animation Event —
# its exact path is only known once Step 3 resolves the m_Motion guid; add that specific pair here.
git commit -m "feat(audio): wire operator fire-gun SFX content (prefabs, weapon GunTypes, animation event)"
```
