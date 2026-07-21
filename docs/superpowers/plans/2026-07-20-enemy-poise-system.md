# Enemy Poise System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every enemy a hidden Poise counter that drains per weapon hit and, once exhausted while the enemy's HP is low enough, knocks it down and interrupts its turn in the ATB queue.

**Architecture:** Poise state lives alongside HP in `BattlefieldView.EnemyRuntimeState` (the existing enemy-runtime owner). Per-shot Poise damage is computed in `AimingState` next to the existing per-shot HP damage sum, using a new pure helper on `CombatMenuController` (same home as the existing `ComputeShotDamage`). The stagger *decision* is a second pure helper on `CombatMenuController`, consumed by `BattlefieldView`. `CombatOrchestrator` remains the only class that touches `ATBSystem`, so the stagger's ATB consequences (gauge reset, skipping queued/ready attacks) are wired there, driven by a new `IBattlefieldView.IsEnemyStaggered(slotIndex)` query and a new `ICombatOrchestrator.NotifyEnemyStaggered(slot)` notification.

**Tech Stack:** Unity C#, VContainer (DI), NUnit EditMode tests with hand-written fakes (no mocking framework).

## Global Constraints

- `#nullable enable` at the top of every new/touched file (already present in all files this plan touches).
- Serialized Unity fields default via `[SerializeField]`, never `null!` unless already established in the file (this plan only adds value-typed and asset-reference fields, matching existing patterns in `EnemyData`/`WeaponData`).
- `[Inject]`-attributed `Construct(...)` stays the only injection point on MonoBehaviours — this plan does not add any new injected dependency, so no `Construct` signatures change.
- Tests use plain C# fakes already defined in `CombatMenuControllerTests.cs` (`FakeBattlefieldView`, `FakeOrchestrator`, `FakeOperatorRoster.FakeWeaponSlot`) — extend them, don't introduce a mocking library.
- Default balance values (from the approved design spec, `docs/superpowers/specs/2026-07-20-enemy-poise-design.md`): `minPoise = 15`, `maxPoise = 30`, `staggerHpThresholdPct = 40`, `staggerDurationSec = 2.5`, weapon `poiseDamage = 10`, legs multiplier `x2` (fixed constant, not a data field).
- No `Co-Authored-By` trailers in commit messages (project convention, `CLAUDE.md`).
- Tests run via Unity Test Runner / UnityMCP `run_tests` — there is no CLI test command in this project.

---

## Task 1: Weapon Poise data + `ComputePoiseDamage` helper

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponData.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Operators/IWeaponSlot.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponItem.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Interfaces:**
- Produces: `WeaponData.PoiseDamage` (int), `IWeaponSlot.PoiseDamage` (int), `WeaponItem.PoiseDamage` (int, delegates to `Data.PoiseDamage`), `CombatMenuController.ComputePoiseDamage(ShotZone zone, int weaponPoiseDamage) : int` (internal static).

- [ ] **Step 1: Write the failing tests for `ComputePoiseDamage`**

Add these tests in `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`, directly after the existing `ComputeShotDamage_miss_returns0` test (currently ending around line 467):

```csharp
        [Test]
        public void ComputePoiseDamage_torso_returnsWeaponValueUnchanged()
        {
            Assert.AreEqual(10, CombatMenuController.ComputePoiseDamage(ShotZone.Torso, 10));
        }

        [Test]
        public void ComputePoiseDamage_head_returnsWeaponValueUnchanged()
        {
            Assert.AreEqual(10, CombatMenuController.ComputePoiseDamage(ShotZone.Head, 10));
        }

        [Test]
        public void ComputePoiseDamage_legs_doublesWeaponValue()
        {
            Assert.AreEqual(20, CombatMenuController.ComputePoiseDamage(ShotZone.Legs, 10));
        }

        [Test]
        public void ComputePoiseDamage_zeroWeaponPoise_returnsZeroEvenOnLegs()
        {
            Assert.AreEqual(0, CombatMenuController.ComputePoiseDamage(ShotZone.Legs, 0));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run the `CombatMenuControllerTests` suite via Unity Test Runner (Window → General → Test Runner → EditMode), filtered to `ComputePoiseDamage`.
Expected: FAIL to compile — `ComputePoiseDamage` does not exist on `CombatMenuController` yet.

- [ ] **Step 3: Add `poiseDamage` to `WeaponData`**

In `Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponData.cs`, add the field next to the existing `damage` field:

```csharp
        [SerializeField, Min(1)] private int       damage                 = 20;
        [SerializeField, Min(0)] private int       poiseDamage            = 10;
```

and the accessor next to `Damage`:

```csharp
        public int               Damage                 => this.damage;
        public int               PoiseDamage            => this.poiseDamage;
