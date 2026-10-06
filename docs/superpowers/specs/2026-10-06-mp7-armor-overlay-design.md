# MP7 & Armored Silhouette Overlays — Design Spec

**Date:** 2026-10-06
**Status:** Draft — awaiting review
**Branch:** `feature/mp7-armor` (from `feature/remove-focus-fire`)
**Scope:** Combat — a new enemy type wearing a ballistic vest that only the MP7 fully penetrates, built on a reusable *silhouette overlay* layer system (weak-point blisters will be the next overlay kind, in their own spec). Also adds a total-damage popup at the end of every multi-shot QTE.

---

## Overview

Today an aim QTE resolves every bullet by sampling one pixel of the enemy's color-coded zone mask (`AimHitMaskProfile.ZoneMaskSprite`): the color picks the anatomical zone (head/torso/arms/legs) and precision (normal/graze), and `CombatMenuController.ComputeShotDamage` turns that into damage. The player sees a separate black & white `SilhouetteSprite`.

This spec adds a second, independent layer on top of that silhouette: an **overlay**. An overlay has its own visible sprite (drawn over the silhouette) and its own coverage mask (sampled at the same point as the zone mask). The zone mask still decides *where* a bullet hit; the overlay only adds a *modifier* on top. The first overlay kind is **Armor**: a vest. Weapons carry a per-weapon `armorDamageMultiplier`; a bullet that lands on vest coverage is multiplied by it. The MP7's multiplier is 1.0, so it is the one weapon that hits the vest at full damage.

### Decisions (from brainstorming)

| # | Decision |
|---|----------|
| 1 | Armor penetration is a **per-weapon** property. The GDD's Rip / Armor Piercing ammo system is dropped. |
| 2 | Armor is a **new enemy type** with its own vest art. Grunt and Heavy are unchanged. |
| 3 | Each weapon (firearm **and** melee) has its **own** `armorDamageMultiplier`. MP7 = 1.0. |
| 4 | A blocked hit still deals **full Poise damage** — the vest stops the bullet, not the impact. |
| 5 | The MP7 is obtained as a **world pickup** (registered in `ItemDatabase`). |
| 6 | A blocked hit's damage popup uses a **distinct, highlighted color**. |
| 7 | At the end of the QTE a **total-damage popup** shows the sum of every bullet. |
| 8 | The vest lives in a **separate overlay layer** (approach C), not as a flag/zone in the base mask, so future blisters reuse the same mechanism. |
| 9 | Overlays are picked from a per-enemy **pool**; an enemy carries **at most one** overlay, so it can never have a vest and blisters at once. |
| 10 | Blisters are **out of scope** — the system is built ready for them; they get their own spec. |

---

## 1. Data

### `SilhouetteOverlay` (new ScriptableObject)

`Combat/Data/SilhouetteOverlay.cs` — `[CreateAssetMenu(menuName = "CrimsonDraft/Combat/Silhouette Overlay")]`

| Field | Type | Purpose |
|-------|------|---------|
| `kind` | `OverlayKind` | `enum OverlayKind { Armor = 0 }`. `WeakPoint` is added by the blisters spec. Values are pinned explicitly (serialized). |
| `visibleSprite` | `Sprite` | What the player sees: the vest painted in its armor color, on a canvas identical in size and pivot to the base silhouette sprite, transparent elsewhere. |
| `maskSprite` | `Sprite` | Coverage mask, never shown. Any pixel with alpha ≥ 0.5 counts as *covered*. No per-zone colors — zone/precision still come from the base zone mask. |
| `staggeredVisibleSprite` | `Sprite?` | Same, for the knocked-down pose (`AimHitMaskProfile` already swaps to a staggered profile). |
| `staggeredMaskSprite` | `Sprite?` | Same. |

```csharp
public readonly struct ActiveOverlay
{
    public OverlayKind Kind          { get; }
    public Sprite      VisibleSprite { get; }
    public Sprite      MaskSprite    { get; }
}

// On SilhouetteOverlay:
public ActiveOverlay? Resolve(bool staggered);
```

