#nullable enable

using System.Collections.Generic;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using CrimsonDraft.Combat;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    // Focus-fire ("synced shot") freezes every marked operator's ATB gauge until whoever is
    // left unmarked selects Shoot to trigger the group. That role has no fallback: if the
    // last unmarked operator (or enough marked ones) dies first, the survivors would
    // otherwise stay frozen forever with no one able to send the trigger command -- a hard
    // lock. This lives at the CombatOrchestrator/ATB level, not the UI state machine, so it
    // has to be exercised against the real orchestrator rather than CombatMenuControllerTests'
    // FakeOrchestrator (whose IsOperatorReady() is hardcoded true and can't reproduce it).
    public sealed class CombatOrchestratorTests
    {
        private FakeOperatorRoster       roster           = null!;
        private FakeBattlefieldView      battlefield      = null!;
        private FakeCombatActionMenuView menuView         = null!;
        private FakeInventoryService     inventory        = null!;
        private FakeEncounterContext     encounterContext = null!;
        private FakePublisher<ShootConfigurationRequestedEvent>     shootPublisher               = null!;
        private FakePublisher<MeleeConfigurationRequestedEvent>     meleePublisher               = null!;
        private FakePublisher<CombatEndedEvent>                     combatEndPublisher           = null!;
        private ATBSystem          atbSystem   = null!;
        private CombatActionQueue  actionQueue = null!;
        private CombatOrchestrator orchestrator = null!;

        [SetUp]
        public void SetUp()
        {
            this.roster           = new FakeOperatorRoster();
            this.battlefield      = new FakeBattlefieldView();
            this.menuView         = new FakeCombatActionMenuView();
            this.inventory        = new FakeInventoryService();
            this.encounterContext = new FakeEncounterContext();
            this.shootPublisher              = new FakePublisher<ShootConfigurationRequestedEvent>();
            this.meleePublisher              = new FakePublisher<MeleeConfigurationRequestedEvent>();
            this.combatEndPublisher          = new FakePublisher<CombatEndedEvent>();
            this.atbSystem   = new ATBSystem();
            this.actionQueue = new CombatActionQueue();

            this.orchestrator = new GameObject("CombatOrchestrator").AddComponent<CombatOrchestrator>();
            this.orchestrator.Construct(
                this.atbSystem, this.actionQueue,
                this.shootPublisher, this.meleePublisher, this.combatEndPublisher,
                this.battlefield, this.roster, this.encounterContext, this.inventory, this.menuView);
            ((IInitializable)this.orchestrator).Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (this.orchestrator != null)
                Object.DestroyImmediate(this.orchestrator.gameObject);
        }

        private void Tick()
        {
            var update = typeof(CombatOrchestrator).GetMethod("Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(update);
            update!.Invoke(this.orchestrator, null);
        }

        private void SetRandom(FakeRandomSource fake)
        {
            var field = typeof(CombatOrchestrator).GetField("random",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(field);
            field!.SetValue(this.orchestrator, fake);
        }

        private int InvokeSelectEnemyTargetSlot(IReadOnlyList<int> aliveOperatorSlots)
        {
            var method = typeof(CombatOrchestrator).GetMethod("SelectEnemyTargetSlot",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);
            return (int)method!.Invoke(this.orchestrator, new object[] { aliveOperatorSlots })!;
        }

        // ── Enemy targeting ──

        [Test]
        public void SelectEnemyTargetSlot_noMercy_usesUniformRandomInt()
        {
            var fakeRandom = new FakeRandomSource { NextIntReturnValue = 1 };
            SetRandom(fakeRandom);

            int result = InvokeSelectEnemyTargetSlot(new List<int> { 0, 1, 2 });

            Assert.AreEqual(1, fakeRandom.NextIntCallCount);
            Assert.AreEqual(1, result);
        }

        // ── Enemy targeting bias against an operator in Mercy ──

        [Test]
        public void SelectEnemyTargetSlot_mercyOperator_weightedDown()
        {
            this.roster[0].ApplyDamage(100); // 0 HP, still alive -> Mercy
            Assert.IsTrue(this.roster[0].IsMercy);

            SetRandom(new FakeRandomSource { NextFloat01Value = 0.2f });

            int result = InvokeSelectEnemyTargetSlot(new List<int> { 0, 1, 2 });

            // Weights: slot0 (Mercy) = 0.35 (default mercyTargetWeight), slot1 = 1, slot2 = 1
            // -> total = 2.35. roll = 0.2 * 2.35 = 0.47, past slot0's narrowed [0, 0.35) bucket
            // -- a roll that would have landed on slot0 under a plain uniform 1/3 split now
            // falls through to slot1 instead.
            Assert.AreEqual(1, result);
        }

        [Test]
        public void ReviveMercyOperatorsOnCombatEnd_healsToOneHp_insteadOfKilling()
        {
            this.roster[0].ApplyDamage(100); // 0 HP, still alive -> Mercy
            Assert.IsTrue(this.roster[0].IsMercy);

            var method = typeof(CombatOrchestrator).GetMethod("ReviveMercyOperatorsOnCombatEnd",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);
            method!.Invoke(this.orchestrator, null);

            Assert.IsTrue(this.roster[0].IsAlive);
            Assert.IsFalse(this.roster[0].IsMercy);
            Assert.AreEqual(1, this.roster[0].Hp);
        }

        // ── Fakes ──────────────────────────────────────────────────────

        private sealed class FakeRandomSource : IRandomSource
        {
            public float NextFloat01Value    { get; set; }
            public int   NextIntReturnValue  { get; set; }
            public int   NextIntCallCount    { get; private set; }

            public float NextFloat01() => this.NextFloat01Value;

            public int NextInt(int minInclusive, int maxExclusive)
            {
                this.NextIntCallCount++;
                return this.NextIntReturnValue;
            }
        }

        private sealed class FakePublisher<T> : IPublisher<T> where T : struct
        {
            public int  PublishCount { get; private set; }
            public T?   LastMessage  { get; private set; }
            public void Publish(T message)
            {
                this.PublishCount++;
                this.LastMessage = message;
            }
        }

        private sealed class FakeEncounterContext : IEncounterContext
        {
            public string?          CurrentEncounterId => "test-encounter";
            public ScriptableObject? EncounterAsset     { get; } = ScriptableObject.CreateInstance<EncounterData>();
            public bool              OperatorsStartFull => false;
        }

        private sealed class FakeOperatorRoster : IOperatorRoster
        {
            private readonly OperatorRuntime[] slots;
            private readonly List<int> scratchAlive = new();
            public bool IsInitialized { get; private set; } = true;

            internal FakeOperatorRoster(int slotCount = 3, int maxHp = 100)
            {
                this.slots = new OperatorRuntime[slotCount];
                for (int i = 0; i < slotCount; i++)
                    this.slots[i] = new OperatorRuntime(i, null, isPresent: true, maxHp);
            }

            public int Count => this.slots.Length;
            public OperatorRuntime this[int slotIndex] => this.slots[slotIndex];
            public void EnsureInitialized() => this.IsInitialized = true;

            public IReadOnlyList<int> GetAliveSlots()
            {
                this.scratchAlive.Clear();
                for (int i = 0; i < this.slots.Length; i++)
                    if (this.slots[i].IsAlive) this.scratchAlive.Add(i);
                return this.scratchAlive;
            }

            public int[] GetHpSnapshot() => System.Array.Empty<int>();
            public void RestoreHp(int[] snapshot) { }
        }

        private sealed class FakeBattlefieldView : IBattlefieldView
        {
            public void Populate(EncounterData encounter) { }
            public void SetOperatorIndicator(int slotIndex) { }
            public void DimOperatorIndicator() { }
            public void PlayEnemyAttackFeedback(int enemySlotIndex, System.Action onAttackImpact) => onAttackImpact?.Invoke();
            public bool TryGetResolvedEnemyAttackDuration(int enemySlotIndex, out float durationSec)
            {
                durationSec = 0f;
                return false;
            }
            public void ShowOperatorDamage(int operatorSlotIndex, int damage) { }
            public void PlayOperatorHitFx(int operatorSlotIndex) { }
            public void PlayOperatorFlinch(int operatorSlotIndex) { }
            public void PlayOperatorReload(int operatorSlotIndex) { }
            public void PlayOperatorDeath(int operatorSlotIndex) { }
            public bool HasOperatorDeathSettled(int operatorSlotIndex) => true;
            public void SetEnemyTargetIndicator(int slotIndex) { }
            public void HideEnemyTargetIndicator() { }
            public int[] GetOccupiedEnemySlots() => System.Array.Empty<int>();
            public AimHitMaskProfile? GetEnemyHitMaskProfile(int slotIndex) => null;
            public EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int hpDamage, int poiseDamage, int decapitationPellets) =>
                new EnemyDamageResult(slotIndex, 0, 0, false, false);
            public void TriggerEnemyStagger(int slotIndex) { }
            public void RecoverEnemyStagger(int slotIndex) { }
            public void FinalizeEnemyDeath(int slotIndex) { }
            public int[] NotifyActionDequeued() => System.Array.Empty<int>();
            public bool IsEnemyStaggered(int slotIndex) => false;
            public bool IsEnemyDead(int slotIndex) => false;
            public bool HasAliveEnemies() => false;
            public UniTask PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots, bool isMelee = false) =>
                UniTask.CompletedTask;
#if UNITY_EDITOR || DEBUG_COMBAT
            public (int Current, int Max, bool IsDead, int Poise, bool IsStaggered) GetEnemyHpDebug(int slotIndex) =>
                (0, 0, true, 0, false);
#endif
        }

        private sealed class FakeCombatActionMenuView : ICombatActionMenuView
        {
            public event System.Action<int>? OnOperatorSelected;
            public event System.Action<int>? OnOperatorFocused;
            public void FocusOperator(int index) { }
            public void ClearFocus() { }
            public void ReleaseOperatorFocus(int index) { }
            public void PlayActionFeedback(int index) { }
            public RectTransform GetOperatorAnchor(int index)        => new GameObject().AddComponent<RectTransform>();
            public RectTransform GetOperatorRect(int index)          => new GameObject().AddComponent<RectTransform>();
            public RectTransform GetOperatorOverviewRect(int index)  => new GameObject().AddComponent<RectTransform>();
            public void MoveSelectorTo(RectTransform anchor) { }
            public void SetOperatorAmmo(int index, int currentAmmo, int maxAmmo) { }
            public void SetOperatorHealth(int index, float hpRatio, bool isAlive) { }
            public void SetOperatorName(int index, string name) { }
            public void PlayOperatorDamageShake(int index) { }
            public void PlayOperatorDamageGlitch(int index) { }
            public void SetOperatorActionPending(int index, bool pending) { }
            public readonly Dictionary<int, int> TurnOrderByIndex = new();
            public void SetOperatorTurnOrder(int index, int position) => this.TurnOrderByIndex[index] = position;
            public void SetOperatorGauge(int index, float gauge01) { }
            public void ExpandOperatorBorder(int index, bool expanded, System.Action? onComplete = null) => onComplete?.Invoke();
            public void SetOperatorWeapon(int index, WeaponItem? weapon) { }
            public void SetDimmed(bool dimmed) { }
            public readonly Dictionary<int, bool> DimmedByIndex = new();
            public void SetOperatorDimmed(int index, bool dimmed) => this.DimmedByIndex[index] = dimmed;
            public bool IsOperatorFocused(int index) => false;
        }

    }
}