```

- [ ] **Step 4: Add `PoiseDamage` to `IWeaponSlot`**

In `Game/CrimsonDraft/Assets/Scripts/Operators/IWeaponSlot.cs`, the interface becomes:

```csharp
#nullable enable

namespace CrimsonDraft.Operators
{
    public interface IWeaponSlot
    {
        Caliber Caliber    { get; }
        GunType GunType    { get; }
        int     BaseDamage { get; }
        int     CurrentAmmo { get; }
        int     MaxAmmo     { get; }
        int     PoiseDamage { get; }
        void    SetAmmo(int value);
    }
}
```

- [ ] **Step 5: Implement `PoiseDamage` on `WeaponItem`**

In `Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponItem.cs`, add next to `BaseDamage`:

```csharp
        public int     BaseDamage    => this.Data.Damage;
        public int     PoiseDamage   => this.Data.PoiseDamage;
```

- [ ] **Step 6: Fix the now-broken `FakeWeaponSlot` test fake**

`IWeaponSlot` gained a member, so `FakeOperatorRoster.FakeWeaponSlot` (inside `CombatMenuControllerTests.cs`, currently around line 823) no longer compiles. Update it:

```csharp
            private sealed class FakeWeaponSlot : IWeaponSlot
            {
                public Caliber Caliber    => Caliber._9mm;
                public GunType GunType    => GunType.Pistols;
                public int     BaseDamage => 20;
                public int     CurrentAmmo { get; private set; }
                public int     MaxAmmo { get; }
                public int     PoiseDamage { get; }

                internal FakeWeaponSlot(int maxAmmo, int poiseDamage = 10)
                {
                    this.MaxAmmo = Mathf.Max(1, maxAmmo);
                    this.CurrentAmmo = this.MaxAmmo;
                    this.PoiseDamage = poiseDamage;
                }

                public void SetAmmo(int value) =>
                    this.CurrentAmmo = Mathf.Clamp(value, 0, this.MaxAmmo);
            }
```

The existing call site `this.slots[i].SetEquippedWeapon(new FakeWeaponSlot(maxAmmo));` (in `FakeOperatorRoster`'s constructor) keeps compiling unchanged — `poiseDamage` defaults to `10`.

- [ ] **Step 7: Add `ComputePoiseDamage` to `CombatMenuController`**

In `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs`, add this right after `ComputeShotDamage` (currently ending at line 219, inside the `#region Internal API (testable)` block):

```csharp
        internal static int ComputePoiseDamage(ShotZone zone, int weaponPoiseDamage) =>
            zone == ShotZone.Legs ? weaponPoiseDamage * 2 : weaponPoiseDamage;
```

- [ ] **Step 8: Run tests to verify they pass**

Run the same `ComputePoiseDamage` filter in the Test Runner.
Expected: all 4 new tests PASS, and no existing test in the suite regressed (the full `CombatMenuControllerTests` class still compiles and passes).

- [ ] **Step 9: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponData.cs \
        Game/CrimsonDraft/Assets/Scripts/Operators/IWeaponSlot.cs \
        Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponItem.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs \
        Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat): add weapon PoiseDamage and ComputePoiseDamage helper"
