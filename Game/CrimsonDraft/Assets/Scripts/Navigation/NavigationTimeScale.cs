#nullable enable

using System;
using MessagePipe;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Navigation
{
    /// <summary>
    /// Escala de tiempo propia de la fase de Navigation, independiente de Time.timeScale global
    /// (que Combat necesita en 1 para su propio ATB mientras Navigation queda congelado detrás,
    /// ya que Combat se carga aditivo y ambas escenas comparten el mismo Time.timeScale).
    /// En Scale=0, todo lo que consulte DeltaTime/Scale en Navigation queda congelado sin afectar
    /// a Combate.
    /// </summary>
    public sealed class NavigationTimeScale : IInitializable, IDisposable
    {
        private readonly ISubscriber<CombatStartedEvent> combatStartedSubscriber;
        private readonly ISubscriber<CombatEndedEvent>   combatEndedSubscriber;

        private IDisposable? combatStartedSub;
        private IDisposable? combatEndedSub;

        public float Scale { get; private set; } = 1f;

        public float DeltaTime => UnityEngine.Time.deltaTime * Scale;

        [Preserve]
        public NavigationTimeScale(
            ISubscriber<CombatStartedEvent> combatStartedSubscriber,
            ISubscriber<CombatEndedEvent>   combatEndedSubscriber)
        {
            this.combatStartedSubscriber = combatStartedSubscriber;
            this.combatEndedSubscriber   = combatEndedSubscriber;
        }

        void IInitializable.Initialize()
        {
            this.combatStartedSub = this.combatStartedSubscriber.Subscribe(_ => Scale = 0f);
            this.combatEndedSub   = this.combatEndedSubscriber.Subscribe(_ => Scale = 1f);
        }

        void IDisposable.Dispose()
        {
            this.combatStartedSub?.Dispose();
            this.combatEndedSub?.Dispose();
        }
    }
}