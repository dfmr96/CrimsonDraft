#nullable enable

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
        public float NextFloat01() => Random.value;

        public int NextInt(int minInclusive, int maxExclusive) => Random.Range(minInclusive, maxExclusive);
    }

    public readonly struct EnemyAttackConfig
    {
        public int EnemySlotIndex { get; }
        public float AttackBaseSeconds { get; }
        public float AttackJitterSeconds { get; }
        public float AttackDurationSeconds { get; }
        public int AttackDamage { get; }

        public EnemyAttackConfig(
            int enemySlotIndex,
            float attackBaseSeconds,
            float attackJitterSeconds,
            float attackDurationSeconds,
            int attackDamage)
        {
            this.EnemySlotIndex = enemySlotIndex;
            this.AttackBaseSeconds = Mathf.Max(0f, attackBaseSeconds);
            this.AttackJitterSeconds = Mathf.Max(0f, attackJitterSeconds);
            this.AttackDurationSeconds = Mathf.Max(0f, attackDurationSeconds);
            this.AttackDamage = Mathf.Max(0, attackDamage);
        }
    }

    public readonly struct EnemyAttackResult
    {
        public int AttackerSlotIndex { get; }
        public int TargetOperatorSlotIndex { get; }
        public int Damage { get; }
        public float LockUntilTime { get; }
        public float NextAttackTime { get; }

        public EnemyAttackResult(
            int attackerSlotIndex,
            int targetOperatorSlotIndex,
            int damage,
            float lockUntilTime,
            float nextAttackTime)
        {
            this.AttackerSlotIndex = attackerSlotIndex;
            this.TargetOperatorSlotIndex = targetOperatorSlotIndex;
            this.Damage = damage;
            this.LockUntilTime = lockUntilTime;
            this.NextAttackTime = nextAttackTime;
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
        private readonly List<EnemyAttackState> tiedCandidates = new();
        private float attackLockUntil;

        public EnemyAttackScheduler(IRandomSource random)
        {
            this.random = random;
        }

        public void Initialize(IReadOnlyList<EnemyAttackConfig> configs, float now)
        {
            this.states.Clear();
            this.attackLockUntil = 0f;

            for (int i = 0; i < configs.Count; i++)
            {
                EnemyAttackConfig config = configs[i];
                this.states.Add(new EnemyAttackState
                {
                    Config = config,
                    NextAttackTime = now + ComputeCooldown(config),
                    IsDead = false,
                });
            }
        }

        public void MarkDead(int enemySlotIndex)
        {
            for (int i = 0; i < this.states.Count; i++)
            {
                EnemyAttackState state = this.states[i];
                if (state.Config.EnemySlotIndex == enemySlotIndex)
                    state.IsDead = true;
            }
        }

        public bool TryScheduleAttack(
            float now,
            IReadOnlyList<int> aliveOperatorSlots,
            out EnemyAttackResult attack)
        {
            attack = default;

            if (now < this.attackLockUntil)
                return false;

            if (aliveOperatorSlots == null || aliveOperatorSlots.Count == 0)
                return false;

            EnemyAttackState? bestState = null;
            this.tiedCandidates.Clear();

            for (int i = 0; i < this.states.Count; i++)
            {
                EnemyAttackState state = this.states[i];
                if (state.IsDead)
                    continue;
                if (state.NextAttackTime > now)
                    continue;

                if (bestState == null || state.NextAttackTime < bestState.NextAttackTime)
                {
                    bestState = state;
                    this.tiedCandidates.Clear();
                    this.tiedCandidates.Add(state);
                    continue;
                }

                if (bestState != null && Mathf.Approximately(state.NextAttackTime, bestState.NextAttackTime))
                    this.tiedCandidates.Add(state);
            }

            if (bestState == null)
                return false;

            EnemyAttackState selectedAttacker = this.tiedCandidates.Count == 1
                ? this.tiedCandidates[0]
                : this.tiedCandidates[this.random.NextInt(0, this.tiedCandidates.Count)];

            int targetIndex = this.random.NextInt(0, aliveOperatorSlots.Count);
            int targetSlot = aliveOperatorSlots[targetIndex];

            float nextAttackTime = now + ComputeCooldown(selectedAttacker.Config);
            float lockUntil = now + selectedAttacker.Config.AttackDurationSeconds;
            selectedAttacker.NextAttackTime = nextAttackTime;
            this.attackLockUntil = lockUntil;

            attack = new EnemyAttackResult(
                selectedAttacker.Config.EnemySlotIndex,
                targetSlot,
                selectedAttacker.Config.AttackDamage,
                lockUntil,
                nextAttackTime);
            return true;
        }

        private float ComputeCooldown(EnemyAttackConfig config)
        {
            if (config.AttackJitterSeconds <= 0f)
                return config.AttackBaseSeconds;

            float jitter = Mathf.Lerp(-config.AttackJitterSeconds, config.AttackJitterSeconds, this.random.NextFloat01());
            return Mathf.Max(0f, config.AttackBaseSeconds + jitter);
        }
    }
}