`Resolve(false)` returns the standing pair; `Resolve(true)` returns the staggered pair, or `null` if either staggered sprite is missing (the overlay is then neither shown nor blocking while the enemy is down). `Resolve(false)` also returns `null` if either standing sprite is missing, so an overlay asset whose art hasn't been assigned yet is harmless.

### `EnemyData`

Adds `[SerializeField] private SilhouetteOverlay[] overlayPool = Array.Empty<SilhouetteOverlay>();` and `OverlayPool` getter.

### `OverlayPicker` (new, pure)

```csharp
public static class OverlayPicker
{
    public static SilhouetteOverlay? Pick(IReadOnlyList<SilhouetteOverlay?> pool, IRandomSource random);
}
```

Empty pool (or all-null entries) → `null`. Otherwise exactly one non-null entry, chosen uniformly via `random.NextInt`. Picking exactly one is what enforces "never vest and blisters at once" — no extra validation needed.

### Weapons

`WeaponData` and `MeleeWeaponData` each add:

```csharp
[SerializeField, Range(0f, 1f)] private float armorDamageMultiplier = 0.25f;
public float ArmorDamageMultiplier => this.armorDamageMultiplier;
```

| Asset | `armorDamageMultiplier` |
|-------|-------------------------|
| MP7 | **1.0** |
| P226, P229, Mk18, Benelli M4, Knife, Hatchet | 0.25 (placeholder, tune in Inspector) |

---

## 2. Shot resolution

### Rolling and exposing the overlay

`BattlefieldView.Populate` already rolls each enemy's max HP with `enemyStatRandom` (`RollMaxHp`). Right next to it, it rolls the overlay with `OverlayPicker.Pick(enemy.OverlayPool, this.enemyStatRandom)` and stores it in that slot's `EnemyRuntimeState`.

`IBattlefieldView` adds:

```csharp
ActiveOverlay? GetEnemyOverlay(int slotIndex);
```

Mirrors `GetEnemyHitMaskProfile`: returns `rolledOverlay?.Resolve(IsEnemyStaggered(slotIndex))`, `null` for empty/invalid slots.

### Configuring the aim view

`IAimView` adds `void ConfigureOverlay(ActiveOverlay? overlay);`.

`TargetSelectionState` calls it right after `ConfigureHitMask(...)` with `battlefieldView.GetEnemyOverlay(CurrentTargetSlot)` (or `null` with no target). `ShotCountSelectionState`'s no-enemy path calls `ConfigureOverlay(null)` next to its existing `ConfigureHitMask(null)`.

`AimViewController`:
- New `[SerializeField] private Image? overlayImage;` — a child of the silhouette with the same rect, so it shakes with the heartbeat. `ConfigureOverlay` sets its sprite and enables it, or disables it for `null`.
- Stores the active overlay mask sprite and kind.
- `ConfigureWeapon` / `ConfigureMeleeWeapon` also store `activeArmorDamageMultiplier` (`weaponData?.ArmorDamageMultiplier ?? 1f`).

### Per-pellet resolution (`AimViewController.BuildResolvedShots`)

1. Sample the base zone mask exactly as today → zone + precision. If the zone is `Miss`, stop: an overlay pixel outside the body never counts.
2. Sample the overlay mask at the same normalized point (same UV math as `SampleSilhouette`) → `covered`.
3. `bool armorBlocked = ArmorRules.IsBlocked(zone, covered, overlayKind, activeArmorDamageMultiplier);`
4. `damage = CombatMenuController.ComputeShotDamage(zone, precMult, activeBaseDamage, armorBlocked ? activeArmorDamageMultiplier : 1f);`
5. `new ResolvedShot(..., damage, armorBlocked)`.

```csharp
public static class ArmorRules
{
    // Blocked only when the shot hit the body, landed on Armor coverage, and the weapon
    // doesn't fully penetrate (multiplier < 1). The MP7 (1.0) is therefore never blocked.
    public static bool IsBlocked(ShotZone zone, bool covered, OverlayKind? kind, float armorDamageMultiplier);
}
```

