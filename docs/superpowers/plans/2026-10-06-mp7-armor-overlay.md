# MP7 & Armored Silhouette Overlays Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a reusable silhouette-overlay layer to the aim QTE, use it for a vest that only the MP7 fully penetrates, ship the armored enemy + MP7 pickup content, and show a total-damage popup at the end of every multi-shot QTE.

**Architecture:** Pure rule helpers (`ArmorRules`, `OverlayPicker`, `OverlayCoverage`, `TotalFeedback`) hold every decision so they're EditMode-testable without sprites. `BattlefieldView` rolls one `SilhouetteOverlay` per enemy at spawn; `TargetSelectionState` hands the resolved `ActiveOverlay` to `IAimView.ConfigureOverlay`; `AimViewController.BuildResolvedShots` samples the overlay mask at the same UV as the zone mask and folds the weapon's `armorDamageMultiplier` into `ComputeShotDamage`. Content and scene wiring are done through the Unity editor (UnityMCP `execute_code`).

**Tech Stack:** Unity 6 (C# 9), VContainer, MessagePipe, UniTask, DOTween, TextMeshPro, NUnit EditMode tests via Unity Test Runner / UnityMCP `run_tests`.

**Spec:** `docs/superpowers/specs/2026-10-06-mp7-armor-overlay-design.md`

## Global Constraints

- Every C# file starts with `#nullable enable`; zero new compiler warnings in touched files.
- Serialized required fields use `null!`; genuinely optional serialized references are `T?`.
- Combat code lives in namespace `CrimsonDraft.Combat` (assembly `CrimsonDraft.Combat`); tests in `CrimsonDraft.Tests` (assembly `CrimsonDraft.Tests.EditMode`, which already has `InternalsVisibleTo` access to Combat).
- Tests are plain C# fakes, no mocking framework. Private fields are set via reflection in tests when needed (existing convention, e.g. `CombatOrchestratorTests.SetRandom`).
- Enum values that get serialized are pinned explicitly (`Armor = 0`).
- Preserve each file's existing line endings (some are CRLF, e.g. `CombatMenuController.cs`).
- Commits: conventional style (`feat(combat): ...`), **no `Co-Authored-By` trailer** (CLAUDE.md).
- Baseline: full EditMode suite = 550 tests, the only failures are 5 pre-existing `ItemSocketInteractableTests.*` NullReferenceExceptions. Every task must end with no *other* failures.
- How to run tests: UnityMCP `run_tests` with `mode: "EditMode"` and `group_names: ["<regex>"]` (e.g. `"CrimsonDraft.Tests.ArmorRulesTests"`), then `get_test_job` with `wait_timeout: 120`. Before running, trigger a compile with `refresh_unity` (`compile: "request"`, `mode: "force"`, `scope: "scripts"`) and check `read_console` (types `["error"]`) for `CS` errors.
- `armorDamageMultiplier` default is `0.25`; MP7 is `1.0`. Blocked-hit color default `#33E0FF` = `new Color(0.2f, 0.88f, 1f, 1f)`. `totalFeedbackScale` default `1.6f`. Overlay coverage = mask pixel alpha `>= 0.5`.

## Review Focus

1. **Overlay mask imported without Read/Write** — `GetPixel` would throw mid-QTE; expected: shot treated as uncovered, one warning, QTE keeps working. Pinned by `OverlayCoverageTests.IsCovered_nonReadableTexture_returnsFalse` (Task 2) and the guard in `AimViewController.IsOverlayCovered` (Task 4).
2. **Retargeting from an armored enemy to an unarmored one** — the previous vest must not linger visually or keep blocking. Pinned by `ConfirmTarget_enemyWithoutOverlay_clearsAimOverlay` and `ShotCountConfirm_noEnemies_clearsAimOverlay` (Task 3).
3. **Overlay pool with empty/null entries** (designer leaves a blank slot) — expected: blanks are skipped, never picked. Pinned by `OverlayPickerTests.Pick_skipsNullEntries` / `Pick_allNull_returnsNull` (Task 2).
4. **`Overlay_Vest` with no art assigned yet** — expected: enemy plays like a Grunt, no overlay drawn, nothing blocked, no errors. Pinned by `SilhouetteOverlayTests.Resolve_standing_missingSprites_returnsNull` (Task 2) and the manual check in Task 7.
5. **Total popup surviving into the next QTE** (dismissed mid-sequence or a new shot started) — expected: cleared on every `Show()`/`Hide()`. Not unit-testable (MonoBehaviour + scene); pinned by `ClearTotalFeedback()` calls in Task 5 and the manual check in Task 7.

---

## File Structure

| File | Status | Responsibility |
|------|--------|----------------|
| `Game/CrimsonDraft/Assets/Scripts/Combat/Data/OverlayKind.cs` | Create | Overlay kind enum (Armor now, WeakPoint later) |
| `Game/CrimsonDraft/Assets/Scripts/Combat/Shooting/ArmorRules.cs` | Create | "Is this pellet blocked by armor?" rule |
| `Game/CrimsonDraft/Assets/Scripts/Combat/Data/ActiveOverlay.cs` | Create | Resolved overlay for one pose (kind + visible + mask sprite) |
| `Game/CrimsonDraft/Assets/Scripts/Combat/Data/SilhouetteOverlay.cs` | Create | Overlay asset (sprites per pose) + `Resolve(staggered)` |
| `Game/CrimsonDraft/Assets/Scripts/Combat/Data/OverlayPicker.cs` | Create | Pick one overlay from a pool |
| `Game/CrimsonDraft/Assets/Scripts/Combat/Shooting/OverlayCoverage.cs` | Create | Sample a mask sprite at a UV → covered? |
| `Game/CrimsonDraft/Assets/Scripts/Combat/Shooting/TotalFeedback.cs` | Create | Total-popup text rule |
| `Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponData.cs` | Modify | `armorDamageMultiplier` |
| `Game/CrimsonDraft/Assets/Scripts/Inventory/MeleeWeaponData.cs` | Modify | `armorDamageMultiplier` |
| `Game/CrimsonDraft/Assets/Scripts/Combat/Data/EnemyData.cs` | Modify | `overlayPool` |
| `Game/CrimsonDraft/Assets/Scripts/Combat/Data/ResolvedShot.cs` | Modify | `ArmorBlocked` |
| `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs` | Modify | `ComputeShotDamage(..., armorMultiplier)` |
| `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs` / `BattlefieldView.cs` | Modify | Roll + expose overlay |
| `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IAimView.cs` / `AimViewController.cs` | Modify | Overlay config, armor in resolution, blocked color, total popup |
| `Game/CrimsonDraft/Assets/Scripts/Combat/States/TargetSelectionState.cs` / `ShotCountSelectionState.cs` | Modify | Call `ConfigureOverlay` |
| `Game/CrimsonDraft/Assets/Tests/EditMode/TestSprites.cs` | Create | Throwaway in-memory sprites for tests |
| `Game/CrimsonDraft/Assets/Tests/EditMode/ArmorRulesTests.cs` | Create | Task 1 |
| `Game/CrimsonDraft/Assets/Tests/EditMode/SilhouetteOverlayTests.cs` | Create | Task 2 (overlay, picker, coverage, EnemyData) |
| `Game/CrimsonDraft/Assets/Tests/EditMode/TotalFeedbackTests.cs` | Create | Task 5 |
| `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs` / `CombatOrchestratorTests.cs` | Modify | New damage tests, fakes implement new interface members, overlay flow tests |
| `Scenes/Production/Combat.unity`, `Scenes/Test/Combat_Decor.unity` | Modify | `OverlayImage` + `TotalFeedbackAnchor` under the QTE silhouette |
| `Data/...` assets, `Scenes/Production/Navigation.unity` | Modify/Create | MP7, ItemDatabase, pickup, vest overlay, armored enemy, encounter |
| `Design/GDD/crimson-draft-gdd.md` | Modify | §5.r + damage tables + changelog v0.18 |

(All `Scenes/` and `Data/` paths are under `Game/CrimsonDraft/Assets/`.)

---

### Task 1: Armor damage rules and weapon multipliers

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/OverlayKind.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Shooting/ArmorRules.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs` (`ComputeShotDamage`, ~line 270)
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponData.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/MeleeWeaponData.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/ArmorRulesTests.cs` (create)
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs` (add next to the existing `ComputeShotDamage_*` tests, ~line 557)

**Interfaces:**
- Produces: `enum OverlayKind { Armor = 0 }`; `static bool ArmorRules.IsBlocked(ShotZone zone, bool covered, OverlayKind? kind, float armorDamageMultiplier)`; `internal static int CombatMenuController.ComputeShotDamage(ShotZone zone, float precisionMultiplier, int baseDamage = BaseDamage, float armorMultiplier = 1f)`; `float WeaponData.ArmorDamageMultiplier`; `float MeleeWeaponData.ArmorDamageMultiplier`.

- [ ] **Step 1: Write the failing tests**

Create `Game/CrimsonDraft/Assets/Tests/EditMode/ArmorRulesTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Combat;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class ArmorRulesTests
    {
        [Test]
        public void IsBlocked_miss_neverBlocks() =>
            Assert.IsFalse(ArmorRules.IsBlocked(ShotZone.Miss, covered: true, OverlayKind.Armor, 0.25f));

        [Test]
        public void IsBlocked_uncovered_doesNotBlock() =>
            Assert.IsFalse(ArmorRules.IsBlocked(ShotZone.Torso, covered: false, OverlayKind.Armor, 0.25f));

        [Test]
        public void IsBlocked_noOverlay_doesNotBlock() =>
            Assert.IsFalse(ArmorRules.IsBlocked(ShotZone.Torso, covered: true, kind: null, 0.25f));

        [Test]
        public void IsBlocked_fullPenetrationWeapon_doesNotBlock() =>
            Assert.IsFalse(ArmorRules.IsBlocked(ShotZone.Torso, covered: true, OverlayKind.Armor, 1f));

        [Test]
        public void IsBlocked_coveredArmorWithReducedMultiplier_blocks() =>
            Assert.IsTrue(ArmorRules.IsBlocked(ShotZone.Torso, covered: true, OverlayKind.Armor, 0.25f));

        [Test]
        public void IsBlocked_anyBodyZoneUnderArmor_blocks() =>
            Assert.IsTrue(ArmorRules.IsBlocked(ShotZone.Arms, covered: true, OverlayKind.Armor, 0.5f));

        [Test]
        public void WeaponData_defaultArmorDamageMultiplier_isQuarter()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            Assert.AreEqual(0.25f, weapon.ArmorDamageMultiplier, 1e-6f);
            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void MeleeWeaponData_defaultArmorDamageMultiplier_isQuarter()
        {
            var melee = ScriptableObject.CreateInstance<MeleeWeaponData>();
            Assert.AreEqual(0.25f, melee.ArmorDamageMultiplier, 1e-6f);
            Object.DestroyImmediate(melee);
        }
    }
}
```

Add to `CombatMenuControllerTests.cs`, right after `ComputeShotDamage_miss_returns0`:

```csharp
        [Test]
        public void ComputeShotDamage_torso_armorQuarter_returns5()
        {
            Assert.AreEqual(5, CombatMenuController.ComputeShotDamage(ShotZone.Torso, 1f, 20, 0.25f));
        }

        [Test]
        public void ComputeShotDamage_head_graze_armorHalf_returns10()
        {
            Assert.AreEqual(10, CombatMenuController.ComputeShotDamage(ShotZone.Head, 0.5f, 20, 0.5f));
        }

        [Test]
        public void ComputeShotDamage_armorMultiplierOne_isUnchanged()
        {
            Assert.AreEqual(20, CombatMenuController.ComputeShotDamage(ShotZone.Torso, 1f, 20, 1f));
        }

        [Test]
        public void ComputeShotDamage_miss_withArmor_returns0()
        {
            Assert.AreEqual(0, CombatMenuController.ComputeShotDamage(ShotZone.Miss, 1f, 20, 0.25f));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Compile via `refresh_unity`, then `read_console` (errors). Expected: compile errors `CS0103: The name 'ArmorRules' does not exist`, `CS0246: 'OverlayKind' could not be found`, `CS1501: No overload for method 'ComputeShotDamage' takes 4 arguments`, `CS1061: 'WeaponData' does not contain a definition for 'ArmorDamageMultiplier'`.

- [ ] **Step 3: Implement**

Create `Game/CrimsonDraft/Assets/Scripts/Combat/Data/OverlayKind.cs`:

```csharp
#nullable enable

namespace CrimsonDraft.Combat
{
    // Serialized on SilhouetteOverlay assets -- values are pinned so adding kinds (WeakPoint
    // for blisters) never shifts existing ones.
    public enum OverlayKind
    {
        Armor = 0,
    }
}
```

Create `Game/CrimsonDraft/Assets/Scripts/Combat/Shooting/ArmorRules.cs`:

```csharp
#nullable enable

namespace CrimsonDraft.Combat
{
    public static class ArmorRules
    {
        // Blocked only when the pellet hit the body (the zone mask decides that, an overlay
        // pixel outside the body never counts), landed on Armor coverage, and the weapon
        // doesn't fully penetrate it. A 1.0 multiplier (the MP7) is therefore never blocked.
        public static bool IsBlocked(ShotZone zone, bool covered, OverlayKind? kind, float armorDamageMultiplier) =>
            zone != ShotZone.Miss
            && covered
            && kind == OverlayKind.Armor
            && armorDamageMultiplier < 1f;
    }
}
```

In `CombatMenuController.cs`, replace `ComputeShotDamage`:

```csharp
        internal static int ComputeShotDamage(ShotZone zone, float precisionMultiplier, int baseDamage = BaseDamage, float armorMultiplier = 1f)
        {
            float zoneMult = zone switch
            {
                ShotZone.Head  => 2.0f,
                ShotZone.Torso => 1.0f,
                ShotZone.Arms  => 0.7f,
                ShotZone.Legs  => 0.8f,
                ShotZone.Hit   => 1.0f,
                _              => 0.0f,
            };
            return Mathf.RoundToInt(baseDamage * zoneMult * precisionMultiplier * armorMultiplier);
        }
```

In `WeaponData.cs`, after the `poiseDamage` field add:

```csharp

        // Fraction of damage that gets through a vest (SilhouetteOverlay of kind Armor) --
        // 1 means it fully penetrates (the MP7). Poise damage is never reduced by armor.
        [SerializeField, Range(0f, 1f)] private float armorDamageMultiplier = 0.25f;
```

and after `public int PoiseDamage => this.poiseDamage;` add:

```csharp
        public float             ArmorDamageMultiplier  => this.armorDamageMultiplier;
```

In `MeleeWeaponData.cs`, after `[SerializeField, Min(0)] private int poiseDamage = 10;` add:

```csharp

        // Fraction of damage that gets through a vest -- same meaning as WeaponData's.
        [SerializeField, Range(0f, 1f)] private float armorDamageMultiplier = 0.25f;
```

and next to its existing `PoiseDamage` getter add:

```csharp
        public float ArmorDamageMultiplier => this.armorDamageMultiplier;
```

- [ ] **Step 4: Run tests to verify they pass**

`run_tests` with `group_names: ["CrimsonDraft.Tests.ArmorRulesTests", "CrimsonDraft.Tests.CombatMenuControllerTests"]`. Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/OverlayKind.cs* Game/CrimsonDraft/Assets/Scripts/Combat/Shooting/ArmorRules.cs* Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponData.cs Game/CrimsonDraft/Assets/Scripts/Inventory/MeleeWeaponData.cs Game/CrimsonDraft/Assets/Tests/EditMode/ArmorRulesTests.cs* Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat): per-weapon armor damage multiplier and armor block rule"
```

(The `*` globs pick up the `.meta` files Unity generated.)

---

### Task 2: Silhouette overlay data, picker and coverage sampling

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/ActiveOverlay.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/SilhouetteOverlay.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/OverlayPicker.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Shooting/OverlayCoverage.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/EnemyData.cs`
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/TestSprites.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/SilhouetteOverlayTests.cs` (create)

**Interfaces:**
- Consumes: `OverlayKind` (Task 1); `IRandomSource` (`Combat/RandomSource.cs`: `int NextInt(int minInclusive, int maxExclusive)`); `internal static Vector2Int AimViewController.MapUvToTexturePixel(Sprite sprite, float u, float v)` (existing).
- Produces: `readonly struct ActiveOverlay(OverlayKind kind, Sprite visibleSprite, Sprite maskSprite)` with `Kind`, `VisibleSprite`, `MaskSprite`; `SilhouetteOverlay : ScriptableObject` with `Kind` and `ActiveOverlay? Resolve(bool staggered)`; `static SilhouetteOverlay? OverlayPicker.Pick(IReadOnlyList<SilhouetteOverlay?>? pool, IRandomSource random)`; `static bool OverlayCoverage.IsCovered(Sprite mask, float u, float v)` and `const float OverlayCoverage.CoveredAlphaThreshold = 0.5f`; `SilhouetteOverlay[] EnemyData.OverlayPool`; test helper `TestSprites` (`Solid(Color, bool readable = true)`, `LeftHalf(Color left, Color right)`, `IDisposable`).

- [ ] **Step 1: Write the test helper and failing tests**

Create `Game/CrimsonDraft/Assets/Tests/EditMode/TestSprites.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Tests
{
    // In-memory 4x4 sprites for tests that need real Sprite/Texture2D objects. Dispose
    // destroys everything it created.
    internal sealed class TestSprites : System.IDisposable
    {
        private const int Size = 4;
        private readonly List<Object> created = new List<Object>();

        public Sprite Solid(Color color, bool readable = true) => Make((x, y) => color, readable);

        public Sprite LeftHalf(Color left, Color right) => Make((x, y) => x < Size / 2 ? left : right, true);

        private Sprite Make(System.Func<int, int, Color> pixel, bool readable)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    tex.SetPixel(x, y, pixel(x, y));
            tex.Apply(false, makeNoLongerReadable: !readable);

            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
            this.created.Add(tex);
            this.created.Add(sprite);
            return sprite;
        }

        public void Dispose()
        {
            foreach (var obj in this.created)
                if (obj != null) Object.DestroyImmediate(obj);
            this.created.Clear();
        }
    }
}
```

Create `Game/CrimsonDraft/Assets/Tests/EditMode/SilhouetteOverlayTests.cs`:

```csharp
#nullable enable

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class SilhouetteOverlayTests
    {
        private TestSprites sprites = null!;

        [SetUp]
        public void SetUp() => this.sprites = new TestSprites();

        [TearDown]
        public void TearDown() => this.sprites.Dispose();

        private static SilhouetteOverlay MakeOverlay(
            Sprite? visible, Sprite? mask, Sprite? staggeredVisible = null, Sprite? staggeredMask = null)
        {
            var overlay = ScriptableObject.CreateInstance<SilhouetteOverlay>();
            SetField(overlay, "visibleSprite", visible);
            SetField(overlay, "maskSprite", mask);
            SetField(overlay, "staggeredVisibleSprite", staggeredVisible);
            SetField(overlay, "staggeredMaskSprite", staggeredMask);
            return overlay;
        }

        private static void SetField(object target, string name, object? value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, name);
            field!.SetValue(target, value);
        }

        // ── SilhouetteOverlay.Resolve ──

        [Test]
        public void Resolve_standing_returnsStandingPair()
        {
            Sprite visible = this.sprites.Solid(Color.cyan);
            Sprite mask    = this.sprites.Solid(Color.white);
            var overlay    = MakeOverlay(visible, mask);

            ActiveOverlay? result = overlay.Resolve(staggered: false);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(OverlayKind.Armor, result!.Value.Kind);
            Assert.AreSame(visible, result.Value.VisibleSprite);
            Assert.AreSame(mask, result.Value.MaskSprite);
        }

        [Test]
        public void Resolve_staggered_withSprites_returnsStaggeredPair()
        {
            Sprite stVisible = this.sprites.Solid(Color.blue);
            Sprite stMask    = this.sprites.Solid(Color.white);
            var overlay      = MakeOverlay(this.sprites.Solid(Color.cyan), this.sprites.Solid(Color.white), stVisible, stMask);

            ActiveOverlay? result = overlay.Resolve(staggered: true);

            Assert.IsTrue(result.HasValue);
            Assert.AreSame(stVisible, result!.Value.VisibleSprite);
            Assert.AreSame(stMask, result.Value.MaskSprite);
        }

        [Test]
        public void Resolve_staggered_withoutSprites_returnsNull()
        {
            var overlay = MakeOverlay(this.sprites.Solid(Color.cyan), this.sprites.Solid(Color.white));
            Assert.IsFalse(overlay.Resolve(staggered: true).HasValue);
        }

        [Test]
        public void Resolve_standing_missingSprites_returnsNull()
        {
            var overlay = MakeOverlay(visible: null, mask: null);
            Assert.IsFalse(overlay.Resolve(staggered: false).HasValue);
        }

        // ── OverlayPicker.Pick ──

        private sealed class FixedRandom : IRandomSource
        {
            public int Value;
            public int LastMaxExclusive = -1;
            public float NextFloat01() => 0f;
            public int NextInt(int minInclusive, int maxExclusive)
            {
                this.LastMaxExclusive = maxExclusive;
                return this.Value;
            }
        }

        [Test]
        public void Pick_emptyPool_returnsNull() =>
            Assert.IsNull(OverlayPicker.Pick(new SilhouetteOverlay?[0], new FixedRandom()));

        [Test]
        public void Pick_nullPool_returnsNull() =>
            Assert.IsNull(OverlayPicker.Pick(null, new FixedRandom()));

        [Test]
        public void Pick_allNull_returnsNull() =>
            Assert.IsNull(OverlayPicker.Pick(new SilhouetteOverlay?[] { null, null }, new FixedRandom()));

        [Test]
        public void Pick_singleEntry_returnsIt()
        {
            var vest = MakeOverlay(null, null);
            Assert.AreSame(vest, OverlayPicker.Pick(new SilhouetteOverlay?[] { vest }, new FixedRandom()));
        }

        [Test]
        public void Pick_skipsNullEntries()
        {
            var a      = MakeOverlay(null, null);
            var b      = MakeOverlay(null, null);
            var random = new FixedRandom { Value = 1 };

            var result = OverlayPicker.Pick(new SilhouetteOverlay?[] { a, null, b }, random);

            Assert.AreSame(b, result);
            Assert.AreEqual(2, random.LastMaxExclusive); // rolled over the 2 real candidates only
        }

        // ── OverlayCoverage.IsCovered ──

        [Test]
        public void IsCovered_opaquePixel_returnsTrue() =>
            Assert.IsTrue(OverlayCoverage.IsCovered(this.sprites.Solid(Color.white), 0.5f, 0.5f));

        [Test]
        public void IsCovered_transparentPixel_returnsFalse() =>
            Assert.IsFalse(OverlayCoverage.IsCovered(this.sprites.Solid(new Color(1f, 1f, 1f, 0f)), 0.5f, 0.5f));

        [Test]
        public void IsCovered_samplesTheRequestedUv()
        {
            Sprite mask = this.sprites.LeftHalf(Color.white, new Color(0f, 0f, 0f, 0f));
            Assert.IsTrue(OverlayCoverage.IsCovered(mask, 0.1f, 0.5f));
            Assert.IsFalse(OverlayCoverage.IsCovered(mask, 0.9f, 0.5f));
        }

        [Test]
        public void IsCovered_nonReadableTexture_returnsFalse() =>
            Assert.IsFalse(OverlayCoverage.IsCovered(this.sprites.Solid(Color.white, readable: false), 0.5f, 0.5f));

        // ── EnemyData ──

        [Test]
        public void EnemyData_defaultOverlayPool_isEmptyNotNull()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyData>();
            Assert.NotNull(enemy.OverlayPool);
            Assert.AreEqual(0, enemy.OverlayPool.Length);
            Object.DestroyImmediate(enemy);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Compile; expected `CS0246` for `SilhouetteOverlay`, `ActiveOverlay`; `CS0103` for `OverlayPicker`, `OverlayCoverage`; `CS1061` for `EnemyData.OverlayPool`.

