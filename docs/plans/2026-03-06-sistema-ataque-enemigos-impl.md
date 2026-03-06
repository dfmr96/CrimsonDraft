# Sistema de Ataque de Enemigos Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implementar el scheduler de ataques enemigos con lock por duración, daño instantáneo a operadores y feedback visual (vibración, texto flotante y flash rojo en ECG).

**Architecture:** Lógica de scheduling en clase pura testable (`EnemyAttackScheduler`) + controlador MonoBehaviour que integra Encounter/roster/feedback en escena. Roster de operadores con HP runtime para seleccionar objetivos vivos y aplicar daño. Feedback visual centralizado en `BattlefieldView` y `OperatorEcgWidget`.

**Tech Stack:** Unity, VContainer, MessagePipe, DOTween, NUnit (EditMode tests).

**Spec:** Implements `[[Sistema de Ataque de Enemigos]]` (GDD).

---

### Task 1: EnemyAttackScheduler Tests

**Files:**
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/EnemyAttackSchedulerTests.cs`

**Step 1: Write the failing tests**

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class EnemyAttackSchedulerTests
    {
        private sealed class FakeRandom : IRandomSource
        {
            private readonly float[] floats;
            private int index;

            public FakeRandom(params float[] floats)
            {
                this.floats = floats.Length == 0 ? new[] { 0f } : floats;
            }

            public float NextFloat01()
            {
                float value = this.floats[this.index % this.floats.Length];
                this.index++;
                return Mathf.Clamp01(value);
            }

            public int NextInt(int minInclusive, int maxExclusive)
            {
                float t = NextFloat01();
                return Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(minInclusive, maxExclusive, t)), minInclusive, maxExclusive - 1);
            }
        }

        [Test]
        public void Scheduler_doesNotAttackWhileLocked()
        {
            var random = new FakeRandom(0f);
            var scheduler = new EnemyAttackScheduler(random);
            scheduler.Initialize(new[]
            {
                new EnemyAttackConfig(0, 2f, 0f, 1f, 10),
            }, now: 0f);

            // First attack at t=2
            Assert.IsTrue(scheduler.TryScheduleAttack(2f, new[] { 0 }, out var attack));
            Assert.AreEqual(0, attack.AttackerSlot);
            Assert.AreEqual(0, attack.TargetSlot);

            // Locked until t=3
            Assert.IsFalse(scheduler.TryScheduleAttack(2.5f, new[] { 0 }, out _));
        }

        [Test]
        public void Scheduler_picksSoonestReadyEnemy()
        {
            var random = new FakeRandom(0f, 0f);
            var scheduler = new EnemyAttackScheduler(random);
            scheduler.Initialize(new[]
            {
                new EnemyAttackConfig(0, 3f, 0f, 0.5f, 10),
                new EnemyAttackConfig(1, 2f, 0f, 0.5f, 10),
            }, now: 0f);

            Assert.IsTrue(scheduler.TryScheduleAttack(2f, new[] { 0 }, out var attack));
            Assert.AreEqual(1, attack.AttackerSlot);
        }

        [Test]
        public void Scheduler_selectsRandomTargetFromAliveOperators()
        {
            var random = new FakeRandom(0.9f);
            var scheduler = new EnemyAttackScheduler(random);
            scheduler.Initialize(new[]
            {
                new EnemyAttackConfig(0, 1f, 0f, 0.2f, 10),
            }, now: 0f);

            Assert.IsTrue(scheduler.TryScheduleAttack(1f, new[] { 0, 2, 3 }, out var attack));
            Assert.AreEqual(3, attack.TargetSlot);
        }

        [Test]
        public void Scheduler_recomputesNextAttackWithJitter()
        {
            var random = new FakeRandom(1f); // max jitter
            var scheduler = new EnemyAttackScheduler(random);
            scheduler.Initialize(new[]
            {
                new EnemyAttackConfig(0, 2f, 1f, 0.5f, 10),
            }, now: 0f);

            scheduler.TryScheduleAttack(2f, new[] { 0 }, out var attack);
            // next_attack_time = now + base + jitter = 2 + 2 + 1 = 5
            Assert.AreEqual(5f, attack.NextAttackTime, 0.001f);
        }
    }
}
```

**Step 2: Run test to verify it fails**

Run: `"C:\Program Files\Unity\Hub\Editor\<VERSION>\Editor\Unity.exe" -runTests -projectPath "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\.worktrees\enemy-attack\Game\CrimsonDraft" -testPlatform editmode -testResults "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\.worktrees\enemy-attack\TestResults.xml"`
Expected: FAIL because `EnemyAttackScheduler` and `IRandomSource` do not exist.

