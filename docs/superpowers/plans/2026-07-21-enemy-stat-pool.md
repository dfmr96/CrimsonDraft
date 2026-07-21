# Enemy HP/Speed Stat Pool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Per GDD §5.f, each Wanderer's max HP and ATB speed (`AttackBaseSec`) are rolled once per combat encounter from a small discrete pool defined per enemy type, instead of being fixed values — so the same enemy type can feel a bit tougher/weaker or faster/slower to act each time it's fought.

**Architecture:** `EnemyData`'s single `maxHp`/`attackBaseSec` fields become `maxHpPool: int[]`/`attackBaseSecPool: float[]`. `BattlefieldView.Populate()` rolls HP once per enemy slot the same way it already rolls Poise (existing `IRandomSource` pattern). `CombatOrchestrator` rolls and caches `AttackBaseSec` per slot in a `Dictionary<int, float>`, reusing its existing `IRandomSource random` field, so the same rolled value is reused for both the initial ATB config and every subsequent attack-cycle reset within one encounter. The two systems never need each other's rolled value, so there's no shared service and no initialization-order dependency.

**Tech Stack:** Unity C#, ScriptableObject, NUnit EditMode (compile/regression verification only — see Global Constraints).

## Global Constraints

- `#nullable enable` at the top of every touched file (already present in all three).
- No `System.Linq` in `Combat/` — use plain loops, matching existing convention.
- No `Co-Authored-By` trailers in commit messages (project convention, `CLAUDE.md`).
- Tests run via Unity Test Runner / UnityMCP `run_tests` — there is no CLI test command in this project.
- Per the approved spec, neither `CombatOrchestrator`'s nor `BattlefieldView`'s roll logic gets a dedicated automated test — both are deeply `MonoBehaviour`/`Update()`-coupled classes with no existing test file, matching the established boundary from the Poise, Focus Fire, and Room Enemy Reset work earlier on this branch. Each task is verified by compiling clean and running the full existing EditMode suite to confirm no regressions; final correctness is verified manually in Play Mode (checklist at the end of this plan).

---

## Task 1: `EnemyData` pools + seed the two existing assets

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/EnemyData.cs`
- Modify: `Game/CrimsonDraft/Assets/Data/Enemies/Enemy_Grunt.asset`
- Modify: `Game/CrimsonDraft/Assets/Data/Enemies/Enemy_Heavy.asset`

**Interfaces:**
- Produces: `EnemyData.MaxHpPool : int[]`, `EnemyData.AttackBaseSecPool : float[]`. Removes: `EnemyData.MaxHp`, `EnemyData.AttackBaseSec`.

- [ ] **Step 1: Replace the fixed fields with pools**

`EnemyData.cs` — current relevant lines:

```csharp
        [SerializeField] private int                maxHp          = 100;
        [SerializeField, Min(0f)] private float     attackBaseSec = 7f;
```

become:

```csharp
        [SerializeField] private int[]              maxHpPool          = System.Array.Empty<int>();
        [SerializeField] private float[]             attackBaseSecPool = System.Array.Empty<float>();
```

And the properties:

```csharp
        public int MaxHp                         => this.maxHp;
        public float AttackBaseSec               => this.attackBaseSec;
```

become:

```csharp
        public int[] MaxHpPool                   => this.maxHpPool;
        public float[] AttackBaseSecPool         => this.attackBaseSecPool;
```

Leave every other field/property in the file untouched (`enemyId`, `battlefieldPrefab`, `sprite`, `hitMaskProfile`, `attackJitterSec`, `attackDurationSec`, `attackDamage`, `initialGaugePct`, the poise fields).

- [ ] **Step 2: Confirm the project compiles**

Run `mcp__UnityMCP__refresh_unity` (force recompile) → `mcp__UnityMCP__read_console` filtered for `CS` errors.
Expected: compile errors in `CombatOrchestrator.cs` (`data.AttackBaseSec` no longer exists) and `BattlefieldView.cs` (`enemy.MaxHp` no longer exists) — this is expected at this point; Tasks 2 and 3 fix them. Confirm there are no OTHER unexpected errors (e.g. no stray references to the removed properties anywhere else — a project-wide grep for `.MaxHp` / `.AttackBaseSec` before starting this plan turned up exactly these two call sites and nothing else).

- [ ] **Step 3: Seed `Enemy_Grunt.asset`**

Open `Game/CrimsonDraft/Assets/Data/Enemies/Enemy_Grunt.asset` in the Inspector (or edit the YAML directly — it's a small ScriptableObject asset). Current relevant lines:

```yaml
  maxHp: 100
  attackBaseSec: 4