- [ ] **Step 3: Implement**

Create `Game/CrimsonDraft/Assets/Scripts/Combat/Data/ActiveOverlay.cs`:

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // One overlay resolved for the enemy's current pose (standing or staggered): what the aim
    // view draws over the silhouette and what it samples to decide coverage.
    public readonly struct ActiveOverlay
    {
        public OverlayKind Kind          { get; }
        public Sprite      VisibleSprite { get; }
        public Sprite      MaskSprite    { get; }

        public ActiveOverlay(OverlayKind kind, Sprite visibleSprite, Sprite maskSprite)
        {
            this.Kind          = kind;
            this.VisibleSprite = visibleSprite;
            this.MaskSprite    = maskSprite;
        }
    }
}
```

Create `Game/CrimsonDraft/Assets/Scripts/Combat/Data/SilhouetteOverlay.cs`:

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // A layer drawn over an enemy's aim silhouette (a vest now, weak-point blisters later).
    // The zone mask still decides where a pellet hit; the overlay's mask only adds a modifier
    // on top. Every sprite must share the base silhouette sprite's size and pivot, and mask
    // sprites need Read/Write enabled (sampled with GetPixel).
    [CreateAssetMenu(fileName = "SilhouetteOverlay", menuName = "CrimsonDraft/Combat/Silhouette Overlay")]
    public sealed class SilhouetteOverlay : ScriptableObject
    {
        [SerializeField] private OverlayKind kind = OverlayKind.Armor;
        [SerializeField] private Sprite?     visibleSprite;
        [SerializeField] private Sprite?     maskSprite;
        [SerializeField] private Sprite?     staggeredVisibleSprite;
        [SerializeField] private Sprite?     staggeredMaskSprite;

        public OverlayKind Kind => this.kind;

        // null when that pose's art isn't assigned -- an overlay without art is neither drawn
        // nor blocking, so an asset created before its sprites exist is harmless.
        public ActiveOverlay? Resolve(bool staggered)
        {
            Sprite? visible = staggered ? this.staggeredVisibleSprite : this.visibleSprite;
            Sprite? mask    = staggered ? this.staggeredMaskSprite    : this.maskSprite;
            if (visible == null || mask == null) return null;
            return new ActiveOverlay(this.kind, visible, mask);
        }
    }
}
```

