#nullable enable

using System;
using MessagePipe;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation
{
    public sealed class DeadOperatorGearReleaser : IInitializable, IDisposable
    {
        private readonly IInventoryService             inventory;
        private readonly ISubscriber<CombatEndedEvent> combatEndedSubscriber;
        private IDisposable?                           subscription;

        [Preserve]
        public DeadOperatorGearReleaser(IInventoryService inventory, ISubscriber<CombatEndedEvent> combatEndedSubscriber)
        {
            this.inventory             = inventory;
            this.combatEndedSubscriber = combatEndedSubscriber;
        }

        void IInitializable.Initialize()
        {
            this.inventory.ReleaseDeadOperatorWeapons();
            this.subscription = this.combatEndedSubscriber.Subscribe(_ => this.inventory.ReleaseDeadOperatorWeapons());
        }

        void IDisposable.Dispose() => this.subscription?.Dispose();
    }
}
