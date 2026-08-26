#nullable enable

using System;
using System.Collections.Generic;
using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Cysharp.Threading.Tasks;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class OperatorCorpseBootstrapTests
    {
        private sealed class FakeRoster : IOperatorRoster
        {
            private readonly OperatorRuntime[] slots;

            public FakeRoster(int count, params int[] deadSlots)
            {
                this.slots = new OperatorRuntime[count];
                for (int i = 0; i < count; i++)
                {
                    this.slots[i] = new OperatorRuntime(i, null, isPresent: true, maxHp: 100);
                    if (Array.IndexOf(deadSlots, i) >= 0)
                        this.slots[i].ApplyDamage(9999);
                }
            }

            public bool IsInitialized => true;
            public int Count => this.slots.Length;
            public OperatorRuntime this[int slotIndex] => this.slots[slotIndex];

            public IReadOnlyList<int> GetAliveSlots()
            {
                var alive = new List<int>();
                for (int i = 0; i < this.slots.Length; i++)
                    if (this.slots[i].IsAlive) alive.Add(i);
                return alive;
            }

            public void EnsureInitialized() { }
            public int[] GetHpSnapshot() => Array.Empty<int>();
            public void RestoreHp(int[] snapshot) { }
        }

        private sealed class FakeRoomOrchestrator : IRoomOrchestrator
        {
            public FakeRoomOrchestrator(RoomController? currentRoom) => this.CurrentRoom = currentRoom;
            public RoomController? CurrentRoom { get; }
            public UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab) => UniTask.CompletedTask;
            public void ActivateRoomImmediate(string roomId) { }
        }

        private sealed class FakeSpawner : IOperatorCorpseSpawner
        {
            public int SpawnCallCount;
            public RoomController? LastRoom;
            public Vector3 LastPosition;

            public void Spawn(RoomController room, Vector3 position, Quaternion rotation)
            {
                this.SpawnCallCount++;
                this.LastRoom     = room;
                this.LastPosition = position;
            }
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

        [Test]
        public void OnCombatEnded_recordsAndSpawnsCorpseForNewlyDeadOperator()
        {
            var roomGo = new GameObject("Room");
            var room   = roomGo.AddComponent<RoomController>();
            var roomSo = new SerializedObject(room);
            roomSo.FindProperty("roomId").stringValue = "room-1";
            roomSo.ApplyModifiedPropertiesWithoutUndo();

            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(4f, 0f, 5f);
            var player = playerGo.AddComponent<PlayerController>();

            var roster     = new FakeRoster(count: 2, deadSlots: 1);
            var roomOrch   = new FakeRoomOrchestrator(room);
            var subscriber = new FakeSubscriber<CombatEndedEvent>();
            var roomTransitionedSubscriber = new FakeSubscriber<RoomTransitionedEvent>();
            var registry   = new OperatorCorpseRegistry();
            var spawner    = new FakeSpawner();

            try
            {
                var bootstrap = new OperatorCorpseBootstrap(roster, roomOrch, player, subscriber, roomTransitionedSubscriber, registry, spawner);
                ((IInitializable)bootstrap).Initialize();

                subscriber.Publish(new CombatEndedEvent { Victory = true });

                Assert.IsTrue(registry.IsRecorded(1));
                Assert.IsFalse(registry.IsRecorded(0));
                Assert.AreEqual(1, spawner.SpawnCallCount);
                Assert.AreEqual(room, spawner.LastRoom);
                Assert.AreEqual(new Vector3(4f, 0f, 5f), spawner.LastPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }

        [Test]
        public void OnCombatEnded_doesNotRespawnAlreadyRecordedOperator()
        {
            var roomGo = new GameObject("Room");
            var room   = roomGo.AddComponent<RoomController>();

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            var roster     = new FakeRoster(count: 1, deadSlots: 0);
            var roomOrch   = new FakeRoomOrchestrator(room);
            var subscriber = new FakeSubscriber<CombatEndedEvent>();
            var roomTransitionedSubscriber = new FakeSubscriber<RoomTransitionedEvent>();
            var registry   = new OperatorCorpseRegistry();
            var spawner    = new FakeSpawner();
            registry.Record(0, "room-1", Vector3.zero, Quaternion.identity);

            try
            {
                var bootstrap = new OperatorCorpseBootstrap(roster, roomOrch, player, subscriber, roomTransitionedSubscriber, registry, spawner);
                ((IInitializable)bootstrap).Initialize();

                subscriber.Publish(new CombatEndedEvent { Victory = false });

                Assert.AreEqual(0, spawner.SpawnCallCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }

        [Test]
        public void OnCombatEnded_ignoresAliveOperators()
        {
            var roomGo = new GameObject("Room");
            var room   = roomGo.AddComponent<RoomController>();

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            var roster     = new FakeRoster(count: 1); // no dead slots
            var roomOrch   = new FakeRoomOrchestrator(room);
            var subscriber = new FakeSubscriber<CombatEndedEvent>();
            var roomTransitionedSubscriber = new FakeSubscriber<RoomTransitionedEvent>();
            var registry   = new OperatorCorpseRegistry();
            var spawner    = new FakeSpawner();

            try
            {
                var bootstrap = new OperatorCorpseBootstrap(roster, roomOrch, player, subscriber, roomTransitionedSubscriber, registry, spawner);
                ((IInitializable)bootstrap).Initialize();

                subscriber.Publish(new CombatEndedEvent { Victory = true });

                Assert.AreEqual(0, spawner.SpawnCallCount);
                Assert.IsFalse(registry.IsRecorded(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }

        [Test]
        public void Initialize_spawnsCorpseAlreadyRecordedForCurrentRoom()
        {
            var roomGo = new GameObject("Room");
            var room   = roomGo.AddComponent<RoomController>();
            var roomSo = new SerializedObject(room);
            roomSo.FindProperty("roomId").stringValue = "room-1";
            roomSo.ApplyModifiedPropertiesWithoutUndo();

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            var roster     = new FakeRoster(count: 1, deadSlots: 0);
            var roomOrch   = new FakeRoomOrchestrator(room);
            var subscriber = new FakeSubscriber<CombatEndedEvent>();
            var roomTransitionedSubscriber = new FakeSubscriber<RoomTransitionedEvent>();
            var registry   = new OperatorCorpseRegistry();
            var spawner    = new FakeSpawner();
            registry.Record(0, "room-1", new Vector3(2f, 0f, 2f), Quaternion.identity);

            try
            {
                var bootstrap = new OperatorCorpseBootstrap(roster, roomOrch, player, subscriber, roomTransitionedSubscriber, registry, spawner);
                ((IInitializable)bootstrap).Initialize();

                Assert.AreEqual(1, spawner.SpawnCallCount);
                Assert.AreEqual(room, spawner.LastRoom);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }

        [Test]
        public void RoomTransitioned_spawnsRecordedCorpseOnlyForItsOwnRoom_andOnlyOnce()
        {
            var otherRoomGo = new GameObject("OtherRoom");
            var otherRoom    = otherRoomGo.AddComponent<RoomController>();
            var otherRoomSo  = new SerializedObject(otherRoom);
            otherRoomSo.FindProperty("roomId").stringValue = "room-1";
            otherRoomSo.ApplyModifiedPropertiesWithoutUndo();

            var targetRoomGo = new GameObject("TargetRoom");
            var targetRoom    = targetRoomGo.AddComponent<RoomController>();
            var targetRoomSo  = new SerializedObject(targetRoom);
            targetRoomSo.FindProperty("roomId").stringValue = "room-2";
            targetRoomSo.ApplyModifiedPropertiesWithoutUndo();

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            var roster     = new FakeRoster(count: 1, deadSlots: 0);
            var roomOrch   = new FakeRoomOrchestrator(otherRoom); // starts in room-1, not room-2
            var subscriber = new FakeSubscriber<CombatEndedEvent>();
            var roomTransitionedSubscriber = new FakeSubscriber<RoomTransitionedEvent>();
            var registry   = new OperatorCorpseRegistry();
            var spawner    = new FakeSpawner();
            registry.Record(0, "room-2", new Vector3(3f, 0f, 3f), Quaternion.identity);

            try
            {
                var bootstrap = new OperatorCorpseBootstrap(roster, roomOrch, player, subscriber, roomTransitionedSubscriber, registry, spawner);
                ((IInitializable)bootstrap).Initialize();

                Assert.AreEqual(0, spawner.SpawnCallCount);

                roomTransitionedSubscriber.Publish(new RoomTransitionedEvent(otherRoom));
                Assert.AreEqual(0, spawner.SpawnCallCount);

                roomTransitionedSubscriber.Publish(new RoomTransitionedEvent(targetRoom));
                Assert.AreEqual(1, spawner.SpawnCallCount);
                Assert.AreEqual(targetRoom, spawner.LastRoom);

                roomTransitionedSubscriber.Publish(new RoomTransitionedEvent(targetRoom));
                Assert.AreEqual(1, spawner.SpawnCallCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherRoomGo);
                UnityEngine.Object.DestroyImmediate(targetRoomGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }
    }
}