**Step 3: Commit failing test**

```bash
git add Game/CrimsonDraft/Assets/Tests/EditMode/EnemyAttackSchedulerTests.cs
git commit -m "test: add enemy attack scheduler tests"
```

---

### Task 2: Implement EnemyAttackScheduler

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/EnemyAttackScheduler.cs`

**Step 1: Write minimal implementation**

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface IRandomSource
    {
        float NextFloat01();
        int NextInt(int minInclusive, int maxExclusive);
    }

    public sealed class UnityRandomSource : IRandomSource
    {
        public float NextFloat01() => UnityEngine.Random.value;
        public int NextInt(int minInclusive, int maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    public readonly struct EnemyAttackConfig
    {
        public int   SlotIndex { get; }
        public float BaseSec   { get; }
        public float JitterSec { get; }
        public float DurationSec { get; }
        public int   Damage    { get; }

        public EnemyAttackConfig(int slotIndex, float baseSec, float jitterSec, float durationSec, int damage)
        {
            SlotIndex = slotIndex;
            BaseSec = Mathf.Max(0f, baseSec);
            JitterSec = Mathf.Max(0f, jitterSec);
            DurationSec = Mathf.Max(0f, durationSec);
            Damage = Mathf.Max(0, damage);
        }
    }

    public readonly struct EnemyAttackResult
    {
        public int AttackerSlot { get; }
        public int TargetSlot { get; }
        public int Damage { get; }
        public float LockUntil { get; }
        public float NextAttackTime { get; }

        public EnemyAttackResult(int attackerSlot, int targetSlot, int damage, float lockUntil, float nextAttackTime)
        {
            AttackerSlot = attackerSlot;
            TargetSlot = targetSlot;
            Damage = damage;
            LockUntil = lockUntil;
            NextAttackTime = nextAttackTime;
        }
    }

    internal sealed class EnemyAttackState
    {
        public EnemyAttackConfig Config;
        public float NextAttackTime;
        public bool IsDead;
    }

    public sealed class EnemyAttackScheduler
    {
        private readonly IRandomSource random;
        private readonly List<EnemyAttackState> states = new();
        private float lockUntil;

        public EnemyAttackScheduler(IRandomSource random)
        {
            this.random = random;
        }

        public void Initialize(IReadOnlyList<EnemyAttackConfig> configs, float now)
        {
            this.states.Clear();
            this.lockUntil = 0f;
            for (int i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                this.states.Add(new EnemyAttackState
                {
                    Config = config,
                    NextAttackTime = now + ComputeCooldown(config),
                    IsDead = false
                });
            }
        }

        public void MarkDead(int slotIndex)
        {
            foreach (var state in this.states)
            {
                if (state.Config.SlotIndex == slotIndex)
                    state.IsDead = true;
            }
        }

        public bool TryScheduleAttack(float now, IReadOnlyList<int> aliveOperatorSlots, out EnemyAttackResult attack)
        {
            attack = default;
            if (now < this.lockUntil)
                return false;
            if (aliveOperatorSlots == null || aliveOperatorSlots.Count == 0)
                return false;

            EnemyAttackState? best = null;
            for (int i = 0; i < this.states.Count; i++)
            {
                var state = this.states[i];
                if (state.IsDead)
                    continue;
                if (state.NextAttackTime > now)
                    continue;
                if (best == null || state.NextAttackTime < best.NextAttackTime)
                    best = state;
            }

            if (best == null)
                return false;

            int targetIndex = this.random.NextInt(0, aliveOperatorSlots.Count);
            int targetSlot = aliveOperatorSlots[targetIndex];

            float nextAttack = now + ComputeCooldown(best.Config);
            float lockUntil = now + best.Config.DurationSec;

            best.NextAttackTime = nextAttack;
            this.lockUntil = lockUntil;

            attack = new EnemyAttackResult(best.Config.SlotIndex, targetSlot, best.Config.Damage, lockUntil, nextAttack);
            return true;
        }

        private float ComputeCooldown(EnemyAttackConfig config)
        {
            if (config.JitterSec <= 0f)
                return config.BaseSec;
            float jitter = Mathf.Lerp(-config.JitterSec, config.JitterSec, this.random.NextFloat01());
            return Mathf.Max(0f, config.BaseSec + jitter);
        }
    }
}
```