Create `Game/CrimsonDraft/Assets/Scripts/Combat/Data/OverlayPicker.cs`:

```csharp
#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Combat
{
    public static class OverlayPicker
    {
        // Exactly one overlay per enemy (or none for an empty pool) -- picking a single entry is
        // what guarantees an enemy never wears a vest and blisters at the same time. Blank pool
        // slots are skipped rather than rolled as "no overlay".
        public static SilhouetteOverlay? Pick(IReadOnlyList<SilhouetteOverlay?>? pool, IRandomSource random)
        {
            if (pool == null) return null;

            var candidates = new List<SilhouetteOverlay>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                SilhouetteOverlay? overlay = pool[i];
                if (overlay != null) candidates.Add(overlay);
            }

            return candidates.Count == 0 ? null : candidates[random.NextInt(0, candidates.Count)];
        }
    }
}
```

Create `Game/CrimsonDraft/Assets/Scripts/Combat/Shooting/OverlayCoverage.cs`:

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    public static class OverlayCoverage
    {
        public const float CoveredAlphaThreshold = 0.5f;

        // A texture without Read/Write can't be sampled (GetPixel throws) -- treated as
        // uncovered so a mis-imported mask degrades to "no armor" instead of breaking the QTE.
        public static bool IsCovered(Sprite mask, float u, float v)
        {
            Texture2D? tex = mask.texture;
            if (tex == null || !tex.isReadable) return false;

            Vector2Int px = AimViewController.MapUvToTexturePixel(mask, u, v);
            return tex.GetPixel(px.x, px.y).a >= CoveredAlphaThreshold;
        }
    }
}
```

In `EnemyData.cs`, after the `decapitationPelletThreshold` field add:

```csharp

        // Overlays this enemy may wear (vest, blister variants...). One is picked at random when
        // it spawns in combat (BattlefieldView.Populate); empty = no overlay.
        [SerializeField] private SilhouetteOverlay[] overlayPool = System.Array.Empty<SilhouetteOverlay>();