```

---

## Task 2: Enemy Poise data + `BattlefieldView` stagger state

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/EnemyData.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatDebugView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/Commands/ShootCommand.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Interfaces:**
- Consumes: `CombatMenuController.ComputePoiseDamage` (Task 1, not used here but same class/region).
- Produces: `EnemyData.MinPoise/MaxPoise/StaggerHpThresholdPct/StaggerDurationSec` (properties), `CombatMenuController.ShouldStagger(int poiseAfterDamage, int currentHp, int maxHp, float staggerHpThresholdPct) : bool` (internal static), `IBattlefieldView.ApplyDamageToEnemy(int slotIndex, int hpDamage, int poiseDamage) : EnemyDamageResult` (signature change), `EnemyDamageResult.IsStaggered` (bool), `IBattlefieldView.IsEnemyStaggered(int slotIndex) : bool`.

- [ ] **Step 1: Write the failing tests for `ShouldStagger`**

Add these tests in `CombatMenuControllerTests.cs`, right after the `ComputePoiseDamage` tests added in Task 1:

```csharp
        [Test]
        public void ShouldStagger_positivePoise_returnsFalseRegardlessOfHp()
        {
            Assert.IsFalse(CombatMenuController.ShouldStagger(poiseAfterDamage: 5, currentHp: 1, maxHp: 100, staggerHpThresholdPct: 40f));
        }

        [Test]
        public void ShouldStagger_zeroPoise_hpAboveThreshold_returnsFalse()
        {
            Assert.IsFalse(CombatMenuController.ShouldStagger(poiseAfterDamage: 0, currentHp: 50, maxHp: 100, staggerHpThresholdPct: 40f));
        }

        [Test]
        public void ShouldStagger_zeroPoise_hpBelowThreshold_returnsTrue()
        {
            Assert.IsTrue(CombatMenuController.ShouldStagger(poiseAfterDamage: 0, currentHp: 30, maxHp: 100, staggerHpThresholdPct: 40f));
        }

        [Test]
        public void ShouldStagger_negativePoise_hpBelowThreshold_returnsTrue()
        {
            Assert.IsTrue(CombatMenuController.ShouldStagger(poiseAfterDamage: -8, currentHp: 30, maxHp: 100, staggerHpThresholdPct: 40f));
        }

        [Test]
        public void ShouldStagger_hpExactlyAtThreshold_returnsFalse()
        {
            Assert.IsFalse(CombatMenuController.ShouldStagger(poiseAfterDamage: 0, currentHp: 40, maxHp: 100, staggerHpThresholdPct: 40f));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL to compile — `ShouldStagger` does not exist yet.

- [ ] **Step 3: Add Poise config fields to `EnemyData`**

In `Game/CrimsonDraft/Assets/Scripts/Combat/Data/EnemyData.cs`, add after the existing `initialGaugePct` field:

```csharp
        [SerializeField, Range(0f, 100f)] private float initialGaugePct = 0f;
        [SerializeField, Min(0)] private int   minPoise               = 15;
        [SerializeField, Min(0)] private int   maxPoise               = 30;
        [SerializeField, Range(0f, 100f)] private float staggerHpThresholdPct = 40f;
        [SerializeField, Min(0f)] private float staggerDurationSec    = 2.5f;
```

and the accessors after `InitialGaugePct`:

```csharp
        public float InitialGaugePct             => this.initialGaugePct;
        public int   MinPoise                     => this.minPoise;
        public int   MaxPoise                     => this.maxPoise;
        public float StaggerHpThresholdPct         => this.staggerHpThresholdPct;
        public float StaggerDurationSec            => this.staggerDurationSec;
```

Existing `EnemyData` assets (e.g. `Enemy_Heavy.asset`) don't need manual edits — Unity serializes the new fields with these defaults the next time the asset is saved. No asset file changes are required for this task to compile and run.

- [ ] **Step 4: Add `ShouldStagger` to `CombatMenuController`**

Right after `ComputePoiseDamage` (added in Task 1):

```csharp
        internal static bool ShouldStagger(int poiseAfterDamage, int currentHp, int maxHp, float staggerHpThresholdPct)
        {
            if (poiseAfterDamage > 0) return false;
            float hpPct = maxHp > 0 ? (float)currentHp / maxHp * 100f : 0f;
            return hpPct < staggerHpThresholdPct;
        }
```

- [ ] **Step 5: Run tests to verify they pass**

Expected: all 5 `ShouldStagger` tests PASS.

- [ ] **Step 6: Extend `EnemyDamageResult` with `IsStaggered`**

In `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs`, replace the struct:

```csharp
    public readonly struct EnemyDamageResult
    {
        public int SlotIndex      { get; }
        public int DamageApplied  { get; }
        public int RemainingHp    { get; }
        public bool IsDead        { get; }
        public bool IsStaggered   { get; }

        public EnemyDamageResult(int slotIndex, int damageApplied, int remainingHp, bool isDead, bool isStaggered)
        {
            this.SlotIndex     = slotIndex;
            this.DamageApplied = damageApplied;
            this.RemainingHp   = remainingHp;
            this.IsDead        = isDead;
            this.IsStaggered   = isStaggered;
        }
    }
```

and change the interface method + add the new query method + extend the debug tuple:

```csharp
    public interface IBattlefieldView
    {
        void Populate(EncounterData encounter);
        void SetOperatorIndicator(int slotIndex);
        void DimOperatorIndicator();
        void PlayEnemyAttackFeedback(int enemySlotIndex);
        void ShowOperatorDamage(int operatorSlotIndex, int damage);
        void SetEnemyTargetIndicator(int slotIndex);
        void HideEnemyTargetIndicator();
        int[] GetOccupiedEnemySlots();
        AimHitMaskProfile? GetEnemyHitMaskProfile(int slotIndex);
        EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int hpDamage, int poiseDamage);
        bool IsEnemyStaggered(int slotIndex);
        bool HasAliveEnemies();
        UniTask PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots);
#if UNITY_EDITOR || DEBUG_COMBAT
        (int Current, int Max, bool IsDead, int Poise, bool IsStaggered) GetEnemyHpDebug(int slotIndex);
#endif
    }
```

- [ ] **Step 7: Add Poise state to `EnemyRuntimeState` and roll it in `Populate()`**

In `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs`, extend the private state class:

```csharp
        private sealed class EnemyRuntimeState
        {
            public int CurrentHp;
            public int MaxHp;
            public bool IsDead;
            public int CurrentPoise;
            public int InitialPoise; // the roll this enemy resets to on a silent Poise reset
            public bool IsStaggered;
            public float StaggerEndsAt;
        }
```

Add a locally-owned random source next to the other per-slot dictionaries (same pattern `CombatOrchestrator` already uses — a private field, not DI-injected):

```csharp
        private readonly IRandomSource poiseRandom = new UnityRandomSource();
```

And a hash for the new Animator bool, next to `Hit1Hash`/`Hit2Hash`:

```csharp
        private static readonly int IsStaggeredHash = Animator.StringToHash("IsStaggered");
```

In `Populate()`, the enemy-state construction currently reads:

```csharp
                this.enemyStateBySlot[i] = new EnemyRuntimeState
                {
                    CurrentHp = Mathf.Max(1, enemy.MaxHp),
                    MaxHp = Mathf.Max(1, enemy.MaxHp),
                    IsDead = false
                };
```

Replace it with:

```csharp
                int rolledPoise = this.poiseRandom.NextInt(enemy.MinPoise, enemy.MaxPoise + 1);
                this.enemyStateBySlot[i] = new EnemyRuntimeState
                {
                    CurrentHp     = Mathf.Max(1, enemy.MaxHp),
                    MaxHp         = Mathf.Max(1, enemy.MaxHp),
                    IsDead        = false,
                    CurrentPoise  = rolledPoise,
                    InitialPoise  = rolledPoise,
                    IsStaggered   = false,
                    StaggerEndsAt = 0f
                };
```

(`NextInt(minInclusive, maxExclusive)` is the existing `IRandomSource` contract — `enemy.MaxPoise + 1` makes the roll inclusive of `MaxPoise`, matching the design spec's `[minPoise, maxPoise]` range.)

- [ ] **Step 8: Rewrite `ApplyDamageToEnemy` to drain and check Poise**

Replace the whole method:

```csharp
        public EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int hpDamage, int poiseDamage)
        {
            if (!this.enemyStateBySlot.TryGetValue(slotIndex, out var state))
                return new EnemyDamageResult(slotIndex, 0, 0, false, false);

            if (state.IsDead)
                return new EnemyDamageResult(slotIndex, 0, 0, true, false);

            int appliedDamage = Mathf.Max(0, hpDamage);
            state.CurrentHp = Mathf.Max(0, state.CurrentHp - appliedDamage);
            bool isDead = state.CurrentHp <= 0;
            if (isDead)
            {
                state.IsDead = true;
                if (this.enemyGoBySlot.TryGetValue(slotIndex, out var go) && go != null)
                    StartCoroutine(this.FadeOutAndHideEnemy(go));

                var nextOccupied = new List<int>(this.occupiedEnemySlots.Length);
                foreach (int slot in this.occupiedEnemySlots)
                {
                    if (slot != slotIndex)
                        nextOccupied.Add(slot);
                }
                this.occupiedEnemySlots = nextOccupied.ToArray();

                return new EnemyDamageResult(slotIndex, appliedDamage, 0, true, false);
            }

            bool justStaggered = false;
            // Poise doesn't drain further while the enemy is already down — it only
            // matters again once it recovers (Update() below clears IsStaggered).
            if (!state.IsStaggered)
            {
                state.CurrentPoise -= Mathf.Max(0, poiseDamage);

                EnemyData? enemyData = slotIndex >= 0 && slotIndex < this.currentEnemySlots.Length
                    ? this.currentEnemySlots[slotIndex]
                    : null;

                if (enemyData != null && state.CurrentPoise <= 0)
                {
                    if (CombatMenuController.ShouldStagger(state.CurrentPoise, state.CurrentHp, state.MaxHp, enemyData.StaggerHpThresholdPct))
                    {
                        state.IsStaggered   = true;
                        state.StaggerEndsAt = Time.time + enemyData.StaggerDurationSec;
                        justStaggered       = true;
                        if (this.enemyAnimatorBySlot.TryGetValue(slotIndex, out var anim) && anim != null)
                            anim.SetBool(IsStaggeredHash, true);
                    }
                    else
                    {
                        state.CurrentPoise = state.InitialPoise; // silent reset — enemy too healthy to stagger yet
                    }
                }
            }

            return new EnemyDamageResult(slotIndex, appliedDamage, state.CurrentHp, false, justStaggered);
        }
```

- [ ] **Step 9: Add `IsEnemyStaggered` and the recovery `Update()`**

Add the query method next to `HasAliveEnemies`:

```csharp
        public bool IsEnemyStaggered(int slotIndex) =>
            this.enemyStateBySlot.TryGetValue(slotIndex, out var state) && state.IsStaggered;
```

Add a new `Update()` method (`BattlefieldView` has no `Update()` today — this is the first one):

```csharp
        private void Update()
        {
            float now = Time.time;
            foreach (var kvp in this.enemyStateBySlot)
            {
                var state = kvp.Value;
                if (!state.IsStaggered || now < state.StaggerEndsAt) continue;

                state.IsStaggered = false;
                if (this.enemyAnimatorBySlot.TryGetValue(kvp.Key, out var anim) && anim != null)
                    anim.SetBool(IsStaggeredHash, false);
            }
        }
```

- [ ] **Step 10: Extend the debug tuple**

Update `GetEnemyHpDebug`:

```csharp
#if UNITY_EDITOR || DEBUG_COMBAT
        public (int Current, int Max, bool IsDead, int Poise, bool IsStaggered) GetEnemyHpDebug(int slotIndex)
        {
            if (this.enemyStateBySlot.TryGetValue(slotIndex, out var state))
                return (state.CurrentHp, state.MaxHp, state.IsDead, state.CurrentPoise, state.IsStaggered);
            return (0, 0, true, 0, false);
        }
#endif
```

- [ ] **Step 11: Update `CombatDebugView` to show Poise/stagger**

In `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatDebugView.cs`, the enemy HP loop currently reads:

```csharp
                for (int i = 0; i < encounter.EnemySlots.Length; i++)
                {
                    EnemyData? data = encounter.EnemySlots[i];
                    if (data == null) continue;
                    var (cur, max, dead) = this.battlefieldView.GetEnemyHpDebug(i);
                    string name = data.EnemyId.Length > 0 ? data.EnemyId : $"Enemy {i}";
                    string hp   = dead ? "<color=#FF4444>DEAD</color>" : $"{cur} / {max}";
                    sb.AppendLine($"  EN[{i}] {name}  {hp}");
                }
```

Replace with:

```csharp
                for (int i = 0; i < encounter.EnemySlots.Length; i++)
                {
                    EnemyData? data = encounter.EnemySlots[i];
                    if (data == null) continue;
                    var (cur, max, dead, poise, staggered) = this.battlefieldView.GetEnemyHpDebug(i);
                    string name = data.EnemyId.Length > 0 ? data.EnemyId : $"Enemy {i}";
                    string hp   = dead ? "<color=#FF4444>DEAD</color>" : $"{cur} / {max}";
                    string poiseText = dead ? "" : staggered ? " <color=#FFAA00>STAGGERED</color>" : $" poise={poise}";
                    sb.AppendLine($"  EN[{i}] {name}  {hp}{poiseText}");
                }
```

- [ ] **Step 12: Fix `ShootCommand`'s now-broken call site**

`ShootCommand` (in `Game/CrimsonDraft/Assets/Scripts/Combat/Commands/ShootCommand.cs`) is not called from anywhere in the codebase today (dead code — confirmed no `new ShootCommand(...)` call site exists), but it still must compile against the new 3-arg `ApplyDamageToEnemy`. Update:

```csharp
        public void Execute()
        {
            var weapon = this.op.ActiveWeapon;
            if (weapon != null)
                weapon.SetAmmo(weapon.CurrentAmmo - this.shotCount);
            int baseDamage = this.op.ActiveWeapon?.BaseDamage ?? CombatMenuController.BaseDamage;
            this.battlefield.ApplyDamageToEnemy(this.targetSlot, this.shotCount * baseDamage, 0);
        }
```

(Poise damage is `0` here — this path has no per-shot zone data to compute it from, and isn't exercised anywhere today.)

- [ ] **Step 13: Fix the now-broken `FakeBattlefieldView` test fake**

In `CombatMenuControllerTests.cs`, `FakeBattlefieldView` (currently around line 692) needs: the new interface method, a one-shot "force the next `ApplyDamageToEnemy` result to report staggered" flag (since the fake doesn't model real Poise math, and this is what the Task 3 tests actually need — nothing in this plan needs `IsEnemyStaggered` itself to return `true`, since `CombatOrchestrator`, the only consumer of that query, has no automated test — so it's a plain stub, not backed by mutable state), and the updated debug tuple. Replace the class body's relevant parts:

Add these fields next to the existing ones:

```csharp
            private bool forceNextResultStaggered;
            public int LastPoiseDamageApplied { get; private set; }
```

Add this method next to `SetEnemyHp`:

```csharp
            public void ForceNextDamageResultStaggered() => this.forceNextResultStaggered = true;
```

Replace `ApplyDamageToEnemy` and add `IsEnemyStaggered`:

```csharp
            public bool IsEnemyStaggered(int slotIndex) => false;

            public EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int hpDamage, int poiseDamage)
            {
                this.LastPoiseDamageApplied = poiseDamage;
                bool staggeredThisHit = this.forceNextResultStaggered;
                this.forceNextResultStaggered = false;

                if (!this.hpBySlot.TryGetValue(slotIndex, out int hp))
                {
                    this.LastDamageResult = new EnemyDamageResult(slotIndex, 0, 0, false, staggeredThisHit);
                    return this.LastDamageResult;
                }

                int applied = Mathf.Max(0, hpDamage);
                int nextHp = Mathf.Max(0, hp - applied);
                this.hpBySlot[slotIndex] = nextHp;
                bool dead = nextHp <= 0;
                if (dead)
                {
                    var next = new System.Collections.Generic.List<int>(this.occupiedSlots.Length);
                    foreach (int slot in this.occupiedSlots)
                    {
                        if (slot != slotIndex)
                            next.Add(slot);
                    }
                    this.occupiedSlots = next.ToArray();
                }

                this.LastDamageResult = new EnemyDamageResult(slotIndex, applied, nextHp, dead, staggeredThisHit);
                return this.LastDamageResult;
            }
```

Update the debug tuple:

```csharp
#if UNITY_EDITOR || DEBUG_COMBAT
            public (int Current, int Max, bool IsDead, int Poise, bool IsStaggered) GetEnemyHpDebug(int slotIndex)
            {
                bool alive = System.Array.IndexOf(this.occupiedSlots, slotIndex) >= 0;
                int  hp    = this.hpBySlot.TryGetValue(slotIndex, out int v) ? v : 0;
                return (hp, 100, !alive, 0, false);
            }
#endif
```

- [ ] **Step 14: Fix the existing tests that call `ApplyDamageToEnemy`/damage assertions with the old 2-arg shape**

`ShotsResolved_hit_appliesDamageUsingShotPayload` (around line 338) calls into `AimingState` indirectly, not `ApplyDamageToEnemy` directly — it does not need code changes, since `AimingState` itself isn't touched until Task 3. Confirm this by running the full suite now.

- [ ] **Step 15: Run the full `CombatMenuControllerTests` suite**

Run all tests in the class via Test Runner.
Expected: everything compiles and PASSes, including the pre-existing tests untouched by this task (they don't depend on the new 3-arg signature since `AimingState` — the only production caller — is updated in Task 3, not here; `FakeBattlefieldView.ApplyDamageToEnemy` compiling against the new 3-arg interface is sufficient for this task).

- [ ] **Step 16: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/EnemyData.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatDebugView.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/Commands/ShootCommand.cs \
        Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat): add enemy Poise state, stagger decision, and debug overlay"
```

---

## Task 3: Wire `AimingState` to drain Poise and notify the orchestrator

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/ICombatOrchestrator.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/CombatOrchestrator.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Interfaces:**
- Consumes: `CombatMenuController.ComputePoiseDamage` (Task 1), `IBattlefieldView.ApplyDamageToEnemy(int, int, int)` returning `EnemyDamageResult.IsStaggered` (Task 2), `IWeaponSlot.PoiseDamage` (Task 1).
- Produces: `ICombatOrchestrator.NotifyEnemyStaggered(int enemySlot)`.

- [ ] **Step 1: Write the failing tests**

Add these tests to `CombatMenuControllerTests.cs`, after `ShotsResolved_hit_appliesDamageUsingShotPayload` (added context from Task 2's step 14, currently ending around line 354):

```csharp
        [Test]
        public void ShotsResolved_legsHit_sendsDoublePoiseDamage()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            InvokeConfirm(c);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Legs, ShotPrecision.Normal, 16) });

            // FakeWeaponSlot's default PoiseDamage is 10 (Task 1) -> legs doubles it to 20.
            Assert.AreEqual(20, this.battlefieldView.LastPoiseDamageApplied);
        }

        [Test]
        public void ShotsResolved_missShot_contributesNoPoiseDamage()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            InvokeConfirm(c);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Miss, ShotPrecision.Normal, 0) });

            Assert.AreEqual(0, this.battlefieldView.LastPoiseDamageApplied);
        }

        [Test]
        public void ShotsResolved_resultStaggered_notifiesOrchestrator()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            this.battlefieldView.ForceNextDamageResultStaggered();
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            InvokeConfirm(c);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

            Assert.AreEqual(1, this.orchestrator.NotifyEnemyStaggeredCallCount);
            Assert.AreEqual(1, this.orchestrator.LastStaggeredSlot);
        }

        [Test]
        public void ShotsResolved_resultNotStaggered_doesNotNotifyOrchestrator()
        {
            this.battlefieldView.SetOccupiedSlots(new[] { 1 });
            this.battlefieldView.SetEnemyHp(1, 100);
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.BeginShootConfiguration(0);

            InvokeConfirm(c);
            InvokeConfirm(c);

            this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20) });

            Assert.AreEqual(0, this.orchestrator.NotifyEnemyStaggeredCallCount);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL to compile — `FakeOrchestrator` doesn't implement `NotifyEnemyStaggered` yet (once added to the interface in Step 3 below, the fake must implement it or the whole file fails to compile — so add the interface member and the fake implementation together before re-running).

