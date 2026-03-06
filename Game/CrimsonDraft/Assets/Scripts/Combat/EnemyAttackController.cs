#nullable enable

using System.Collections.Generic;
using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat
{
    public sealed class EnemyAttackController : MonoBehaviour
    {
        private IEncounterContext   encounterContext  = null!;
        private EncounterDatabase   encounterDatabase = null!;
        private IBattlefieldView    battlefieldView   = null!;
        private IOperatorRoster     roster            = null!;

        private EnemyAttackScheduler    scheduler = null!;
        private readonly IRandomSource  random    = new UnityRandomSource();
        private readonly HashSet<int>   knownAliveEnemySlots = new();
        private IOperatorEcgFeedback?   ecgFeedback;
        private bool                    initialized;

        [Inject]
        public void Construct(
            IEncounterContext   encounterContext,
            EncounterDatabase   encounterDatabase,
            IBattlefieldView    battlefieldView,
            IOperatorRoster     roster)
        {
            this.encounterContext  = encounterContext;
            this.encounterDatabase = encounterDatabase;
            this.battlefieldView   = battlefieldView;
            this.roster            = roster;
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

            this.ecgFeedback = ResolveEcgFeedback();
            SyncAllOperatorEcgStates(this.roster.Count);
            this.initialized = true;
        }

        private void Update()
        {
            if (!this.initialized)
                return;

            SyncDeadEnemiesFromBattlefield();

            IReadOnlyList<int> aliveSlots = this.roster.GetAliveSlots();
            if (aliveSlots.Count == 0)
                return;

            var validAliveOperators = new List<int>(aliveSlots.Count);
            for (int i = 0; i < aliveSlots.Count; i++)
                validAliveOperators.Add(aliveSlots[i]);

            if (!this.scheduler.TryScheduleAttack(Time.time, validAliveOperators, out var attack))
                return;

            this.roster[attack.TargetOperatorSlotIndex].ApplyDamage(attack.Damage);
            this.battlefieldView.PlayEnemyAttackFeedback(attack.AttackerSlotIndex);
            this.battlefieldView.ShowOperatorDamage(attack.TargetOperatorSlotIndex, attack.Damage);
            this.ecgFeedback?.FlashOperatorDamage(attack.TargetOperatorSlotIndex);
            this.ecgFeedback?.SetOperatorHealthState(
                attack.TargetOperatorSlotIndex,
                this.roster[attack.TargetOperatorSlotIndex].HpRatio,
                this.roster[attack.TargetOperatorSlotIndex].IsAlive);
        }

        private void LateUpdate()
        {
            if (!this.initialized)
                return;

            SyncAllOperatorEcgStates(this.roster.Count);
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
                bool isPresent = i < this.roster.Count && this.roster[i].IsPresent;
                this.ecgFeedback.SetOperatorHealthState(
                    i,
                    isPresent ? this.roster[i].HpRatio : 0f,
                    isPresent && this.roster[i].IsAlive);
            }
        }
    }
}
