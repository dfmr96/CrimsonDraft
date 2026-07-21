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
        private IPublisher<FocusFireConfigurationRequestedEvent> focusFirePublisher = null!;
        private IPublisher<CombatEndedEvent>                 combatEndPublisher = null!;
        private IBattlefieldView                             battlefieldView    = null!;
        private IOperatorRoster                              roster             = null!;
        private IEncounterContext                            encounterContext    = null!;
        private IInventoryService                            inventory          = null!;
        private ICombatActionMenuView                        menuView           = null!;

        [SerializeField] private float operatorActionDurationSec      = 0.5f;
        [SerializeField] private float defaultEnemyAttackDurSec       = 1.2f;
        [SerializeField] private float atbGaugeDivisor                = 100f;
        [SerializeField] private bool  freezeOperatorWhenActionQueued = false;

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
            IPublisher<FocusFireConfigurationRequestedEvent> focusFirePublisher,
            IPublisher<CombatEndedEvent>                 combatEndPublisher,
            IBattlefieldView                             battlefieldView,
            IOperatorRoster                              roster,
            IEncounterContext                            encounterContext,
            IInventoryService                            inventory,
            ICombatActionMenuView                        menuView)
        {
            this.atbSystem          = atbSystem;
            this.actionQueue        = actionQueue;
            this.shootPublisher     = shootPublisher;
            this.focusFirePublisher = focusFirePublisher;
            this.combatEndPublisher = combatEndPublisher;
            this.battlefieldView    = battlefieldView;
            this.roster             = roster;
            this.encounterContext   = encounterContext;
            this.inventory          = inventory;
            this.menuView           = menuView;
        }

        void IInitializable.Initialize()
        {
            this.encounter = this.encounterContext.EncounterAsset as EncounterData;
            if (this.encounter == null) return;

            var configs = BuildATBConfigs(this.encounter, this.roster, this.atbGaugeDivisor);
            this.atbSystem.Initialize(configs);

            if (this.encounterContext.OperatorsStartFull)
                this.atbSystem.FillOperatorGauges();

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
            if (this.freezeOperatorWhenActionQueued)
                this.atbSystem.FreezeActor(action.SlotIndex, ATBActorKind.Operator);
            this.menuView.SetOperatorDimmed(action.SlotIndex, true);
        }

        public void SetWaitMode(bool paused) => this.waitModeActive = paused;

        public bool IsOperatorReady(int slotIndex)
        {
            ATBActorState? actor = this.atbSystem.GetActor(slotIndex, ATBActorKind.Operator);
            return actor != null && actor.IsReady && actor.IsAwaitingCommand;
        }

        public void NotifyEnemyStaggered(int enemySlot)
        {
            // Reset alone isn't enough — the gauge would keep ticking while staggered
            // and be fully charged (or overcharged) the instant it recovers, letting it
            // attack immediately. Freeze it at 0 for the whole knockdown; ProcessQueueHead
            // unfreezes it only once the EnemyRecover action actually resolves.
            this.atbSystem.ResetActor(enemySlot, ATBActorKind.Enemy);
            this.atbSystem.FreezeActor(enemySlot, ATBActorKind.Enemy);
        }

        public void MarkOperatorForFocusFire(int operatorSlot)
        {
            // Same "reset then freeze" shape as NotifyEnemyStaggered — the marked
            // operator's gauge must not tick back up to ready while it waits, or
            // NotifyReadyOperators() would offer it a command panel again.
            this.atbSystem.ResetActor(operatorSlot, ATBActorKind.Operator);
            this.atbSystem.FreezeActor(operatorSlot, ATBActorKind.Operator);
        }

        public void NotifyShootCompleted()
        {
            if (!this.actionQueue.HasPending) return;
            if (this.actionQueue.Peek().Type != PendingActionType.Shoot) return;
            int slotIndex = this.actionQueue.Peek().SlotIndex;
            this.DequeueAction();
            if (this.freezeOperatorWhenActionQueued)
                this.atbSystem.UnfreezeActor(slotIndex, ATBActorKind.Operator);
            this.shootConfigurationInProgress = false;
            SetAnimationLock(this.operatorActionDurationSec);
        }

        public void NotifyFocusFireCompleted()
        {
            if (!this.actionQueue.HasPending) return;
            if (this.actionQueue.Peek().Type != PendingActionType.FocusFire) return;
            this.DequeueAction();
            this.shootConfigurationInProgress = false;
            SetAnimationLock(this.operatorActionDurationSec);
        }

        // The single place actions leave the queue, so every dequeue can count toward
        // staggered enemies' action-based recovery (see NotifyActionDequeued on
        // IBattlefieldView) without duplicating that bookkeeping at each call site.
        private void DequeueAction()
        {
            this.actionQueue.Dequeue();
            int[] readySlots = this.battlefieldView.NotifyActionDequeued();
            for (int i = 0; i < readySlots.Length; i++)
                this.actionQueue.Enqueue(PendingAction.EnemyRecover(readySlots[i]));
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
                if (this.battlefieldView.IsEnemyStaggered(i)) continue;
                if (this.battlefieldView.IsEnemyDead(i)) continue;

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
            if (action.Type == PendingActionType.EnemyAttack || action.Type == PendingActionType.EnemyRecover)
            {
                // battlefieldView.IsEnemyDead is checked first because it reflects death the
                // instant it happens; the ATB actor's own IsDead flag only catches up once
                // SyncDeadEnemies notices the slot vanish from occupiedEnemySlots, which is
                // deferred until the death animation/blood-pool sequence finishes.
                if (this.battlefieldView.IsEnemyDead(action.SlotIndex)) return true;
                ATBActorState? actor = this.atbSystem.GetActor(action.SlotIndex, ATBActorKind.Enemy);
                return actor == null || actor.IsDead;
            }
            if (action.Type == PendingActionType.FocusFire)
            {
                for (int i = 0; i < action.FocusFireParticipants.Length; i++)
                {
                    int s = action.FocusFireParticipants[i];
                    if (s >= this.roster.Count || !this.roster[s].IsAlive) return true;
                }
                return false;
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
                    if (IsActorDead(head)) { this.DequeueAction(); return; }
                    this.shootConfigurationInProgress = true;
                    this.shootPublisher.Publish(new ShootConfigurationRequestedEvent(head.SlotIndex));
                }
                return;
            }

            if (head.Type == PendingActionType.FocusFire)
            {
                if (!this.shootConfigurationInProgress)
                {
                    if (IsActorDead(head)) { this.DequeueAction(); return; }
                    this.shootConfigurationInProgress = true;
                    for (int i = 0; i < head.FocusFireParticipants.Length; i++)
                        this.atbSystem.UnfreezeActor(head.FocusFireParticipants[i], ATBActorKind.Operator);
                    this.focusFirePublisher.Publish(new FocusFireConfigurationRequestedEvent(head.FocusFireParticipants));
                }
                return;
            }

            if (head.Type == PendingActionType.EnemyAttack)
            {
                if (!this.enemyAttackInProgress)
                {
                    if (IsActorDead(head)) { this.DequeueAction(); return; }
                    if (this.battlefieldView.IsEnemyStaggered(head.SlotIndex)) { this.DequeueAction(); return; }
                    if (Time.time < this.animationLockUntil) return;
                    this.enemyAttackInProgress = true;
                    ApplyEnemyAttack(head);
                }
                else if (Time.time >= this.animationLockUntil)
                {
                    this.atbSystem.UnfreezeActor(head.SlotIndex, ATBActorKind.Enemy);
                    this.DequeueAction();
                    this.enemyAttackInProgress = false;
                }
                return;
            }

            if (head.Type == PendingActionType.EnemyRecover)
            {
                if (IsActorDead(head)) { this.DequeueAction(); return; }
                if (Time.time < this.animationLockUntil) return;
                this.battlefieldView.RecoverEnemyStagger(head.SlotIndex);
                this.atbSystem.UnfreezeActor(head.SlotIndex, ATBActorKind.Enemy);
                this.DequeueAction();
                return;
            }

            if (IsActorDead(head))
            {
                this.DequeueAction();
                if (this.freezeOperatorWhenActionQueued)
                    this.atbSystem.UnfreezeActor(head.SlotIndex, ATBActorKind.Operator);
                return;
            }
            if (Time.time < this.animationLockUntil) return;
            this.DequeueAction();
            if (this.freezeOperatorWhenActionQueued)
                this.atbSystem.UnfreezeActor(head.SlotIndex, ATBActorKind.Operator);

            switch (head.Type)
            {
                case PendingActionType.UseItem:
                    ApplyUseItem(head);
                    SetAnimationLock(this.operatorActionDurationSec);
                    break;
            }
        }

        private void ApplyUseItem(PendingAction action)
        {
            if (action.ItemIndex < 0 || action.ItemIndex >= this.inventory.Slots.Count) return;
            InventorySlot slot = this.inventory.Slots[action.ItemIndex];
            if (slot.IsEmpty || slot.Item?.Data is not ConsumableData consumable) return;

            int targetSlot = action.TargetOperatorSlot >= 0 ? action.TargetOperatorSlot : action.SlotIndex;
            if (targetSlot < this.roster.Count && this.roster[targetSlot].IsAlive)
                this.roster[targetSlot].Heal(consumable.HealAmount);

            slot.Quantity--;
            if (slot.Quantity <= 0)
                this.inventory.RemoveItem(action.ItemIndex);
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
                float gps          = data.AttackBaseSec > 0f ? 1f / data.AttackBaseSec : 1f;
                float initialGauge = data.InitialGaugePct / 100f;
                configs.Add(new ATBActorConfig(i, ATBActorKind.Enemy, gps, initialGauge));
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
            for (int i = 0; i < this.roster.Count; i++)
            {
                bool  isPresent = this.roster[i].IsPresent;
                float hpRatio   = isPresent ? this.roster[i].HpRatio : 0f;
                bool  isAlive   = isPresent && this.roster[i].IsAlive;

                this.ecgFeedback?.SetOperatorHealthState(i, hpRatio, isAlive);
                this.menuView.SetOperatorHealth(i, hpRatio);
            }
        }
    }
}