```

and after `public int DecapitationPelletThreshold => ...;` add:

```csharp
        public SilhouetteOverlay[] OverlayPool   => this.overlayPool;
```

- [ ] **Step 4: Run tests to verify they pass**

`run_tests` with `group_names: ["CrimsonDraft.Tests.SilhouetteOverlayTests"]`. Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/ActiveOverlay.cs* Game/CrimsonDraft/Assets/Scripts/Combat/Data/SilhouetteOverlay.cs* Game/CrimsonDraft/Assets/Scripts/Combat/Data/OverlayPicker.cs* Game/CrimsonDraft/Assets/Scripts/Combat/Shooting/OverlayCoverage.cs* Game/CrimsonDraft/Assets/Scripts/Combat/Data/EnemyData.cs Game/CrimsonDraft/Assets/Tests/EditMode/TestSprites.cs* Game/CrimsonDraft/Assets/Tests/EditMode/SilhouetteOverlayTests.cs*
git commit -m "feat(combat): silhouette overlay asset, pool picker and mask coverage"
```

---

### Task 3: Overlay flows from the battlefield to the aim view

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs` (after `GetEnemyHitMaskProfile`)
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs` (`EnemyRuntimeState` ~line 18, `Populate` ~line 155, after `GetEnemyHitMaskProfile` ~line 254)
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IAimView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs` (fields, `ConfigureWeapon`, `ConfigureMeleeWeapon`, new `ConfigureOverlay`)
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/TargetSelectionState.cs` (~line 86)
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/ShotCountSelectionState.cs` (~line 67)
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs` (fakes + 3 tests after `ConfirmTarget_configuresAimWithSelectedEnemyMaskProfile`)
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatOrchestratorTests.cs` (`FakeBattlefieldView`)

**Interfaces:**
- Consumes: `ActiveOverlay`, `SilhouetteOverlay.Resolve`, `OverlayPicker.Pick`, `EnemyData.OverlayPool` (Task 2); `WeaponData.ArmorDamageMultiplier`, `MeleeWeaponData.ArmorDamageMultiplier`, `OverlayKind` (Task 1); `TestSprites` (Task 2).
- Produces: `ActiveOverlay? IBattlefieldView.GetEnemyOverlay(int slotIndex)`; `void IAimView.ConfigureOverlay(ActiveOverlay? overlay)`; `AimViewController` private state used by Task 4: `Sprite? activeOverlayMaskSprite`, `OverlayKind? activeOverlayKind`, `float activeArmorDamageMultiplier`, `bool warnedUnreadableOverlayMask`, serialized `Image? overlayImage`.

- [ ] **Step 1: Write the failing tests and update fakes**

In `CombatMenuControllerTests.cs` → `FakeAimView`, add:

```csharp
            public ActiveOverlay? LastConfiguredOverlay { get; private set; }
            public int ConfigureOverlayCallCount { get; private set; }
            public void ConfigureOverlay(ActiveOverlay? overlay)
            {
                this.ConfigureOverlayCallCount++;
                this.LastConfiguredOverlay = overlay;
            }
```

In `CombatMenuControllerTests.cs` → `FakeBattlefieldView`, next to `maskBySlot` / `SetMaskProfile`, add:

```csharp
            private readonly Dictionary<int, ActiveOverlay> overlayBySlot = new Dictionary<int, ActiveOverlay>();
            public void SetOverlay(int slotIndex, ActiveOverlay overlay) => this.overlayBySlot[slotIndex] = overlay;
            public ActiveOverlay? GetEnemyOverlay(int slotIndex) =>
                this.overlayBySlot.TryGetValue(slotIndex, out var overlay) ? overlay : (ActiveOverlay?)null;
```

In `CombatOrchestratorTests.cs` → `FakeBattlefieldView`, next to `GetEnemyHitMaskProfile`, add:

```csharp
            public ActiveOverlay? GetEnemyOverlay(int slotIndex) => null;
