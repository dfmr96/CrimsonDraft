#nullable enable

using System.Collections.Generic;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat
{
    public sealed class CombatOrchestrator : MonoBehaviour, ICombatOrchestrator, IInitializable
    {
        private ATBSystem                                    atbSystem          = null!;
        private CombatActionQueue                            actionQueue        = null!;
        private IPublisher<ShootConfigurationRequestedEvent> shootPublisher     = null!;
        private IPublisher<CombatEndedEvent>                 combatEndPublisher = null!;
        private IBattlefieldView                             battlefieldView    = null!;
        private IOperatorRoster                              roster             = null!;
        private IEncounterContext                            encounterContext    = null!;
        private EncounterDatabase                            encounterDatabase   = null!;
        private IInventoryService                            inventory          = null!;
        private ICombatActionMenuView                        menuView           = null!;

        [SerializeField] private float operatorActionDurationSec = 0.5f;
        [SerializeField] private float defendDurationSec         = 0.3f;
        [SerializeField] private float defaultEnemyAttackDurSec  = 1.2f;
        [SerializeField] private float atbGaugeDivisor           = 100f;

        private readonly IRandomSource  random               = new UnityRandomSource();
        private readonly HashSet<int>   knownAliveEnemySlots = new();
        private readonly HashSet<int>   syncAliveSet         = new();
        private readonly List<int>      syncDeadBuf          = new();

        private float          animationLockUntil;
        private float          animationLockDuration;
        private bool           shootConfigurationInProgress;
        private bool           enemyAttackInProgress;
        private bool           waitModeActive;
        private bool           initialized;
        private bool           combatEnded;
        private EncounterData? encounter;
        private IOperatorEcgFeedback? ecgFeedback;

        [Inject]
        [UnityEngine.Scripting.Preserve]
        public void Construct(
            ATBSystem                                    atbSystem,
            CombatActionQueue                            actionQueue,
            IPublisher<ShootConfigurationRequestedEvent> shootPublisher,
            IPublisher<CombatEndedEvent>                 combatEndPublisher,
            IBattlefieldView                             battlefieldView,
            IOperatorRoster                              roster,
            IEncounterContext                            encounterContext,
            EncounterDatabase                            encounterDatabase,
            IInventoryService                            inventory,
            ICombatActionMenuView                        menuView)
        {
            this.atbSystem          = atbSystem;
            this.actionQueue        = actionQueue;
            this.shootPublisher     = shootPublisher;
            this.combatEndPublisher = combatEndPublisher;
            this.battlefieldView    = battlefieldView;
            this.roster             = roster;
            this.encounterContext   = encounterContext;
            this.encounterDatabase  = encounterDatabase;
            this.inventory          = inventory;
            this.menuView           = menuView;
        }

        void IInitializable.Initialize()
        {
            string? encounterId = this.encounterContext.CurrentEncounterId;
            if (encounterId == null) return;

            this.encounter = this.encounterDatabase.GetById(encounterId);
            if (this.encounter == null) return;

            var configs = BuildATBConfigs(this.encounter, this.roster, this.atbGaugeDivisor);
            this.atbSystem.Initialize(configs);
            for (int i = 0; i < this.roster.Count; i++)
                this.menuView.SetOperatorDimmed(i, true);

            this.knownAliveEnemySlots.Clear();
            for (int i = 0; i < this.encounter.EnemySlots.Length; i++)
            {
                if (this.encounter.EnemySlots[i] != null)
                    this.knownAliveEnemySlots.Add(i);
            }

            this.ecgFeedback = ResolveEcgFeedback();
            SyncAllEcgStates();
            this.initialized = true;
        }

        private void Update()
        {
            if (!this.initialized) return;

            SyncDeadEnemies();
            this.atbSystem.Tick(Time.deltaTime, this.waitModeActive);
            NotifyReadyOperators();
            EnqueueReadyEnemyAttacks();
            ProcessQueueHead();
        }

        private void LateUpdate()
        {
            if (!this.initialized) return;
            SyncAllEcgStates();
        }

        public void EnqueueAction(PendingAction action)
        {
            this.actionQueue.Enqueue(action);
            this.atbSystem.ResetActor(action.SlotIndex, ATBActorKind.Operator);
            this.menuView.SetOperatorDimmed(action.SlotIndex, true);
        }

        public void SetWaitMode(bool paused) => this.waitModeActive = paused;

        public bool IsOperatorReady(int slotIndex)
        {
            ATBActorState? actor = this.atbSystem.GetActor(slotIndex, ATBActorKind.Operator);
            return actor != null && actor.IsReady && actor.IsAwaitingCommand;
        }

        public void NotifyShootCompleted()
        {
            if (!this.actionQueue.HasPending) return;
            if (this.actionQueue.Peek().Type != PendingActionType.Shoot) return;
            this.actionQueue.Dequeue();
            this.shootConfigurationInProgress = false;
            SetAnimationLock(this.operatorActionDurationSec);
        }

        internal float AnimationLockRemaining => UnityEngine.Mathf.Max(0f, this.animationLockUntil - Time.time);
        internal float AnimationLockDuration  => this.animationLockDuration;

        private void SetAnimationLock(float duration)
        {
            this.animationLockUntil    = Time.time + duration;
            this.animationLockDuration = duration;
        }

        private void NotifyReadyOperators()
        {
            for (int i = 0; i < this.roster.Count; i++)
            {
                ATBActorState? actor = this.atbSystem.GetActor(i, ATBActorKind.Operator);
                if (actor == null || actor.IsDead || actor.IsAwaitingCommand) continue;
                if (actor.IsReady)
                {
                    actor.IsAwaitingCommand = true;
                    this.menuView.SetOperatorDimmed(i, false);
                }
            }
        }

        private void EnqueueReadyEnemyAttacks()
        {
            if (this.encounter == null) return;
            IReadOnlyList<int> aliveOperatorSlots = this.roster.GetAliveSlots();
            if (aliveOperatorSlots.Count == 0) return;

            for (int i = 0; i < this.encounter.EnemySlots.Length; i++)
            {
                EnemyData? data = this.encounter.EnemySlots[i];
                if (data == null) continue;

                ATBActorState? actor = this.atbSystem.GetActor(i, ATBActorKind.Enemy);
                if (actor == null || actor.IsDead || !actor.IsReady) continue;

                int targetIndex = this.random.NextInt(0, aliveOperatorSlots.Count);
                int targetSlot  = aliveOperatorSlots[targetIndex];

                this.actionQueue.Enqueue(PendingAction.EnemyAttack(i, targetSlot, data.AttackDamage));

                float nextSec = Mathf.Max(0.1f, data.AttackBaseSec);
                this.atbSystem.ResetActor(i, ATBActorKind.Enemy);
                this.atbSystem.FreezeActor(i, ATBActorKind.Enemy);
                this.atbSystem.UpdateActorGaugeRate(i, ATBActorKind.Enemy, 1f / nextSec);
            }
        }

        private bool IsActorDead(PendingAction action)
        {
            if (action.Type == PendingActionType.EnemyAttack)
            {
                ATBActorState? actor = this.atbSystem.GetActor(action.SlotIndex, ATBActorKind.Enemy);
                return actor == null || actor.IsDead;
            }
            return action.SlotIndex >= this.roster.Count || !this.roster[action.SlotIndex].IsAlive;
        }

        private void ProcessQueueHead()
        {
            if (!this.actionQueue.HasPending) return;

            PendingAction head = this.actionQueue.Peek();

            if (head.Type == PendingActionType.Shoot)
            {
                if (!this.shootConfigurationInProgress)
                {
                    if (IsActorDead(head)) { this.actionQueue.Dequeue(); return; }
                    this.shootConfigurationInProgress = true;
                    this.shootPublisher.Publish(new ShootConfigurationRequestedEvent(head.SlotIndex));
                }
                return;
            }

            if (head.Type == PendingActionType.EnemyAttack)
            {
                if (!this.enemyAttackInProgress)
                {
                    if (IsActorDead(head)) { this.actionQueue.Dequeue(); return; }
                    if (Time.time < this.animationLockUntil) return;
                    this.enemyAttackInProgress = true;
                    ApplyEnemyAttack(head);
                }
                else if (Time.time >= this.animationLockUntil)
                {
                    this.atbSystem.UnfreezeActor(head.SlotIndex, ATBActorKind.Enemy);
                    this.actionQueue.Dequeue();
                    this.enemyAttackInProgress = false;
                }
                return;
            }

            if (IsActorDead(head)) { this.actionQueue.Dequeue(); return; }
            if (Time.time < this.animationLockUntil) return;
            this.actionQueue.Dequeue();

            switch (head.Type)
            {
                case PendingActionType.Reload:
                    this.inventory.ReloadOperator(head.AmmoBoxIndex, head.SlotIndex);
                    var weapon = this.roster.Count > head.SlotIndex ? this.roster[head.SlotIndex].ActiveWeapon : null;
                    this.menuView.SetOperatorAmmo(head.SlotIndex, weapon?.CurrentAmmo ?? 0, weapon?.MaxAmmo ?? 0);
                    SetAnimationLock(this.operatorActionDurationSec);
                    break;

                case PendingActionType.UseItem:
                    SetAnimationLock(this.operatorActionDurationSec);
                    break;

                case PendingActionType.Defend:
                    SetAnimationLock(this.defendDurationSec);
                    break;
            }
        }

        private void ApplyEnemyAttack(PendingAction action)
        {
            if (action.TargetOperatorSlot >= this.roster.Count) return;

            this.roster[action.TargetOperatorSlot].ApplyDamage(action.Damage);
            this.battlefieldView.PlayEnemyAttackFeedback(action.SlotIndex);
            this.battlefieldView.ShowOperatorDamage(action.TargetOperatorSlot, action.Damage);
            this.ecgFeedback?.FlashOperatorDamage(action.TargetOperatorSlot);
            this.ecgFeedback?.SetOperatorHealthState(
                action.TargetOperatorSlot,
                this.roster[action.TargetOperatorSlot].HpRatio,
                this.roster[action.TargetOperatorSlot].IsAlive);

            SetAnimationLock(this.defaultEnemyAttackDurSec);
        }

        private void SyncDeadEnemies()
        {
            int[] aliveEnemySlots = this.battlefieldView.GetOccupiedEnemySlots();

            this.syncAliveSet.Clear();
            for (int i = 0; i < aliveEnemySlots.Length; i++)
                this.syncAliveSet.Add(aliveEnemySlots[i]);

            this.syncDeadBuf.Clear();
            foreach (int slot in this.knownAliveEnemySlots)
            {
                if (!this.syncAliveSet.Contains(slot))
                    this.syncDeadBuf.Add(slot);
            }

            for (int i = 0; i < this.syncDeadBuf.Count; i++)
            {
                this.atbSystem.MarkDead(this.syncDeadBuf[i], ATBActorKind.Enemy);
                this.knownAliveEnemySlots.Remove(this.syncDeadBuf[i]);
            }

            if (this.syncDeadBuf.Count > 0 && this.knownAliveEnemySlots.Count == 0 && !this.combatEnded)
            {
                this.combatEnded = true;
                this.combatEndPublisher.Publish(new CombatEndedEvent { Victory = true });
            }
        }

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
                float gps = data.AttackBaseSec > 0f ? 1f / data.AttackBaseSec : 1f;
                configs.Add(new ATBActorConfig(i, ATBActorKind.Enemy, gps));
            }

            return configs;
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

        private void SyncAllEcgStates()
        {
            if (this.ecgFeedback == null) return;
            for (int i = 0; i < this.roster.Count; i++)
            {
                bool isPresent = this.roster[i].IsPresent;
                this.ecgFeedback.SetOperatorHealthState(
                    i,
                    isPresent ? this.roster[i].HpRatio : 0f,
                    isPresent && this.roster[i].IsAlive);
            }
        }
    }
}
