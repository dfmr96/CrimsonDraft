#nullable enable

using System;
using MessagePipe;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>Forwards room entries to every RoomVisitMutation in the scene. Lives outside the
    /// rooms because the mutation's own room is inactive while the player is in another one.</summary>
    public sealed class RoomVisitMutationTracker : IInitializable, IDisposable
    {
        private readonly ItemSocketStateRegistry            registry;
        private readonly RoomVisitMutation[]                mutations;
        private readonly ISubscriber<RoomTransitionedEvent> subscriber;

        private IDisposable? subscription;

        [Preserve]
        public RoomVisitMutationTracker(
            ItemSocketStateRegistry            registry,
            RoomVisitMutation[]                mutations,
            ISubscriber<RoomTransitionedEvent> subscriber)
        {
            this.registry   = registry;
            this.mutations  = mutations;
            this.subscriber = subscriber;
        }

        void IInitializable.Initialize()
        {
            foreach (var mutation in this.mutations)
                mutation.Construct(this.registry);

            this.subscription = this.subscriber.Subscribe(OnRoomTransitioned);
        }

        private void OnRoomTransitioned(RoomTransitionedEvent e)
        {
            if (e.ActiveRoom == null) return;

            foreach (var mutation in this.mutations)
                mutation.OnRoomEntered(e.ActiveRoom.RoomId);
        }

        void IDisposable.Dispose() => this.subscription?.Dispose();
    }
}