```

Add these tests to `CombatMenuControllerTests.cs` right after `ConfirmTarget_configuresAimWithSelectedEnemyMaskProfile`:

```csharp
        [Test]
        public void ConfirmTarget_configuresAimWithSelectedEnemyOverlay()
        {
            using var sprites = new TestSprites();
            var overlay = new ActiveOverlay(OverlayKind.Armor, sprites.Solid(Color.cyan), sprites.Solid(Color.white));
            this.battlefieldView.SetOccupiedSlots(new[] { 2 });
            this.battlefieldView.SetOverlay(2, overlay);

            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);
            InvokeConfirm(c); // shot count -> target selection
            InvokeConfirm(c); // target -> aiming

            Assert.AreEqual(1, this.aimView.ConfigureOverlayCallCount);
            Assert.IsTrue(this.aimView.LastConfiguredOverlay.HasValue);
            Assert.AreSame(overlay.MaskSprite, this.aimView.LastConfiguredOverlay!.Value.MaskSprite);
        }

        [Test]
        public void ConfirmTarget_enemyWithoutOverlay_clearsAimOverlay()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 2 });

            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);
            InvokeConfirm(c);
            InvokeConfirm(c);

            Assert.AreEqual(1, this.aimView.ConfigureOverlayCallCount); // called even with nothing to show
            Assert.IsFalse(this.aimView.LastConfiguredOverlay.HasValue);
        }

        [Test]
        public void ShotCountConfirm_noEnemies_clearsAimOverlay()
        {
            this.battlefieldView.SetOccupiedSlots(System.Array.Empty<int>());

            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);
            InvokeConfirm(c); // no enemies -> straight to aiming

            Assert.AreEqual(1, this.aimView.ConfigureOverlayCallCount);
            Assert.IsFalse(this.aimView.LastConfiguredOverlay.HasValue);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Compile, then run `group_names: ["CrimsonDraft.Tests.CombatMenuControllerTests"]`. The interfaces haven't changed yet, so the fakes' new members are just extra methods and everything compiles. Expected: the three new tests **FAIL** with `Expected: 1 But was: 0` on `ConfigureOverlayCallCount`. (A `CS0246 ActiveOverlay` compile error means Task 2 isn't in place — stop.)

- [ ] **Step 3: Implement**

`IBattlefieldView.cs`, after `AimHitMaskProfile? GetEnemyHitMaskProfile(int slotIndex);`:

```csharp
        ActiveOverlay? GetEnemyOverlay(int slotIndex);
```

`BattlefieldView.cs`:
- In `EnemyRuntimeState`, after `public bool DeathFinalized;` add:

```csharp
            // Rolled once from EnemyData.OverlayPool in Populate; null = no overlay.
            public SilhouetteOverlay? Overlay;
```

- In `Populate`, add `Overlay = OverlayPicker.Pick(enemy.OverlayPool, this.enemyStatRandom),` as the last initializer of the `new EnemyRuntimeState { ... }` block (after `DeathFinalized = false`, adding the comma).
- After `GetEnemyHitMaskProfile`, add:

```csharp
        public ActiveOverlay? GetEnemyOverlay(int slotIndex)
        {
            if (!this.enemyStateBySlot.TryGetValue(slotIndex, out var state) || state.Overlay == null)
                return null;

            return state.Overlay.Resolve(IsEnemyStaggered(slotIndex));
        }
```

`IAimView.cs`, after `void ConfigureHitMask(AimHitMaskProfile? profile);`:

```csharp
        void ConfigureOverlay(ActiveOverlay? overlay);
```

`AimViewController.cs`:
- After `[SerializeField] private Image silhouetteImage = null!;` add:

```csharp
        // Child of silhouetteImage with the same rect -- draws the active overlay (vest) on top
        // of the silhouette so it shakes with it. Optional: without it overlays still apply,
        // they just aren't drawn.
        [SerializeField] private Image?        overlayImage;
```

- In the private state region (next to `private Sprite? activeZoneMaskSprite;`) add:

```csharp
        private Sprite?      activeOverlayMaskSprite;
        private OverlayKind? activeOverlayKind;
        private float        activeArmorDamageMultiplier = 1f;
        private bool         warnedUnreadableOverlayMask;
```

- In `ConfigureWeapon`, add as the first line of the body:

```csharp
            this.activeArmorDamageMultiplier = weaponData?.ArmorDamageMultiplier ?? 1f;
```

- In `ConfigureMeleeWeapon`, add as the first line of the body:

```csharp
            this.activeArmorDamageMultiplier = meleeData?.ArmorDamageMultiplier ?? 1f;
```

- After `ConfigureHitMask`, add:

```csharp
        public void ConfigureOverlay(ActiveOverlay? overlay)
        {
            this.activeOverlayMaskSprite     = overlay?.MaskSprite;
            this.activeOverlayKind           = overlay?.Kind;
            this.warnedUnreadableOverlayMask = false;

            if (this.overlayImage == null) return;
            this.overlayImage.sprite  = overlay?.VisibleSprite;
            this.overlayImage.enabled = overlay.HasValue;
        }
```

`TargetSelectionState.cs`, right after the `this.aimView.ConfigureHitMask(...);` statement:

```csharp
            this.aimView.ConfigureOverlay(
                this.context.CurrentTargetSlot >= 0
                    ? this.battlefieldView.GetEnemyOverlay(this.context.CurrentTargetSlot)
                    : null);
```

`ShotCountSelectionState.cs`, in the `enemies.Length == 0` branch, right after `this.aimView.ConfigureHitMask(null);`:

```csharp
                this.aimView.ConfigureOverlay(null);
```

- [ ] **Step 4: Run tests to verify they pass**

Compile (expect zero `CS` errors), then `run_tests` with `group_names: ["CrimsonDraft.Tests.CombatMenuControllerTests", "CrimsonDraft.Tests.CombatOrchestratorTests"]`. Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs Game/CrimsonDraft/Assets/Scripts/Combat/UI/IAimView.cs Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs Game/CrimsonDraft/Assets/Scripts/Combat/States/TargetSelectionState.cs Game/CrimsonDraft/Assets/Scripts/Combat/States/ShotCountSelectionState.cs Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs Game/CrimsonDraft/Assets/Tests/EditMode/CombatOrchestratorTests.cs
git commit -m "feat(combat): roll an overlay per enemy and hand it to the aim view"
```

---

### Task 4: Apply armor during shot resolution and color blocked hits

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/ResolvedShot.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs` (`BuildResolvedShots` ~line 430, `ResolvePendingShotsAsync` ~line 470, `SpawnShotFeedbackVisual` ~line 509, `SampleSilhouette` ~line 640, serialized colors ~line 46)
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/ArmorRulesTests.cs` (add `ResolvedShot` cases)

**Interfaces:**
- Consumes: `ArmorRules.IsBlocked`, `ComputeShotDamage(..., armorMultiplier)` (Task 1); `OverlayCoverage.IsCovered` (Task 2); `activeOverlayMaskSprite`, `activeOverlayKind`, `activeArmorDamageMultiplier`, `warnedUnreadableOverlayMask` (Task 3).
- Produces: `ResolvedShot(int index, int bulletIndex, Vector2 normalizedPos, ShotZone zone, ShotPrecision precision, int damage, bool armorBlocked = false)` with `bool ArmorBlocked`; `AimViewController` private `bool TryGetSilhouetteUv(Vector2 shotLocal, out float u, out float v)`.

- [ ] **Step 1: Write the failing tests**

Append to `ArmorRulesTests.cs` (inside the class):

```csharp
        [Test]
        public void ResolvedShot_defaultsToNotArmorBlocked()
        {
            var shot = new ResolvedShot(0, 0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20);
            Assert.IsFalse(shot.ArmorBlocked);
        }

        [Test]
        public void ResolvedShot_carriesArmorBlocked()
        {
            var shot = new ResolvedShot(0, 0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 5, armorBlocked: true);
            Assert.IsTrue(shot.ArmorBlocked);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Compile. Expected: `CS1061: 'ResolvedShot' does not contain a definition for 'ArmorBlocked'` and `CS1739: ... does not have a parameter named 'armorBlocked'`.

- [ ] **Step 3: Implement**

`ResolvedShot.cs` — add the property and the optional constructor parameter:

```csharp
        public int           Damage        { get; }
        // True when the pellet landed on armor the weapon doesn't fully penetrate -- Damage is
        // already reduced; this only drives feedback (blocked-hit popup color).
        public bool          ArmorBlocked  { get; }

        public ResolvedShot(int index, int bulletIndex, Vector2 normalizedPos, ShotZone zone, ShotPrecision precision, int damage, bool armorBlocked = false)
        {
            this.Index         = index;
            this.BulletIndex   = bulletIndex;
            this.NormalizedPos = normalizedPos;
            this.Zone          = zone;
            this.Precision     = precision;
            this.Damage        = damage;
            this.ArmorBlocked  = armorBlocked;
        }
```

`AimViewController.cs`:

1. After `[SerializeField] private Color missFeedbackColor = ...;` add:

```csharp
        [SerializeField] private Color         armorBlockedFeedbackColor = new Color(0.2f, 0.88f, 1f, 1f); // #33E0FF
```

2. Extract the UV math from `SampleSilhouette` into a helper. Add above `SampleSilhouette`:

```csharp
        // Shot position (aimSpace local) -> normalized UV inside the silhouette's rect. Shared by
        // the zone mask and the overlay mask, which are both laid out in that same rect.
        private bool TryGetSilhouetteUv(Vector2 shotLocal, out float u, out float v)
        {
            u = 0f;
            v = 0f;
            if (this.silhouetteImage == null) return false;

            var worldPos   = this.aimSpace.TransformPoint(new Vector3(shotLocal.x, shotLocal.y, 0f));
            var silRt      = this.silhouetteImage.rectTransform;
            var localInSil = silRt.InverseTransformPoint(worldPos);
            var rect       = silRt.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;

            u = Mathf.Clamp01((localInSil.x - rect.xMin) / rect.width);
            v = Mathf.Clamp01((localInSil.y - rect.yMin) / rect.height);
            return true;
        }
```

   In `SampleSilhouette`, replace the block from `var worldPos = ...` through `float v = ...;` with:

```csharp
            if (!this.TryGetSilhouetteUv(shotLocal, out float u, out float v))
                return null;
            var silRt = this.silhouetteImage.rectTransform;
            var rect  = silRt.rect;
```

   (`silRt` and `rect` are still used by the `#if UNITY_EDITOR` gizmo block below.)

3. Add after `SampleSilhouette`:

```csharp
        private bool IsOverlayCovered(Vector2 shotLocal)
        {
            Sprite? mask = this.activeOverlayMaskSprite;
            if (mask == null) return false;

            if (!mask.texture.isReadable)
            {
                if (!this.warnedUnreadableOverlayMask)
                {
                    Debug.LogWarning($"[AimView] Overlay mask '{mask.name}' is not Read/Write enabled -- every shot is treated as uncovered.");
                    this.warnedUnreadableOverlayMask = true;
                }
                return false;
            }

            return this.TryGetSilhouetteUv(shotLocal, out float u, out float v)
                && OverlayCoverage.IsCovered(mask, u, v);
        }
```

4. In `BuildResolvedShots`, replace the two lines

```csharp
                    int                 damage     = CombatMenuController.ComputeShotDamage(zone, precMult, this.activeBaseDamage);
                    resolved.Add(new ResolvedShot(flatIndex, bulletIndex, normalized, zone, precision, damage));
```

   with:

```csharp
                    bool                armorBlocked = ArmorRules.IsBlocked(
                        zone, this.IsOverlayCovered(shotLocal), this.activeOverlayKind, this.activeArmorDamageMultiplier);
                    int                 damage     = CombatMenuController.ComputeShotDamage(
                        zone, precMult, this.activeBaseDamage, armorBlocked ? this.activeArmorDamageMultiplier : 1f);
                    resolved.Add(new ResolvedShot(flatIndex, bulletIndex, normalized, zone, precision, damage, armorBlocked));
```

5. Change `SpawnShotFeedbackVisual`'s signature to

```csharp
        private void SpawnShotFeedbackVisual(Vector2 normalizedPos, int damage, bool isMiss, bool armorBlocked = false)
```

   and its color line to

```csharp
            var baseColor = isMiss ? this.missFeedbackColor
                : armorBlocked ? this.armorBlockedFeedbackColor
                : this.hitFeedbackColor;
```

6. In `ResolvePendingShotsAsync`, change the per-shot call to

```csharp
                this.SpawnShotFeedbackVisual(shot.NormalizedPos, shot.Damage, shot.Zone == ShotZone.Miss, shot.ArmorBlocked);
```

- [ ] **Step 4: Run tests to verify they pass**

Compile (zero `CS` errors), then `run_tests` with `group_names: ["CrimsonDraft.Tests.ArmorRulesTests", "CrimsonDraft.Tests.AimViewControllerTests", "CrimsonDraft.Tests.CombatMenuControllerTests"]`. Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/ResolvedShot.cs Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs Game/CrimsonDraft/Assets/Tests/EditMode/ArmorRulesTests.cs
git commit -m "feat(combat): reduce vest hits by the weapon's armor multiplier and highlight them"
```

---

### Task 5: Total-damage popup

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Shooting/TotalFeedback.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs` (fields, `Show`, `Hide`, `ResolvePendingShotsAsync`, new spawn/clear helpers)
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/TotalFeedbackTests.cs` (create)

**Interfaces:**
- Consumes: `ResolvedShot` (Task 4).
- Produces: `static string? TotalFeedback.Format(IReadOnlyList<ResolvedShot> shots)`, `const string TotalFeedback.MissText = "MISS"`; serialized `RectTransform? totalFeedbackAnchor`, `float totalFeedbackScale` on `AimViewController` (wired in Task 6).

- [ ] **Step 1: Write the failing tests**

Create `Game/CrimsonDraft/Assets/Tests/EditMode/TotalFeedbackTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class TotalFeedbackTests
    {
        private static ResolvedShot Shot(ShotZone zone, int damage, int index = 0) =>
            new ResolvedShot(index, index, Vector2.zero, zone, ShotPrecision.Normal, damage);

        [Test]
        public void Format_noShots_returnsNull() =>
            Assert.IsNull(TotalFeedback.Format(new ResolvedShot[0]));

        [Test]
        public void Format_singleShot_returnsNull() =>
            Assert.IsNull(TotalFeedback.Format(new[] { Shot(ShotZone.Torso, 20) }));

        [Test]
        public void Format_allMiss_returnsMiss() =>
            Assert.AreEqual("MISS", TotalFeedback.Format(new[] { Shot(ShotZone.Miss, 0, 0), Shot(ShotZone.Miss, 0, 1) }));

        [Test]
        public void Format_mixed_returnsNegativeSumOfDamage() =>
            Assert.AreEqual("-25", TotalFeedback.Format(new[]
            {
                Shot(ShotZone.Torso, 20, 0),
                Shot(ShotZone.Miss, 0, 1),
                Shot(ShotZone.Arms, 5, 2),
            }));

        [Test]
        public void Format_hitsForZeroDamage_stillShowsTotalNotMiss() =>
            Assert.AreEqual("-0", TotalFeedback.Format(new[] { Shot(ShotZone.Legs, 0, 0), Shot(ShotZone.Legs, 0, 1) }));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Compile. Expected: `CS0103: The name 'TotalFeedback' does not exist in the current context`.

- [ ] **Step 3: Implement**

Create `Game/CrimsonDraft/Assets/Scripts/Combat/Shooting/TotalFeedback.cs`:

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public static class TotalFeedback
    {
        public const string MissText = "MISS";

        // Text for the end-of-QTE total popup, or null when there's nothing worth totaling
        // (0 or 1 resolved shots -- the single per-shot popup already shows that number).
        // "MISS" only when no pellet touched the body at all; a body hit for 0 still totals.
        public static string? Format(IReadOnlyList<ResolvedShot> shots)
        {
            if (shots == null || shots.Count < 2) return null;

            int  total  = 0;
            bool anyHit = false;
            for (int i = 0; i < shots.Count; i++)
            {
                if (shots[i].Zone != ShotZone.Miss) anyHit = true;
                total += Mathf.Max(0, shots[i].Damage);
            }

            return anyHit ? $"-{total}" : MissText;
        }
    }
}
```

`AimViewController.cs`:

1. After the `armorBlockedFeedbackColor` field add:

```csharp
        // Where the end-of-QTE total popup appears (above the silhouette, outside aimSpace so
        // Hide()'s aimSpace sweep never touches it). Optional: without it the total is skipped.
        [SerializeField] private RectTransform? totalFeedbackAnchor;
        [SerializeField] private float          totalFeedbackScale = 1.6f;
```

2. In the private state region add:

```csharp
        private GameObject? totalFeedbackInstance;
        private bool        warnedMissingTotalAnchor;
```

3. Add these helpers next to `SpawnShotFeedbackVisual`:

```csharp
        // Unlike per-shot popups this one never fades and isn't tracked in activeFeedback, so it
        // can't be pruned by maxConcurrentFeedback -- it stays until the QTE is dismissed.
        private void SpawnTotalFeedback(ResolvedShot[] shots)
        {
            string? label = TotalFeedback.Format(shots);
            if (label == null || this.feedbackTextPrefab == null) return;

            if (this.totalFeedbackAnchor == null)
            {
                if (!this.warnedMissingTotalAnchor)
                {
                    Debug.LogWarning("[AimView] Total feedback anchor is not assigned -- skipping the total popup.");
                    this.warnedMissingTotalAnchor = true;
                }
                return;
            }

            this.ClearTotalFeedback();
            var go = Instantiate(this.feedbackTextPrefab, this.totalFeedbackAnchor);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale    = Vector3.one * this.totalFeedbackScale;

            var text = go.GetComponent<TMP_Text>() ?? go.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = label;
                var color = label == TotalFeedback.MissText ? this.missFeedbackColor : this.hitFeedbackColor;
                color.a    = 1f;
                text.color = color;
            }

            this.totalFeedbackInstance = go;
        }

        private void ClearTotalFeedback()
        {
            if (this.totalFeedbackInstance != null)
                Destroy(this.totalFeedbackInstance);
            this.totalFeedbackInstance = null;
        }
```

4. In `ResolvePendingShotsAsync`, right after the `for` loop and before `this.OnShotsResolved?.Invoke(this.pendingResolvedShots);`:

```csharp
            this.SpawnTotalFeedback(this.pendingResolvedShots);
```

5. In `Show()`, add as the first line: `this.ClearTotalFeedback();`
6. In `Hide()`, add right before `this.DetachFeedbackFromAimView();`: `this.ClearTotalFeedback();`

- [ ] **Step 4: Run tests to verify they pass**

Compile (zero `CS` errors), then `run_tests` with `group_names: ["CrimsonDraft.Tests.TotalFeedbackTests"]`. Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/Shooting/TotalFeedback.cs* Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs Game/CrimsonDraft/Assets/Tests/EditMode/TotalFeedbackTests.cs*
git commit -m "feat(combat): total-damage popup at the end of multi-shot QTEs"
```

---

### Task 6: Wire the overlay image and total anchor into the combat scenes

The QTE's `AimViewController` lives directly in `Scenes/Production/Combat.unity` and `Scenes/Test/Combat_Decor.unity` (not in a prefab). `Prefabs/UI/AimDebuffPreviewPanel.prefab` also has one; its new fields stay unassigned (both are optional).

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scenes/Production/Combat.unity`
- Modify: `Game/CrimsonDraft/Assets/Scenes/Test/Combat_Decor.unity`

**Interfaces:**
- Consumes: serialized `overlayImage` (Task 3), `totalFeedbackAnchor` (Task 5) on `AimViewController`.

- [ ] **Step 1: Check the editor has no unsaved scene changes**

UnityMCP `manage_scene` `get_loaded_scenes`. If any loaded scene is dirty, stop and ask the user to save first.

- [ ] **Step 2: Create and wire the objects in both scenes**

UnityMCP `execute_code`:

```csharp
var report = new System.Text.StringBuilder();
foreach (var path in new[] { "Assets/Scenes/Production/Combat.unity", "Assets/Scenes/Test/Combat_Decor.unity" })
{
    var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(path);
    bool wasLoaded = scene.isLoaded;
    if (!wasLoaded)
        scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);

    CrimsonDraft.Combat.AimViewController? aim = null;
    foreach (var root in scene.GetRootGameObjects())
    {
        aim = root.GetComponentInChildren<CrimsonDraft.Combat.AimViewController>(true);
        if (aim != null) break;
    }
    if (aim == null) { report.AppendLine(path + ": no AimViewController"); continue; }

    var so  = new UnityEditor.SerializedObject(aim);
    var sil = (UnityEngine.UI.Image)so.FindProperty("silhouetteImage").objectReferenceValue;

    var overlayGo = new GameObject("OverlayImage", typeof(RectTransform), typeof(UnityEngine.UI.Image));
    overlayGo.layer = sil.gameObject.layer;
    var overlayRt = (RectTransform)overlayGo.transform;
    overlayRt.SetParent(sil.transform, false);
    overlayRt.anchorMin = Vector2.zero;
    overlayRt.anchorMax = Vector2.one;
    overlayRt.offsetMin = Vector2.zero;
    overlayRt.offsetMax = Vector2.zero;
    var overlayImg = overlayGo.GetComponent<UnityEngine.UI.Image>();
    overlayImg.raycastTarget  = false;
    overlayImg.preserveAspect = sil.preserveAspect;
    overlayImg.enabled        = false;

    var anchorGo = new GameObject("TotalFeedbackAnchor", typeof(RectTransform));
    anchorGo.layer = sil.gameObject.layer;
    var anchorRt = (RectTransform)anchorGo.transform;
    anchorRt.SetParent(sil.transform, false);
    anchorRt.anchorMin        = new Vector2(0.5f, 1f);
    anchorRt.anchorMax        = new Vector2(0.5f, 1f);
    anchorRt.sizeDelta        = Vector2.zero;
    anchorRt.anchoredPosition = new Vector2(0f, 12f);

    so.FindProperty("overlayImage").objectReferenceValue        = overlayImg;
    so.FindProperty("totalFeedbackAnchor").objectReferenceValue = anchorRt;
    so.ApplyModifiedProperties();

    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    report.AppendLine(path + ": wired under " + sil.name);
    if (!wasLoaded) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
}
return report.ToString();
```

Expected output: both scenes report `wired under <silhouette name>`.

- [ ] **Step 3: Verify**

`git diff --stat` shows only the two `.unity` files changed in this step. Re-run the full EditMode suite (`run_tests`, no filter). Expected: 550 + the new tests from Tasks 1–5, only the 5 known `ItemSocketInteractableTests` failures.

- [ ] **Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scenes/Production/Combat.unity Game/CrimsonDraft/Assets/Scenes/Test/Combat_Decor.unity
git commit -m "feat(combat): overlay image and total-damage anchor in the QTE"
```