**Step 2: Run tests**

Run: `"C:\Program Files\Unity\Hub\Editor\<VERSION>\Editor\Unity.exe" -runTests -projectPath "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\.worktrees\enemy-attack\Game\CrimsonDraft" -testPlatform editmode -testResults "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\.worktrees\enemy-attack\TestResults.xml"`
Expected: PASS for `EnemyAttackSchedulerTests`.

**Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/EnemyAttackScheduler.cs
git commit -m "feat: add enemy attack scheduler core"
```

---

### Task 3: CombatOperatorRoster Tests

**Files:**
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatOperatorRosterTests.cs`

**Step 1: Write the failing tests**

```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class CombatOperatorRosterTests
    {
        [Test]
        public void ApplyDamage_clampsToZero_andMarksDead()
        {
            var roster = new CombatOperatorRoster();
            roster.Initialize(3, defaultHp: 100);

            roster.ApplyDamage(1, 150);

            Assert.AreEqual(0, roster.GetHp(1));
            Assert.IsFalse(roster.IsAlive(1));
        }

        [Test]
        public void AliveSlots_excludesDeadOperators()
        {
            var roster = new CombatOperatorRoster();
            roster.Initialize(3, defaultHp: 100);
            roster.ApplyDamage(0, 100);

            var alive = roster.GetAliveSlots();
            Assert.AreEqual(2, alive.Count);
            CollectionAssert.DoesNotContain(alive, 0);
        }
    }
}
```

**Step 2: Run test to verify it fails**

Run: `"C:\Program Files\Unity\Hub\Editor\<VERSION>\Editor\Unity.exe" -runTests -projectPath "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\.worktrees\enemy-attack\Game\CrimsonDraft" -testPlatform editmode -testResults "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\.worktrees\enemy-attack\TestResults.xml"`
Expected: FAIL because `CombatOperatorRoster` does not exist.

**Step 3: Commit failing test**

```bash
git add Game/CrimsonDraft/Assets/Tests/EditMode/CombatOperatorRosterTests.cs
git commit -m "test: add combat operator roster tests"
```

---

### Task 4: Implement CombatOperatorRoster

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/CombatOperatorRoster.cs`

**Step 1: Implement minimal roster**

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public sealed class CombatOperatorRoster
    {
        private int[] hpBySlot = System.Array.Empty<int>();
        private bool[] aliveBySlot = System.Array.Empty<bool>();

        public void Initialize(int slotCount, int defaultHp)
        {
            int count = Mathf.Max(0, slotCount);
            this.hpBySlot = new int[count];
            this.aliveBySlot = new bool[count];
            for (int i = 0; i < count; i++)
            {
                this.hpBySlot[i] = Mathf.Max(0, defaultHp);
                this.aliveBySlot[i] = this.hpBySlot[i] > 0;
            }
        }

        public int GetHp(int slotIndex) => slotIndex >= 0 && slotIndex < this.hpBySlot.Length ? this.hpBySlot[slotIndex] : 0;
        public bool IsAlive(int slotIndex) => slotIndex >= 0 && slotIndex < this.aliveBySlot.Length && this.aliveBySlot[slotIndex];

        public void ApplyDamage(int slotIndex, int damage)
        {
            if (slotIndex < 0 || slotIndex >= this.hpBySlot.Length)
                return;
            if (!this.aliveBySlot[slotIndex])
                return;

            int applied = Mathf.Max(0, damage);
            int nextHp = Mathf.Max(0, this.hpBySlot[slotIndex] - applied);
            this.hpBySlot[slotIndex] = nextHp;
            if (nextHp <= 0)
                this.aliveBySlot[slotIndex] = false;
        }

        public IReadOnlyList<int> GetAliveSlots()
        {
            var list = new List<int>();
            for (int i = 0; i < this.aliveBySlot.Length; i++)
            {
                if (this.aliveBySlot[i])
                    list.Add(i);
            }
            return list;
        }
    }
}
```

**Step 2: Run tests**

Run: `"C:\Program Files\Unity\Hub\Editor\<VERSION>\Editor\Unity.exe" -runTests -projectPath "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\.worktrees\enemy-attack\Game\CrimsonDraft" -testPlatform editmode -testResults "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\.worktrees\enemy-attack\TestResults.xml"`
Expected: PASS for `CombatOperatorRosterTests`.

**Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/CombatOperatorRoster.cs
git commit -m "feat: add combat operator roster"
```

---

### Task 5: Extend EnemyData with Attack Fields

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/EnemyData.cs`