`ComputeShotDamage(ShotZone zone, float precisionMultiplier, int baseDamage = BaseDamage, float armorMultiplier = 1f)` — the armor factor joins the existing product; rounding still happens once, at the end.

`ResolvedShot` adds `public bool ArmorBlocked { get; }` (constructor parameter defaults to `false` so existing call sites and tests keep compiling).

### What does not change

- **Poise:** `ComputePoiseDamage` is zone-based and ignores armor → full Poise on a blocked hit (decision 4).
- **Headshot / decapitation counting:** zone-based; the vest doesn't cover the head.
- **Shotgun:** each pellet is resolved independently — some may hit the vest, others not.
- **Melee:** same pipeline (`SlashStrategy` points), using the melee weapon's own multiplier.

---

## 3. Feedback

### Blocked-hit popup

`AimViewController` adds `[SerializeField] private Color armorBlockedFeedbackColor = new Color(0.2f, 0.88f, 1f, 1f); // #33E0FF`. `SpawnShotFeedbackVisual` takes the shot's `ArmorBlocked` and uses that color instead of `hitFeedbackColor`. Text is unchanged (`-{damage}`). An MP7 hit on the vest is not blocked, so it shows in the normal hit color.

### Total-damage popup

At the end of `ResolvePendingShotsAsync` — after the last bullet's popup, before `OnShotsResolved` — the aim view spawns a total popup:

```csharp
public static class TotalFeedback
{
    // null when there's nothing worth totaling (0 or 1 resolved shots).
    public static string? Format(IReadOnlyList<ResolvedShot> shots);
}
```

| Shots | Result |
|-------|--------|
| 0 or 1 | `null` → no total popup (it would just repeat the single number) |
| ≥ 2, every one `Miss` | `"MISS"`, in `missFeedbackColor` |
| ≥ 2, at least one hit | `"-{sum of Damage}"`, in `hitFeedbackColor` |

- Spawned from the existing `feedbackTextPrefab`, scaled by new `[SerializeField] float totalFeedbackScale = 1.6f`, parented to new `[SerializeField] RectTransform? totalFeedbackAnchor` (positioned above the silhouette in the prefab).
- **Does not fade**: stays until the player dismisses the QTE, then is destroyed in `Hide()`. Not tracked in `activeFeedback`, so it never counts against `maxConcurrentFeedback` and is never detached to linger on the battlefield.
- If `totalFeedbackAnchor` is unassigned, the total is skipped with a single editor warning (same pattern as the missing feedback prefab).

### QTE scene objects

The QTE's `AimViewController` lives directly in `Scenes/Production/Combat.unity` and `Scenes/Test/Combat_Decor.unity` (not in a prefab). Two new children of its silhouette image, wired to the new fields: `OverlayImage` (stretched to the silhouette rect, raycast target off) and `TotalFeedbackAnchor`. Created via the editor during implementation.

---

## 4. Content

| Asset | Change |
|-------|--------|
| `Data/Inventory/Weapons/MP7.asset` | `armorDamageMultiplier = 1`; fill missing `damage = 16` (GDD), `poiseDamage = 10`, `maxShotCount = 6` (starting points). Keeps `GunType.REPistols` and the P229 burst pattern — its own pattern is balance work. |
| `Data/ItemDatabase.asset` | Register MP7. |
| `Scenes/Production/Navigation.unity` | `Pickup_MP7`, a duplicate of `Pickup_Weapon_Demo` (`PickupInteractable`) with `item = MP7`, placed next to it. |
| `Data/Enemies/Overlay_Vest.asset` | New `SilhouetteOverlay`, `kind = Armor`, all four sprites empty until the art exists. |
| `Data/Enemies/Enemy_Armored.asset` | New `EnemyData`, `enemyId = armored`, Grunt's stats, base hit-mask profiles and battlefield prefab; `overlayPool = [Overlay_Vest]`. |
| `Data/Enemies/Encounter_Armored.asset` | One `Enemy_Armored`; registered in `EncounterDatabase`. No existing navigation enemy is reassigned. |
| Other weapon/melee assets | Serialize the 0.25 default. |