- [ ] **Step 3: Add `NotifyEnemyStaggered` to `ICombatOrchestrator`**

`Game/CrimsonDraft/Assets/Scripts/Combat/ICombatOrchestrator.cs` becomes:

```csharp
#nullable enable

namespace CrimsonDraft.Combat
{
    public interface ICombatOrchestrator
    {
        void EnqueueAction(PendingAction action);
        void SetWaitMode(bool paused);
        bool IsOperatorReady(int slotIndex);
        void NotifyShootCompleted();
        void NotifyEnemyStaggered(int enemySlot);
    }
}
```

- [ ] **Step 4: Implement it on `CombatOrchestrator`**

In `Game/CrimsonDraft/Assets/Scripts/Combat/CombatOrchestrator.cs`, add next to `NotifyShootCompleted`:

```csharp
        public void NotifyEnemyStaggered(int enemySlot) =>
            this.atbSystem.ResetActor(enemySlot, ATBActorKind.Enemy);
```

- [ ] **Step 5: Implement it on `FakeOrchestrator`**

In `CombatMenuControllerTests.cs`, `FakeOrchestrator` (currently around line 775) gains:

```csharp
            public int NotifyEnemyStaggeredCallCount { get; private set; }
            public int LastStaggeredSlot             { get; private set; } = -1;
            public void NotifyEnemyStaggered(int enemySlot)
            {
                this.NotifyEnemyStaggeredCallCount++;
                this.LastStaggeredSlot = enemySlot;
            }
```

