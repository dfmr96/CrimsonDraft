#nullable enable

using System;
using MessagePipe;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation
{
    public sealed class MapStateTracker : IInitializable, IDisposable
    {
        private readonly RoomStateRegistry registry;
        private readonly ISubscriber<RoomTransitionedEvent> roomTransitionedSubscriber;
        private readonly IRoomOrchestrator roomOrchestrator;

        private IDisposable? subscription;

        [Preserve]
        public MapStateTracker(
            RoomStateRegistry registry,
            ISubscriber<RoomTransitionedEvent> roomTransitionedSubscriber,
            IRoomOrchestrator roomOrchestrator)
        {
            this.registry = registry;
            this.roomTransitionedSubscriber = roomTransitionedSubscriber;
            this.roomOrchestrator = roomOrchestrator;
        }

        void IInitializable.Initialize()
        {
            this.subscription = this.roomTransitionedSubscriber.Subscribe(OnRoomTransitioned);

            if (this.roomOrchestrator.CurrentRoom != null)
                this.registry.MarkVisited(this.roomOrchestrator.CurrentRoom.RoomId);
        }

        private void OnRoomTransitioned(RoomTransitionedEvent e)
        {
            if (e.ActiveRoom != null)
                this.registry.MarkVisited(e.ActiveRoom.RoomId);
        }

        void IDisposable.Dispose()
        {
            this.subscription?.Dispose();
        }
    }
}