```

Replace with:

```yaml
  maxHpPool: [75, 85, 95, 105, 115]
  attackBaseSecPool: [3, 3.5, 4, 4.5, 5]
```

(Exact YAML array syntax may differ slightly from Unity's own serialization format — the reliable way to do this is via the Inspector: select the asset, set `Size` to 5 under both `Max Hp Pool` and `Attack Base Sec Pool`, and fill in the five values each. Using `mcp__UnityMCP__manage_asset` or direct `SerializedObject` editing via UnityMCP is also acceptable if editing through the Editor UI isn't practical.)

- [ ] **Step 4: Seed `Enemy_Heavy.asset`**

Same as Step 3, for `Game/CrimsonDraft/Assets/Data/Enemies/Enemy_Heavy.asset`. Current relevant lines:

```yaml
  maxHp: 100
  attackBaseSec: 8.5
```

New pool values:

```yaml
  maxHpPool: [110, 125, 140, 155, 170]
  attackBaseSecPool: [7, 7.5, 8.5, 9.5, 10]
```

No commit yet — this task alone leaves the project not compiling (`CombatOrchestrator.cs`/`BattlefieldView.cs` still reference the removed `MaxHp`/`AttackBaseSec` properties). Continue straight into Task 2; all three tasks land in a single commit at the end of Task 3, once the project compiles clean and the full suite passes again.

---

## Task 2: `BattlefieldView` — roll HP once per enemy

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs`

**Interfaces:**
- Consumes: `EnemyData.MaxHpPool` (Task 1).

- [ ] **Step 1: Add a dedicated random source and a default-HP constant**

Next to the existing `poiseRandom` field (line 59):

```csharp
        private readonly IRandomSource poiseRandom = new UnityRandomSource();
        private readonly IRandomSource enemyStatRandom = new UnityRandomSource();
        private const int DefaultMaxHp = 100;
```

- [ ] **Step 2: Roll HP in `Populate()`**

Current code (inside the enemy-slot loop in `Populate()`, right before building `EnemyRuntimeState`):

```csharp
                int rolledPoise = this.poiseRandom.NextInt(enemy.MinPoise, enemy.MaxPoise + 1);
                this.enemyStateBySlot[i] = new EnemyRuntimeState
                {
                    CurrentHp               = Mathf.Max(1, enemy.MaxHp),
                    MaxHp                   = Mathf.Max(1, enemy.MaxHp),
                    IsDead                  = false,
                    CurrentPoise            = rolledPoise,
                    InitialPoise            = rolledPoise,
                    IsStaggered             = false,
                    StaggerActionsRemaining = 0,
                    RecoveryQueued          = false
                };
```

Replace with:

```csharp
                int rolledPoise = this.poiseRandom.NextInt(enemy.MinPoise, enemy.MaxPoise + 1);
                int rolledMaxHp = RollMaxHp(enemy);
                this.enemyStateBySlot[i] = new EnemyRuntimeState
                {
                    CurrentHp               = Mathf.Max(1, rolledMaxHp),
                    MaxHp                   = Mathf.Max(1, rolledMaxHp),
                    IsDead                  = false,
                    CurrentPoise            = rolledPoise,
                    InitialPoise            = rolledPoise,
                    IsStaggered             = false,
                    StaggerActionsRemaining = 0,
                    RecoveryQueued          = false
                };
```

- [ ] **Step 3: Add the `RollMaxHp` helper**

Add as a private method (near `Populate()`, e.g. right after it):