- [ ] **Step 6: Rewrite `AimingState.HandleShotsResolved`**

In `Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs`, replace the method:

```csharp
        private void HandleShotsResolved(ResolvedShot[] shots)
        {
            this.pendingShots = shots ?? Array.Empty<ResolvedShot>();

            int op = this.context.SelectedOperator;
            var weapon = this.roster.Count > op ? this.roster[op].ActiveWeapon : null;
            int weaponPoiseDamage = weapon?.PoiseDamage ?? 0;

            int totalDamage = 0;
            int totalPoiseDamage = 0;
            foreach (var shot in this.pendingShots)
            {
                totalDamage += Mathf.Max(0, shot.Damage);
                if (shot.Zone != ShotZone.Miss)
                    totalPoiseDamage += CombatMenuController.ComputePoiseDamage(shot.Zone, weaponPoiseDamage);
            }

            if (this.context.CurrentTargetSlot >= 0)
            {
                var result = this.battlefieldView.ApplyDamageToEnemy(
                    this.context.CurrentTargetSlot, totalDamage, totalPoiseDamage);
#if UNITY_EDITOR
                Debug.Log(
                    $"[Combat] Enemy slot={this.context.CurrentTargetSlot} bullets={this.context.SelectedShotCount} damage={result.DamageApplied} hp={result.RemainingHp} dead={result.IsDead}");
#endif
                if (result.IsStaggered)
                    this.context.Orchestrator.NotifyEnemyStaggered(this.context.CurrentTargetSlot);
            }

            if (weapon != null)
                weapon.SetAmmo(weapon.CurrentAmmo - this.context.SelectedShotCount);

            this.awaitingDismiss = true;
        }
```