With `Overlay_Vest` unassigned, `Enemy_Armored` plays exactly like a Grunt (no overlay, no errors).

### GDD

Rewrite §5.r: armor is per weapon; overlays (vest now, blisters future) with the one-overlay-per-enemy rule and pool selection. Damage tables move from "Rip / Armor Piercing" columns to "Damage / Multiplier vs. vest"; remove the Rip Poise bonus line in the Poise section. Changelog entry v0.18.

---

## 5. Art & model hand-off (for the artist)

### Vest silhouette sprites

The current silhouette is `Assets/Art/Sprites/UI/QTE.png`: a **192×256** sheet, Sprite Mode *Multiple*, four **96×128** sprites, pivot center (48, 64), 32 PPU, **Read/Write enabled**, Filter **Point**, Compression **None**:

| Sheet cell | Sprite | Role |
|------------|--------|------|
| bottom-left | `QTE_2` | zone mask (standing) |
| bottom-right | `QTE_3` | visible silhouette (standing) |
| top-left | `QTE_1` | zone mask (staggered) |
| top-right | `QTE_0` | visible silhouette (staggered) |

Create **`Assets/Art/Sprites/UI/QTE_Vest.png`** with the *same* sheet layout and import settings (Read/Write is mandatory — the mask is sampled with `GetPixel`):

| Sheet cell | Assign to `Overlay_Vest` field | Content |
|------------|-------------------------------|---------|
| bottom-left | `maskSprite` | vest shape in solid opaque color, transparent elsewhere (the "back") |
| bottom-right | `visibleSprite` | vest as the player sees it, in its armor color (the "front") |
| top-left | `staggeredMaskSprite` | same, knocked-down pose (optional) |
| top-right | `staggeredVisibleSprite` | same, knocked-down pose (optional) |

Each cell must line up pixel-for-pixel with the matching `QTE.png` cell — the overlay is drawn and sampled in the silhouette's own rect.

### 3D vest model

`Enemy_Armored` initially reuses the Grunt's battlefield prefab, `Assets/Prefabs/Enemies/EnemyCombatModel.prefab`. To give it a vest without touching the Grunt: create a **Prefab Variant** of it (`Assets/Prefabs/Enemies/EnemyCombatModel_Armored.prefab`), add the vest mesh as a child of the chest bone in the variant (so it follows the skinned animation), and assign the variant to `Enemy_Armored.battlefieldPrefab`.

### Testing in game

On any `EnemyNavAgent` in a navigation scene, set `Encounter Data` to `Encounter_Armored` and `Encounter Id` to a unique id (e.g. `armored_test` — it is published with `CombatStartedEvent`). Pick up the MP7, equip it, and compare its vest hits against another operator's weapon.

---

## 6. Testing

EditMode, plain C# fakes (project convention). The rules live in pure static helpers so they're testable without sprites:

| Test target | Cases |
|-------------|-------|
| `ComputeShotDamage` | armor multiplier reduces torso damage and rounds once; multiplier 1 leaves it unchanged; `Miss` stays 0 |
| `ArmorRules.IsBlocked` | `Miss` never blocks; uncovered never blocks; no overlay never blocks; multiplier 1.0 (MP7) never blocks; covered + Armor + < 1 blocks |
| `OverlayPicker.Pick` | empty → null; all-null → null; single → that one; fake random index → expected entry |
| `SilhouetteOverlay.Resolve` | standing → standing pair; staggered with sprites → staggered pair; staggered without sprites → null; standing without sprites → null |
| `TotalFeedback.Format` | 0/1 shots → null; ≥2 all miss → `"MISS"`; ≥2 mixed → `"-{sum}"` |

Full EditMode suite must still show only the 5 known pre-existing `ItemSocketInteractableTests` failures.

---

## Out of scope

- Weak-point blisters (`OverlayKind.WeakPoint`, blister pools) — next spec.
- Any ammo-type system.
- MP7-specific burst pattern / `GunType`, final balance numbers.
- Navigation-side changes for the armored enemy (patrol placement, nav data).
- A metallic impact sound for blocked hits.
