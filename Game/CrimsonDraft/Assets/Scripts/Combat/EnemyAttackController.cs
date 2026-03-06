#nullable enable

using System.Collections.Generic;
using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure.Scenes;

namespace CrimsonDraft.Combat
{
    public sealed class EnemyAttackController : MonoBehaviour
    {
        [SerializeField, Min(1)] private int defaultOperatorHp = 100;

        private IEncounterContext encounterContext = null!;
        private EncounterDatabase encounterDatabase = null!;
        private IBattlefieldView battlefieldView = null!;
        private CombatOperatorHealthBridge healthBridge = null!;

        private EnemyAttackScheduler scheduler = null!;
        private readonly IRandomSource random = new UnityRandomSource();
        private readonly HashSet<int> knownAliveEnemySlots = new();
        private bool[] hasOperatorSlot = System.Array.Empty<bool>();
        private IOperatorEcgFeedback? ecgFeedback;
        private bool initialized;

        [Inject]
        public void Construct(
            IEncounterContext encounterContext,
            EncounterDatabase encounterDatabase,
            IBattlefieldView battlefieldView,
            CombatOperatorHealthBridge healthBridge)
        {
            this.encounterContext = encounterContext;
            this.encounterDatabase = encounterDatabase;
            this.battlefieldView = battlefieldView;
            this.healthBridge = healthBridge;
        }

        private void Start()
        {
            string? encounterId = this.encounterContext.CurrentEncounterId;
            if (encounterId == null)
                return;

            EncounterData? encounter = this.encounterDatabase.GetById(encounterId);
            if (encounter == null)
                return;

            var configs = BuildAttackConfigs(encounter);
            this.scheduler = new EnemyAttackScheduler(this.random);
            this.scheduler.Initialize(configs, Time.time);

            this.knownAliveEnemySlots.Clear();
            for (int i = 0; i < configs.Count; i++)
                this.knownAliveEnemySlots.Add(configs[i].EnemySlotIndex);

            this.healthBridge.Initialize(encounter.Operators.Length, this.defaultOperatorHp);
            this.hasOperatorSlot = new bool[encounter.Operators.Length];
            for (int i = 0; i < encounter.Operators.Length; i++)
            {
                bool hasOperator = encounter.Operators[i] != null;
                this.hasOperatorSlot[i] = hasOperator;
                if (!hasOperator)
                    this.healthBridge.ApplyDamage(i, this.defaultOperatorHp);
            }

            this.ecgFeedback = ResolveEcgFeedback();
            SyncAllOperatorEcgStates(encounter.Operators.Length);
            this.initialized = true;
        }

        private void Update()
        {
            if (!this.initialized)
                return;

            SyncDeadEnemiesFromBattlefield();

            IReadOnlyList<int> aliveOperators = this.healthBridge.GetAliveSlots();
            var validAliveOperators = new List<int>(aliveOperators.Count);
            for (int i = 0; i < aliveOperators.Count; i++)
            {
                int slot = aliveOperators[i];
                if (slot >= 0 && slot < this.hasOperatorSlot.Length && this.hasOperatorSlot[slot])
                    validAliveOperators.Add(slot);
            }
            if (validAliveOperators.Count == 0)
                return;

            if (!this.scheduler.TryScheduleAttack(Time.time, validAliveOperators, out var attack))
                return;

            this.healthBridge.ApplyDamage(attack.TargetOperatorSlotIndex, attack.Damage);
            this.battlefieldView.PlayEnemyAttackFeedback(attack.AttackerSlotIndex);
            this.battlefieldView.ShowOperatorDamage(attack.TargetOperatorSlotIndex, attack.Damage);
            this.ecgFeedback?.FlashOperatorDamage(attack.TargetOperatorSlotIndex);
            this.ecgFeedback?.SetOperatorHealthState(
                attack.TargetOperatorSlotIndex,
                this.healthBridge.GetHpRatio(attack.TargetOperatorSlotIndex),
                this.healthBridge.IsAlive(attack.TargetOperatorSlotIndex));
        }

        private void LateUpdate()
        {
            if (!this.initialized)
                return;

            SyncAllOperatorEcgStates(this.hasOperatorSlot.Length);
        }

        private static List<EnemyAttackConfig> BuildAttackConfigs(EncounterData encounter)
        {
            var configs = new List<EnemyAttackConfig>(encounter.EnemySlots.Length);
            for (int i = 0; i < encounter.EnemySlots.Length; i++)
            {
                EnemyData? enemy = encounter.EnemySlots[i];
                if (enemy == null)
                    continue;

                configs.Add(new EnemyAttackConfig(
                    i,
                    enemy.AttackBaseSec,
                    enemy.AttackJitterSec,
                    enemy.AttackDurationSec,
                    enemy.AttackDamage));
            }

            return configs;
        }

        private void SyncDeadEnemiesFromBattlefield()
        {
            int[] aliveEnemySlots = this.battlefieldView.GetOccupiedEnemySlots();
            var aliveSet = new HashSet<int>(aliveEnemySlots);

            if (this.knownAliveEnemySlots.Count == 0)
                return;

            var deadCandidates = new List<int>();
            foreach (int slot in this.knownAliveEnemySlots)
            {
                if (!aliveSet.Contains(slot))
                    deadCandidates.Add(slot);
            }

            for (int i = 0; i < deadCandidates.Count; i++)
            {
                int deadSlot = deadCandidates[i];
                this.scheduler.MarkDead(deadSlot);
                this.knownAliveEnemySlots.Remove(deadSlot);
            }
        }

        private IOperatorEcgFeedback? ResolveEcgFeedback()
        {
            var behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IOperatorEcgFeedback feedback)
                    return feedback;
            }

            return null;
        }

        private void SyncAllOperatorEcgStates(int operatorCount)
        {
            if (this.ecgFeedback == null)
                return;

            for (int i = 0; i < operatorCount; i++)
            {
                bool hasOperator = i < this.hasOperatorSlot.Length && this.hasOperatorSlot[i];
                this.ecgFeedback.SetOperatorHealthState(
                    i,
                    hasOperator ? this.healthBridge.GetHpRatio(i) : 0f,
                    hasOperator && this.healthBridge.IsAlive(i));
            }
        }
    }
}