This folds the previously-separate `int op = this.context.SelectedOperator; if (this.roster.Count > op) { ... }` ammo-deduction block into the single `weapon` lookup already needed for `weaponPoiseDamage`, instead of resolving `ActiveWeapon` twice.

- [ ] **Step 7: Run tests to verify they pass**

Run the full `CombatMenuControllerTests` suite.
Expected: all tests PASS, including the 4 new ones from Step 1 and every pre-existing test in the file (in particular `ShotFired_extraConfirm_playsOperatorShootBurst_withSelectedOperatorEnemyAndShots` and `ShotsResolved_hit_appliesDamageUsingShotPayload`, which exercise the same method and must not regress).

- [ ] **Step 8: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/ICombatOrchestrator.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/CombatOrchestrator.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs \
        Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat): drain enemy Poise per shot and notify orchestrator on stagger"
```

---

## Task 4: Skip staggered enemies in the ATB turn queue

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/CombatOrchestrator.cs`

**Interfaces:**
- Consumes: `IBattlefieldView.IsEnemyStaggered(int slotIndex)` (Task 2), `ICombatOrchestrator.NotifyEnemyStaggered` (Task 3, already resets the ATB gauge — this task stops the queue from acting on a staggered enemy in the first place).

This task has no dedicated automated test — `CombatOrchestrator` is a `MonoBehaviour` with no existing EditMode test file (its `Update()` loop is coupled to `ATBSystem`/`IBattlefieldView`/`IEncounterContext`/`IInventoryService`/`ICombatActionMenuView`/MessagePipe publishers, none of which have a lightweight fake setup in this codebase today). This matches the already-approved design spec's stated test boundary. Verification is manual, in Play Mode, using the `CombatDebugView` overlay extended in Task 2.