```csharp
        private int RollMaxHp(EnemyData enemy)
        {
            int[] pool = enemy.MaxHpPool;
            if (pool == null || pool.Length == 0)
            {
                Debug.LogWarning($"[BattlefieldView] {enemy.name} has no MaxHpPool configured; using default {DefaultMaxHp}.", enemy);
                return DefaultMaxHp;
            }

            return pool[this.enemyStatRandom.NextInt(0, pool.Length)];
        }
```

- [ ] **Step 4: Confirm the project compiles**

Run `mcp__UnityMCP__refresh_unity` (force recompile) → `mcp__UnityMCP__read_console` filtered for `CS` errors.
Expected: the `enemy.MaxHp` compile error from Task 1 is now resolved. The `data.AttackBaseSec` error in `CombatOrchestrator.cs` (Task 3) is still expected at this point.

No commit yet — `CombatOrchestrator.cs` still references the removed `data.AttackBaseSec`. Continue into Task 3, which lands the commit covering all three tasks.

---

## Task 3: `CombatOrchestrator` — roll and cache AttackBaseSec once per encounter

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/CombatOrchestrator.cs`

**Interfaces:**
- Consumes: `EnemyData.AttackBaseSecPool` (Task 1).
- Produces: `CombatOrchestrator.GetOrRollAttackBaseSec(int, EnemyData) : float` (private).

- [ ] **Step 1: Add the per-encounter cache and default constant**

Next to the existing RNG/collection fields (around line 33-36):

```csharp
        private readonly IRandomSource  random               = new UnityRandomSource();
        private readonly HashSet<int>   knownAliveEnemySlots = new();
        private readonly HashSet<int>   syncAliveSet         = new();
        private readonly List<int>      syncDeadBuf          = new();
        private readonly Dictionary<int, float> rolledEnemyAttackBaseSec = new();

        private const float DefaultAttackBaseSec = 7f;
```

- [ ] **Step 2: Add `GetOrRollAttackBaseSec`**

Add as a private method, e.g. right before `BuildATBConfigs`:

```csharp
        private float GetOrRollAttackBaseSec(int slotIndex, EnemyData data)
        {
            if (this.rolledEnemyAttackBaseSec.TryGetValue(slotIndex, out float cached))
                return cached;

            float[] pool = data.AttackBaseSecPool;
            float rolled;
            if (pool == null || pool.Length == 0)
            {
                Debug.LogWarning($"[CombatOrchestrator] {data.name} has no AttackBaseSecPool configured; using default {DefaultAttackBaseSec}.", data);
                rolled = DefaultAttackBaseSec;
            }
            else
            {
                rolled = pool[this.random.NextInt(0, pool.Length)];
            }

            this.rolledEnemyAttackBaseSec[slotIndex] = rolled;
            return rolled;
        }
```

- [ ] **Step 3: Make `BuildATBConfigs` an instance method and use the roll**

Current signature and enemy loop:

```csharp
        private static List<ATBActorConfig> BuildATBConfigs(EncounterData encounter, IOperatorRoster roster, float divisor)
        {
            var configs = new List<ATBActorConfig>();

            for (int i = 0; i < roster.Count; i++)
            {
                int speed = roster[i].Data?.Speed ?? 50;
                configs.Add(new ATBActorConfig(i, ATBActorKind.Operator, speed / divisor));
            }

            for (int i = 0; i < encounter.EnemySlots.Length; i++)
            {
                EnemyData? data = encounter.EnemySlots[i];
                if (data == null) continue;
                float gps          = data.AttackBaseSec > 0f ? 1f / data.AttackBaseSec : 1f;
                float initialGauge = data.InitialGaugePct / 100f;
                configs.Add(new ATBActorConfig(i, ATBActorKind.Enemy, gps, initialGauge));
            }

            return configs;
        }