---

### Task 7: Content — MP7, pickup, vest overlay, armored enemy and encounter

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Data/Inventory/Weapons/MP7.asset`, `P226.asset`, `P229.asset`, `Mk18.asset`, `Benelli_M4.asset`, `Data/Inventory/Melee/Knife.asset`, `Hatchet.asset`
- Modify: `Game/CrimsonDraft/Assets/Data/ItemDatabase.asset`
- Create: `Game/CrimsonDraft/Assets/Data/Enemies/Overlay_Vest.asset`, `Enemy_Armored.asset`, `Encounter_Armored.asset`
- Modify: `Game/CrimsonDraft/Assets/Data/Enemies/EncounterDatabase.asset`
- Modify: `Game/CrimsonDraft/Assets/Scenes/Production/Navigation.unity`

**Interfaces:**
- Consumes: `WeaponData`/`MeleeWeaponData.armorDamageMultiplier` (Task 1), `SilhouetteOverlay`, `EnemyData.overlayPool` (Task 2).

- [ ] **Step 1: Update weapon assets and the item database**

UnityMCP `execute_code`:

```csharp
var report = new System.Text.StringBuilder();
string W = "Assets/Data/Inventory/Weapons/", M = "Assets/Data/Inventory/Melee/";

var mp7 = UnityEditor.AssetDatabase.LoadAssetAtPath<CrimsonDraft.Inventory.WeaponData>(W + "MP7.asset");
var mp7So = new UnityEditor.SerializedObject(mp7);
mp7So.FindProperty("armorDamageMultiplier").floatValue = 1f;
mp7So.FindProperty("damage").intValue                = 16;
mp7So.FindProperty("poiseDamage").intValue           = 10;
mp7So.FindProperty("maxShotCount").intValue          = 6;
mp7So.ApplyModifiedProperties();
UnityEditor.EditorUtility.SetDirty(mp7);