- [ ] **Step 1: Skip staggered enemies when enqueuing new attacks**

In `Game/CrimsonDraft/Assets/Scripts/Combat/CombatOrchestrator.cs`, `EnqueueReadyEnemyAttacks()` currently starts:

```csharp
            for (int i = 0; i < this.encounter.EnemySlots.Length; i++)
            {
                EnemyData? data = this.encounter.EnemySlots[i];
                if (data == null) continue;

                ATBActorState? actor = this.atbSystem.GetActor(i, ATBActorKind.Enemy);
```

Add the staggered check right after the `data == null` guard:

```csharp
            for (int i = 0; i < this.encounter.EnemySlots.Length; i++)
            {
                EnemyData? data = this.encounter.EnemySlots[i];
                if (data == null) continue;
                if (this.battlefieldView.IsEnemyStaggered(i)) continue;

                ATBActorState? actor = this.atbSystem.GetActor(i, ATBActorKind.Enemy);
```

- [ ] **Step 2: Discard a queued attack if its owner is staggered by the time it reaches the head of the queue**

In the same file, `ProcessQueueHead()`'s `EnemyAttack` branch currently starts:

```csharp
            if (head.Type == PendingActionType.EnemyAttack)
            {
                if (!this.enemyAttackInProgress)
                {
                    if (IsActorDead(head)) { this.actionQueue.Dequeue(); return; }
                    if (Time.time < this.animationLockUntil) return;
                    this.enemyAttackInProgress = true;
                    ApplyEnemyAttack(head);
                }
```

