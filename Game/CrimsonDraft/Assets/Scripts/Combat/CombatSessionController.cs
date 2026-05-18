#nullable enable

using System;
using MessagePipe;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Combat
{
    public sealed class CombatSessionController : IInitializable, IDisposable
    {
        private readonly IPublisher<CombatEndedEvent> combatEndedPublisher;

        [UnityEngine.Scripting.Preserve]
        public CombatSessionController(IPublisher<CombatEndedEvent> combatEndedPublisher)
        {
            this.combatEndedPublisher = combatEndedPublisher;
        }

        void IInitializable.Initialize() { }

        public void EndCombat(bool victory) =>
            this.combatEndedPublisher.Publish(new CombatEndedEvent { Victory = victory });

        void IDisposable.Dispose() { }
    }
}