foreach (var p in new[] { W + "P226.asset", W + "P229.asset", W + "Mk18.asset", W + "Benelli_M4.asset", M + "Knife.asset", M + "Hatchet.asset" })
{
    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(p);
    var so = new UnityEditor.SerializedObject(asset);
    so.FindProperty("armorDamageMultiplier").floatValue = 0.25f;
    so.ApplyModifiedProperties();
    UnityEditor.EditorUtility.SetDirty(asset);
    report.AppendLine(p + " -> 0.25");
}

var db = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/ItemDatabase.asset");
var dbSo = new UnityEditor.SerializedObject(db);
var items = dbSo.FindProperty("allItems");
bool present = false;
for (int i = 0; i < items.arraySize; i++)
    if (items.GetArrayElementAtIndex(i).objectReferenceValue == mp7) present = true;
if (!present)
{
    items.arraySize++;
    items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = mp7;
    dbSo.ApplyModifiedProperties();
    UnityEditor.EditorUtility.SetDirty(db);
}
UnityEditor.AssetDatabase.SaveAssets();
report.AppendLine("MP7 in ItemDatabase: " + (present ? "already" : "added"));
return report.ToString();
```

- [ ] **Step 2: Create the vest overlay, armored enemy and encounter**

UnityMCP `execute_code`:

```csharp
string E = "Assets/Data/Enemies/";

var vest = ScriptableObject.CreateInstance<CrimsonDraft.Combat.SilhouetteOverlay>();
UnityEditor.AssetDatabase.CreateAsset(vest, E + "Overlay_Vest.asset");

var grunt   = UnityEditor.AssetDatabase.LoadAssetAtPath<CrimsonDraft.Combat.EnemyData>(E + "Enemy_Grunt.asset");
var armored = Object.Instantiate(grunt);
armored.name = "Enemy_Armored";
UnityEditor.AssetDatabase.CreateAsset(armored, E + "Enemy_Armored.asset");
var aSo = new UnityEditor.SerializedObject(armored);
aSo.FindProperty("enemyId").stringValue = "armored";
var pool = aSo.FindProperty("overlayPool");
pool.arraySize = 1;
pool.GetArrayElementAtIndex(0).objectReferenceValue = vest;
aSo.ApplyModifiedProperties();

var pair      = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(E + "Encounter_Pair.asset");
var encounter = Object.Instantiate(pair);
encounter.name = "Encounter_Armored";
UnityEditor.AssetDatabase.CreateAsset(encounter, E + "Encounter_Armored.asset");
var eSo   = new UnityEditor.SerializedObject(encounter);
var slots = eSo.FindProperty("enemySlots");
for (int i = 0; i < slots.arraySize; i++)
    slots.GetArrayElementAtIndex(i).objectReferenceValue = i == 0 ? armored : null;