Add the staggered check between the dead check and the animation-lock check:

```csharp
            if (head.Type == PendingActionType.EnemyAttack)
            {
                if (!this.enemyAttackInProgress)
                {
                    if (IsActorDead(head)) { this.actionQueue.Dequeue(); return; }
                    if (this.battlefieldView.IsEnemyStaggered(head.SlotIndex)) { this.actionQueue.Dequeue(); return; }
                    if (Time.time < this.animationLockUntil) return;
                    this.enemyAttackInProgress = true;
                    ApplyEnemyAttack(head);
                }
```

- [ ] **Step 3: Confirm the project compiles and existing tests still pass**

Run the full EditMode suite via Test Runner (or UnityMCP `run_tests` with no filter).
Expected: no regressions — this task doesn't touch any code path the existing test suite exercises (no test constructs a real `CombatOrchestrator`).

- [ ] **Step 4: Manual verification in Play Mode**

1. Open the `Combat.unity` scene (or trigger a combat encounter from `Navigation.unity` against an enemy using `Enemy_Heavy.asset` or another `EnemyData` asset).
2. Ensure a `CombatDebugView` with its `text` field assigned is present in the scene (existing debug overlay, `#if UNITY_EDITOR || DEBUG_COMBAT`).
3. Enter Play Mode and start combat.
4. Repeatedly shoot the enemy's legs (`ShotZone.Legs`) with an operator whose weapon has `poiseDamage` high enough to zero out the enemy's rolled Poise (`Enemy_Heavy`'s default roll range is 15–30; a `poiseDamage = 10` weapon zeroes it in 2–3 leg hits since legs double it) while keeping its HP below `staggerHpThresholdPct` (default 40%) — plink it down with body shots first if needed.
5. Watch the debug overlay's `[ENEMIES HP]` line for that slot: it should switch from `poise=N` to `STAGGERED` (orange) on the hit that zeroes Poise.
6. In the same overlay's `[ATB ACTORS]` panel, confirm the `EN[<slot>]` row's gauge resets to `0%`/`FILLING` immediately on that hit, and that it does **not** reach `READY` (and no `EnemyAttack` appears in `[QUEUE]` for that slot) until `staggerDurationSec` (default 2.5s) has passed and the overlay's `STAGGERED` label clears back to a `poise=N` reading.
7. If an `EnemyAttack` for that enemy was already sitting in `[QUEUE]` at the moment it got staggered, confirm it disappears from the queue without ever landing (no operator HP loss, no `PlayEnemyAttackFeedback` shake) instead of firing once the animation lock clears.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/CombatOrchestrator.cs
git commit -m "feat(combat): skip staggered enemies in the ATB turn queue"
```

---

## Explicitly out of scope (per the approved design spec)

- Rip ammo's Poise bonus — no ammo-type system exists in code.
- Knocked-down silhouette changing which zones a burst's remaining shots can land on.
- Wiring `Enemy_Combat_Controller.controller`'s actual knockdown animation state/transitions using the `IsStaggered` bool this plan adds — done separately by the user in the Animator Controller editor.
- Any change to the HP pool system (GDD §5.f) — `EnemyData.MaxHp` stays a single fixed value.