**Step 1: Add serialized fields**

```csharp
[SerializeField, Min(0f)] private float attackBaseSec = 3f;
[SerializeField, Min(0f)] private float attackJitterSec = 0.5f;
[SerializeField, Min(0f)] private float attackDurationSec = 0.6f;
[SerializeField, Min(0)] private int attackDamage = 10;

public float AttackBaseSec => this.attackBaseSec;
public float AttackJitterSec => this.attackJitterSec;
public float AttackDurationSec => this.attackDurationSec;
public int AttackDamage => this.attackDamage;
```

**Step 2: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/Data/EnemyData.cs
git commit -m "feat: add enemy attack tuning fields"
```

---

### Task 6: ECG Flash Support

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgWaveGraphic.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgWidget.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgPanel.cs`

**Step 1: Add flash blend to wave graphic**

```csharp
[SerializeField] private Color damageFlashColor = Color.red;
[SerializeField, Min(0f)] private float damageFlashDuration = 0.15f;
private float damageFlashUntil;

public void TriggerDamageFlash()
{
    this.damageFlashUntil = Time.unscaledTime + this.damageFlashDuration;
    SetVerticesDirty();
}
```

And in `OnPopulateMesh`, blend:

```csharp
var baseColor = OperatorEcgMath.ComputeEcgColor(this.hpRatio);
float flashT = Mathf.Clamp01((this.damageFlashUntil - Time.unscaledTime) / Mathf.Max(0.001f, this.damageFlashDuration));
var waveColor = Color.Lerp(baseColor, this.damageFlashColor, flashT);
```

**Step 2: Add widget API**

```csharp
public void FlashDamage()
{
    EnsureReferences();
    this.waveGraphic?.TriggerDamageFlash();
}
```

**Step 3: Add panel mapping**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI.HUD
{
    public sealed class OperatorEcgPanel : MonoBehaviour
    {
        [SerializeField] private OperatorEcgWidget[] widgets = System.Array.Empty<OperatorEcgWidget>();

        public void FlashDamage(int operatorIndex)
        {
            if (operatorIndex < 0 || operatorIndex >= this.widgets.Length) return;
            var widget = this.widgets[operatorIndex];
            if (widget != null) widget.FlashDamage();
        }
    }
}
```

**Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgWaveGraphic.cs Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgWidget.cs Game/CrimsonDraft/Assets/Scripts/UI/HUD/OperatorEcgPanel.cs
git commit -m "feat: add ECG damage flash support"
```

---

### Task 7: Battlefield Feedback for Enemy Attacks

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Step 1: Extend interface**

```csharp
void PlayEnemyAttackFeedback(int enemySlotIndex);
void ShowOperatorDamage(int operatorSlotIndex, int damage);
```

**Step 2: Implement in BattlefieldView**

Add serialized fields:

```csharp
[SerializeField] private GameObject operatorDamageTextPrefab = null!;
[SerializeField] private Vector3 operatorDamageOffset = new(0f, 0.9f, 0f);
[SerializeField, Min(0.01f)] private float operatorDamageDuration = 0.6f;
[SerializeField, Min(0.01f)] private float enemyAttackShakeDuration = 0.2f;
[SerializeField] private Vector3 enemyAttackShakeStrength = new(0.15f, 0.15f, 0f);
```

Implement:

```csharp
public void PlayEnemyAttackFeedback(int enemySlotIndex)
{
    if (!this.enemyGoBySlot.TryGetValue(enemySlotIndex, out var go) || go == null) return;
    go.transform.DOKill();
    go.transform.DOShakePosition(this.enemyAttackShakeDuration, this.enemyAttackShakeStrength, vibrato: 20, randomness: 90f, fadeOut: true);
}

public void ShowOperatorDamage(int operatorSlotIndex, int damage)
{
    if (operatorSlotIndex < 0 || operatorSlotIndex >= this.playerSlotTransforms.Length) return;
    if (this.operatorDamageTextPrefab == null)
    {
        Debug.LogWarning("[BattlefieldView] operatorDamageTextPrefab is not assigned.");
        return;
    }

    var anchor = this.playerSlotTransforms[operatorSlotIndex];
    var go = Instantiate(this.operatorDamageTextPrefab, anchor.position + this.operatorDamageOffset, Quaternion.identity, this.transform);
    var text = go.GetComponentInChildren<TMPro.TMP_Text>();
    if (text == null)
    {
        Destroy(go);
        return;
    }

    text.text = $"-{Mathf.Max(0, damage)}";
    text.alpha = 1f;

    var target = go.transform.position + Vector3.up * 0.4f;
    go.transform.DOMove(target, this.operatorDamageDuration);
    text.DOFade(0f, this.operatorDamageDuration).OnComplete(() => { if (go != null) Destroy(go); });
}
```