eSo.ApplyModifiedProperties();

var encDb  = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(E + "EncounterDatabase.asset");
var dbSo   = new UnityEditor.SerializedObject(encDb);
var list   = dbSo.FindProperty("encounters");
list.arraySize++;
list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = encounter;
dbSo.ApplyModifiedProperties();

UnityEditor.EditorUtility.SetDirty(armored);
UnityEditor.EditorUtility.SetDirty(encounter);
UnityEditor.EditorUtility.SetDirty(encDb);
UnityEditor.AssetDatabase.SaveAssets();
return "created Overlay_Vest, Enemy_Armored, Encounter_Armored";
```

Then verify `Enemy_Armored.asset` contains `enemyId: armored` and one `overlayPool` entry, and `Encounter_Armored.asset` has exactly one non-zero `enemySlots` entry.

- [ ] **Step 3: Add the MP7 pickup to the navigation scene**

Existing pickups in `Navigation.unity` all have an empty `pickupId`; the MP7 gets its own (`pickup_mp7`) so collecting it can't collide with them. UnityMCP `execute_code`:

```csharp
string path = "Assets/Scenes/Production/Navigation.unity";
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(path);
bool wasLoaded = scene.isLoaded;
if (!wasLoaded)
    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);

GameObject? demo = null;
foreach (var root in scene.GetRootGameObjects())
    foreach (var t in root.GetComponentsInChildren<Transform>(true))
        if (t.name == "Pickup_Weapon_Demo") demo = t.gameObject;
if (demo == null) return "Pickup_Weapon_Demo not found";

var copy = Object.Instantiate(demo, demo.transform.parent);
copy.name = "Pickup_MP7";
copy.transform.position = demo.transform.position + demo.transform.right * 1.5f;

var pickup = copy.GetComponent<CrimsonDraft.Navigation.Interactables.PickupInteractable>();
var so = new UnityEditor.SerializedObject(pickup);
so.FindProperty("pickupId").stringValue = "pickup_mp7";
so.FindProperty("item").objectReferenceValue =
    UnityEditor.AssetDatabase.LoadAssetAtPath<CrimsonDraft.Inventory.WeaponData>("Assets/Data/Inventory/Weapons/MP7.asset");
so.ApplyModifiedProperties();

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
if (!wasLoaded) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
return "Pickup_MP7 placed at " + copy.transform.position;
```

- [ ] **Step 4: Manual verification in Play Mode**

Temporarily (do not commit this) set `Encounter Data = Encounter_Armored` and `Encounter Id = armored_test` on one `EnemyNavAgent` in `Navigation.unity`, enter Play Mode, and check:
1. Without vest art: the armored enemy fights like a Grunt, no overlay visible, no console errors (Review Focus 4).
2. A multi-shot burst shows the total popup above the silhouette; it disappears on dismiss and is not present at the start of the next QTE (Review Focus 5).
3. `Pickup_MP7` can be collected and equipped.

Afterwards, put the two `EnemyNavAgent` fields back to their original values in the Inspector and save. Do not use `git checkout` on `Navigation.unity` — that would also discard `Pickup_MP7` from Step 3. Confirm with `git diff` that the only `Navigation.unity` change is the new `Pickup_MP7` object.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Data Game/CrimsonDraft/Assets/Scenes/Production/Navigation.unity
git status --short   # confirm only the intended assets/scene are staged
git commit -m "feat(content): MP7 pickup, vest overlay, armored enemy and encounter"
```

---

### Task 8: GDD update and final verification

**Files:**
- Modify: `Design/GDD/crimson-draft-gdd.md`

- [ ] **Step 1: Rewrite the damage tables (§5, "Valores de daño tentativos")**

Replace from `**Valores de daño tentativos (por disparo, contra zona sin blindaje):**` through the `| Five-Seven | 9 | 19 |` row (end of the "Daño contra zonas blindadas" table) with:

```markdown
**Valores de daño tentativos (por disparo):**

La armadura depende del **arma**, no de la munición: cada arma tiene un multiplicador de daño contra chaleco (1.0 = lo atraviesa por completo). Un impacto bloqueado por el chaleco conserva todo su daño a Poise.

| Arma | Calibre | Operador | Daño | Multiplicador vs. chaleco |
|---|---|---|---|---|
| Mk18 | 5.56 | Ethan (primaria) | 32 | 0.25 |
| MCX Rattler | 5.56 (cañón corto) | Lilou (primaria) | 28 | 0.25 |
| Benelli M4 | 12ga (postas) | Marcus (primaria) | 45 (multi-perdigón, alto a corta distancia) | 0.25 (por perdigón) |
| MP7 | 4.6×30 | Darius (primaria) | 16 por impacto, alta cadencia | **1.0** — única arma que atraviesa el chaleco por completo |
| P229 | 9mm | Ethan (secundaria) | 18 | 0.25 |
| P226 | 9mm | Lilou / Marcus (secundaria) | 18 | 0.25 |
| Five-Seven | 5.7×28 | Darius (secundaria) | 15 | 0.25 (candidata a un valor mayor por la capacidad de penetración real del 5.7×28) |
| Cuchillo / Hacha | — | melee | según arma | 0.25 |
```

- [ ] **Step 2: Remove the Rip Poise bonus**

Delete the line starting with `  - Las balas **Rip** también aplican un multiplicador oculto de daño a Poise` in the Poise section.

- [ ] **Step 3: Rewrite §5.r**

Replace from `### 5.r Tipos de bala y zonas de armadura` through its `[TODO: definir la tabla completa de multiplicadores ...]` line with:

```markdown
### 5.r Capas de silueta: chaleco y puntos débiles

Para profundizar el QTE como diferenciador del juego (más allá de "apuntar a la cabeza"), ciertos Wanderers llevan una **capa** dibujada sobre su silueta. La silueta base sigue decidiendo qué zona se impactó (cabeza, torso, brazos, piernas, roce); la capa solo agrega un modificador encima.

- **Chaleco (implementado):** un tipo de Wanderer lleva un chaleco visible en su silueta, en un color propio que lo identifica como armadura. Un impacto sobre el chaleco multiplica el daño por el multiplicador del arma (ver tabla de daño): la MP7 lo atraviesa al 100%, el resto pierde la mayor parte del daño. El Poise no se reduce — el chaleco frena la bala, no el impacto —, así que las demás armas siguen sirviendo para derribarlo. El popup de daño de un impacto bloqueado aparece en un color destacado.
- **Puntos débiles / ampollas (próximo):** ciertos enemigos presentarán ampollas visibles en zonas distintas a la cabeza que multiplican el daño (tentativo: ×1.5 a ×2), elegidas al azar de un pool de variantes cada vez que aparece el enemigo. Esto evita que la estrategia óptima sea siempre "apuntar a la cabeza".
- **Una capa por enemigo:** cada enemigo elige como máximo una capa de su pool al aparecer — nunca lleva chaleco y ampollas a la vez.
- **Total de la ráfaga:** al terminar el QTE, un popup muestra el daño total de todas las balas.

`[TODO: ajustar los multiplicadores vs. chaleco con playtesting; definir las variantes de ampollas y su multiplicador exacto.]`
```

- [ ] **Step 4: Version header and changelog**

Change `*Versión 0.17 — 6 de octubre de 2026 — Estado: borrador inicial*` to `*Versión 0.18 — 6 de octubre de 2026 — Estado: borrador inicial*`, and add as the first changelog entry under `## 12. Changelog`:

```markdown
- **v0.18 — 06/10/2026:** Reemplazado el sistema de munición Rip/Armor Piercing por armadura por arma (multiplicador vs. chaleco; la MP7 lo atraviesa al 100%). §5.r reescrita como sistema de capas de silueta (chaleco implementado, ampollas a futuro, una capa por enemigo) y popup de daño total al final del QTE.
```

- [ ] **Step 5: Final full verification**

Compile (zero `CS` errors in touched files), then `run_tests` EditMode with no filter. Expected: all tests pass except the 5 known `ItemSocketInteractableTests.*`. Then `grep -n "Rip\|Armor Piercing" Design/GDD/crimson-draft-gdd.md` — only changelog history entries (v0.10, v0.12, v0.13) and this v0.18 entry may still mention them.

- [ ] **Step 6: Commit**

```bash
git add Design/GDD/crimson-draft-gdd.md
git commit -m "docs(gdd): per-weapon armor and silhouette overlays replace Rip/AP ammo"
```