```

Replace with (drops `static`, drops the now-unused `roster`/`divisor` params being passed in from a static context — they're still parameters, just no longer requiring the method to be static):

```csharp
        private List<ATBActorConfig> BuildATBConfigs(EncounterData encounter, IOperatorRoster roster, float divisor)
        {
            var configs = new List<ATBActorConfig>();

            for (int i = 0; i < roster.Count; i++)
            {
                int speed = roster[i].Data?.Speed ?? 50;
                configs.Add(new ATBActorConfig(i, ATBActorKind.Operator, speed / divisor));
            }

            for (int i = 0; i < encounter.EnemySlots.Length; i++)
            {
                EnemyData? data = encounter.EnemySlots[i];
                if (data == null) continue;
                float attackBaseSec = GetOrRollAttackBaseSec(i, data);
                float gps          = attackBaseSec > 0f ? 1f / attackBaseSec : 1f;
                float initialGauge = data.InitialGaugePct / 100f;
                configs.Add(new ATBActorConfig(i, ATBActorKind.Enemy, gps, initialGauge));
            }

            return configs;
        }
```

The call site at line 79 (`var configs = BuildATBConfigs(this.encounter, this.roster, this.atbGaugeDivisor);`) doesn't need to change — it already calls this as `this.BuildATBConfigs(...)` implicitly (instance call syntax is identical to the static call syntax it already used).

- [ ] **Step 4: Use the roll in `EnqueueReadyEnemyAttacks`**

Current line (inside the enemy loop, ~line 229):

```csharp
                float nextSec = Mathf.Max(0.1f, data.AttackBaseSec);
```

Replace with:

```csharp
                float nextSec = Mathf.Max(0.1f, GetOrRollAttackBaseSec(i, data));
```

- [ ] **Step 5: Confirm the project compiles**

Run `mcp__UnityMCP__refresh_unity` (force recompile) → `mcp__UnityMCP__read_console` filtered for `CS` errors.
Expected: no compile errors — both previously-expected errors from Task 1 are now resolved.

- [ ] **Step 6: Run the full EditMode suite to confirm no regressions**

Run via UnityMCP `run_tests` (EditMode), no filter.
Expected: same pass/fail counts as before this task (no new failures introduced beyond the already-known pre-existing failures: `CombatMenuControllerTests.ShotCount_cancel_returnsToCommandPanel` and the 17 `InventoryServiceTests` failures).

- [ ] **Step 7: Commit all three tasks together**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/EnemyData.cs \
        Game/CrimsonDraft/Assets/Data/Enemies/Enemy_Grunt.asset \
        Game/CrimsonDraft/Assets/Data/Enemies/Enemy_Heavy.asset \
        Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs \
        Game/CrimsonDraft/Assets/Scripts/Combat/CombatOrchestrator.cs
git commit -m "feat(combat): roll enemy HP and attack speed from a discrete pool per encounter"
```

(Tasks 1 and 2 left the project non-compiling on their own — see their notes — so this single commit is the first point where everything compiles and the suite passes, and covers all three tasks' files together.)

---

## Manual verification (Play Mode)

Not covered by the automated suite (see Global Constraints). After all 3 tasks:

1. Enter combat against an `Enemy_Grunt`. Note its approximate HP (via `CombatDebugView` or the enemy HP bar/debug overlay if visible) and how quickly it acts.
2. Flee or finish the encounter, then trigger a fresh combat against another `Enemy_Grunt` (same type). Confirm the HP and/or attack cadence is noticeably different from the first encounter at least some of the time (it's a random roll from a 5-value pool, so occasionally repeating the same value is expected — repeat a few times to see variance).
3. Repeat for `Enemy_Heavy` — confirm it generally feels tankier (higher HP pool: 110–170) and slower to act (higher AttackBaseSec pool: 7–10s) than `Enemy_Grunt`.
4. Within a single encounter, confirm an enemy's attack cadence stays consistent across multiple attacks (it shouldn't re-roll a different speed mid-fight — same enemy, same encounter, same rolled value every time it acts).
5. Re-enter the same room and re-trigger combat with the same enemy (using the Room Enemy Reset feature from earlier on this branch) — confirm HP/speed can come out different this time too, matching the GDD's "reentrando a la misma sala" requirement.
6. Check the console for no unexpected `[BattlefieldView]`/`[CombatOrchestrator]` warnings about missing pools (both `Enemy_Grunt.asset` and `Enemy_Heavy.asset` should have been seeded in Task 1).