**Step 3: Update test fake**

In `FakeBattlefieldView`, add empty methods:

```csharp
public void PlayEnemyAttackFeedback(int enemySlotIndex) { }
public void ShowOperatorDamage(int operatorSlotIndex, int damage) { }
```

**Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat: add operator damage feedback hooks"
```

---

### Task 8: EnemyAttackController + DI Wiring

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/EnemyAttackController.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs`

**Step 1: Implement controller**

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.UI.HUD;

namespace CrimsonDraft.Combat
{
    public sealed class EnemyAttackController : MonoBehaviour
    {
        private IEncounterContext encounterContext = null!;
        private EncounterDatabase encounterDatabase = null!;
        private IBattlefieldView battlefieldView = null!;
        private OperatorEcgPanel ecgPanel = null!;

        private EnemyAttackScheduler scheduler = null!;
        private CombatOperatorRoster roster = null!;
        private readonly IRandomSource random = new UnityRandomSource();
        private bool initialized;

        [Inject]
        public void Construct(IEncounterContext encounterContext,
            EncounterDatabase encounterDatabase,
            IBattlefieldView battlefieldView,
            OperatorEcgPanel ecgPanel)
        {
            this.encounterContext = encounterContext;
            this.encounterDatabase = encounterDatabase;
            this.battlefieldView = battlefieldView;
            this.ecgPanel = ecgPanel;
        }

        private void Start()
        {
            var encounterId = this.encounterContext.CurrentEncounterId;
            if (encounterId == null) return;
            var encounter = this.encounterDatabase.GetById(encounterId);
            if (encounter == null) return;

            var configs = BuildConfigs(encounter);
            this.scheduler = new EnemyAttackScheduler(this.random);
            this.scheduler.Initialize(configs, Time.time);

            this.roster = new CombatOperatorRoster();
            this.roster.Initialize(encounter.Operators.Length, defaultHp: 100);
            this.initialized = true;
        }

        private void Update()
        {
            if (!this.initialized) return;
            var alive = this.roster.GetAliveSlots();
            if (alive.Count == 0) return;

            if (this.scheduler.TryScheduleAttack(Time.time, alive, out var attack))
            {
                this.roster.ApplyDamage(attack.TargetSlot, attack.Damage);
                this.battlefieldView.PlayEnemyAttackFeedback(attack.AttackerSlot);
                this.battlefieldView.ShowOperatorDamage(attack.TargetSlot, attack.Damage);
                this.ecgPanel.FlashDamage(attack.TargetSlot);
            }
        }

        private static List<EnemyAttackConfig> BuildConfigs(EncounterData encounter)
        {
            var configs = new List<EnemyAttackConfig>();
            for (int i = 0; i < encounter.EnemySlots.Length; i++)
            {
                var enemy = encounter.EnemySlots[i];
                if (enemy == null) continue;
                configs.Add(new EnemyAttackConfig(
                    i,
                    enemy.AttackBaseSec,
                    enemy.AttackJitterSec,
                    enemy.AttackDurationSec,
                    enemy.AttackDamage));
            }
            return configs;
        }
    }
}
```

**Step 2: Register in CombatScope**

```csharp
builder.RegisterComponentInHierarchy<EnemyAttackController>().AsSelf();
builder.RegisterComponentInHierarchy<OperatorEcgPanel>().AsSelf();
```

**Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/EnemyAttackController.cs Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs
git commit -m "feat: wire enemy attack controller"
```

---

### Task 9: Final Verification

**Step 1: Run EditMode tests**

Run: `"C:\Program Files\Unity\Hub\Editor\<VERSION>\Editor\Unity.exe" -runTests -projectPath "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\.worktrees\enemy-attack\Game\CrimsonDraft" -testPlatform editmode -testResults "d:\Proyectos Unity\CrimsonDraft\CrimsonDraft\.worktrees\enemy-attack\TestResults.xml"`
Expected: PASS (all EditMode tests).

**Step 2: Commit final fixes (if any)**

```bash
git add -A
git commit -m "test: verify enemy attack system"
```
