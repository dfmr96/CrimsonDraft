# Enemy HP/Speed Stat Pool — Design Spec

## Problem

Per GDD §5.f, Wanderers should not have fixed HP and speed: each combat encounter should roll a max HP value and a speed value from a small predefined discrete pool per enemy type, instead of a fixed number. This means the same enemy type can feel slightly different — a bit tougher or weaker, a bit faster or slower to act in the ATB — each time it's fought, even re-entering the same room.

## Current State

`EnemyData` (`Assets/Scripts/Combat/Data/EnemyData.cs`) has two single fixed fields relevant here:
- `maxHp: int` (default 100) — read once by `BattlefieldView.Populate()` to seed `EnemyRuntimeState.CurrentHp`/`MaxHp`.
- `attackBaseSec: float` (default 7f) — the enemy's "seconds per action," which doubles as its ATB speed stat. Read in two places in `CombatOrchestrator.cs`:
  - `BuildATBConfigs` (encounter start): `GaugePerSecond = 1 / AttackBaseSec`.
  - `EnqueueReadyEnemyAttacks` (every time the enemy is ready to act again): resets its gauge rate using the same `AttackBaseSec`.

There is no existing "speed" stat separate from `AttackBaseSec` — it already serves that role for enemies today, just expressed in seconds rather than an abstract unit. `EnemyData` already has a precedent for a per-encounter random roll: `MinPoise`/`MaxPoise` are rolled once per enemy when `BattlefieldView.Populate()` runs, using a dedicated `IRandomSource` field (`poiseRandom`).

Exactly two `EnemyData` assets exist today: `Enemy_Grunt` (HP 100, AttackBaseSec 4s) and `Enemy_Heavy` (HP 100, AttackBaseSec 8.5s).

## Goal

Replace the single fixed `maxHp`/`attackBaseSec` fields with discrete pools. Roll one value from each pool per enemy, once per combat encounter, and keep using that same rolled value for the rest of the encounter (both systems that currently read `AttackBaseSec` — initial ATB config and every subsequent attack-cycle reset — must agree on the same rolled value for a given enemy slot within one encounter).

## Design

### `EnemyData.cs`

- `[SerializeField] private int maxHp = 100;` → `[SerializeField] private int[] maxHpPool = Array.Empty<int>();`
- `[SerializeField, Min(0f)] private float attackBaseSec = 7f;` → `[SerializeField] private float[] attackBaseSecPool = Array.Empty<float>();`
- Public properties exposed as raw arrays (matching the existing convention elsewhere in the codebase, e.g. `EncounterData.EnemySlots`): `public int[] MaxHpPool => this.maxHpPool;`, `public float[] AttackBaseSecPool => this.attackBaseSecPool;`.
- The old `MaxHp`/`AttackBaseSec` single-value properties are removed — every consumer goes through the roll (see below) so nothing can accidentally read stale/unrolled data.
- If a pool is empty at roll time, it's a configuration error: log a warning and fall back to a generic default (100 HP / 7s — the same values the old fixed fields defaulted to), so the enemy still functions rather than crashing (empty-array index, or division by zero) while the empty pool is visible to whoever left it unconfigured.

### `BattlefieldView.cs` — HP roll

`Populate()` already rolls Poise once per enemy via a dedicated `IRandomSource poiseRandom` field. Add a sibling field (e.g. `enemyStatRandom`) and roll HP the same way, replacing the direct `enemy.MaxHp` reads:

```csharp
int rolledMaxHp = RollFromPool(enemy.MaxHpPool, DefaultMaxHp, enemy);
...
CurrentHp = Mathf.Max(1, rolledMaxHp),
MaxHp     = Mathf.Max(1, rolledMaxHp),
```

Where `RollFromPool` picks a random index via `enemyStatRandom.NextInt(0, pool.Length)` when the pool is non-empty, otherwise logs a warning and returns the generic default. This is a per-slot roll happening once, at the same point in `Populate()` where Poise is already rolled — no new ordering dependency.

