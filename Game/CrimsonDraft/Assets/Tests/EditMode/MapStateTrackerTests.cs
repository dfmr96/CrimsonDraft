#nullable enable

using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class MapStateTrackerTests
    {
        [Test]
        public void Initialize_marksCurrentRoomVisited_andSubscribesToTransitions()
        {
            var initialRoom = new GameObject("InitialRoom").AddComponent<RoomController>();
            var nextRoom    = new GameObject("NextRoom").AddComponent<RoomController>();
            var registry    = new RoomStateRegistry();
            var subscriber  = new FakeSubscriber<RoomTransitionedEvent>();
            var orchestrator = new FakeOrchestrator(initialRoom);

            try
            {
                var tracker = new MapStateTracker(registry, subscriber, orchestrator);
                ((IInitializable)tracker).Initialize();

                Assert.AreEqual(RoomMapState.Visited, registry.GetState(initialRoom.RoomId));

                subscriber.Publish(new RoomTransitionedEvent(nextRoom));
                Assert.AreEqual(RoomMapState.Visited, registry.GetState(nextRoom.RoomId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(initialRoom.gameObject);
                UnityEngine.Object.DestroyImmediate(nextRoom.gameObject);
            }
        }

        private sealed class FakeOrchestrator : IRoomOrchestrator
        {
            public FakeOrchestrator(RoomController currentRoom) => CurrentRoom = currentRoom;
            public RoomController? CurrentRoom { get; }

            public UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab)
                => UniTask.CompletedTask;
        }

        private sealed class FakeSubscriber<T> : ISubscriber<T>
        {
            private IMessageHandler<T>? handler;

            public IDisposable Subscribe(IMessageHandler<T> handler, params MessageHandlerFilter<T>[] filters)
            {
                this.handler = handler;
                return new Subscription(() => this.handler = null);
            }

            public void Publish(T value) => this.handler?.Handle(value);

            private sealed class Subscription : IDisposable
            {
                private readonly Action dispose;

                public Subscription(Action dispose) => this.dispose = dispose;
                public void Dispose() => this.dispose();
            }
        }
    }
}