### `CombatOrchestrator.cs` — AttackBaseSec roll

Add a per-encounter cache: `private readonly Dictionary<int, float> rolledEnemyAttackBaseSec = new();`. Add a private helper:

```csharp
private float GetOrRollAttackBaseSec(int slotIndex, EnemyData data)
{
    if (this.rolledEnemyAttackBaseSec.TryGetValue(slotIndex, out float cached))
        return cached;

    float[] pool = data.AttackBaseSecPool;
    float rolled = pool != null && pool.Length > 0
        ? pool[this.random.NextInt(0, pool.Length)]
        : DefaultAttackBaseSec; // + Debug.LogWarning if empty

    this.rolledEnemyAttackBaseSec[slotIndex] = rolled;
    return rolled;
}
```

Reuses the existing `IRandomSource random` field already used for target selection — no new RNG source needed here. Both call sites switch from `data.AttackBaseSec` to `GetOrRollAttackBaseSec(i, data)`:
- `BuildATBConfigs` (becomes an instance method instead of `static`, since it now needs `this.rolledEnemyAttackBaseSec`/`this.random`).
- `EnqueueReadyEnemyAttacks`.

Because the dictionary caches by slot index, the first call (during `BuildATBConfigs` at encounter start) performs the roll; every subsequent call for that same slot during the same encounter (each time the enemy is ready to act again) returns the cached value — satisfying "rolled once per encounter, reused for the rest of it."

### Why no shared service or init-order concern

HP and speed are read by two entirely separate systems that never need each other's rolled value: `BattlefieldView` only ever needs `MaxHp`, `CombatOrchestrator` only ever needs `AttackBaseSec`. Each independently owns its own roll-and-cache for its own concern, mirroring the pattern `BattlefieldView` already uses for Poise. No new class, no new DI registration, and no dependency on which of `CombatOrchestrator`/`BattlefieldPresenter` initializes first (a real fragility this project hit once already this session for an unrelated reason).

### Data — pool values

Seeded directly into the two existing assets as part of this implementation (not left empty), inspired by RE2's discrete zombie HP pool (`Design/References/GD_RE2_Combate.md` §1.2) adapted to this game's existing ~100 HP / 4–8.5s baseline, with `Enemy_Heavy` shifted up to justify its name:

| Enemy | HP pool | AttackBaseSec pool |
|---|---|---|
| Enemy_Grunt | 75, 85, 95, 105, 115 | 3, 3.5, 4, 4.5, 5 |
| Enemy_Heavy | 110, 125, 140, 155, 170 | 7, 7.5, 8.5, 9.5, 10 |

## Edge Cases

- **Empty pool** (new `EnemyData` asset authored without configuring one): warning logged, generic default (100 HP / 7s) used — enemy still functions.
- **Re-entering the same room / re-fighting the same encounter:** each combat start re-runs `BattlefieldView.Populate()` and `CombatOrchestrator.Initialize()` fresh, so both rolls happen again independently — this is exactly the "feels slightly different each time" behavior the GDD asks for.
- **Single-value pool** (e.g. `{100}`): degenerates to the old fixed-value behavior for that stat — a valid, intentional way to opt an enemy type out of variance if ever needed.

## Testing

- `CombatOrchestrator`'s `GetOrRollAttackBaseSec` caching behavior is not covered by a dedicated automated test — `CombatOrchestrator` has no EditMode test file today (matching the established boundary for this deeply `MonoBehaviour`/`Update()`-coupled class, same as the Poise and Focus Fire work earlier this branch).
- `BattlefieldView`'s HP roll is likewise part of `Populate()`, already untested at this level (Poise rolling isn't unit-tested either, for the same reason).
- Verified by compiling clean and manual Play Mode verification: enter combat against the same enemy type multiple times (e.g. flee/retry, or re-enter the room) and confirm HP and attack cadence visibly vary run to run within the configured pool's range.
