#nullable enable

using System;
using System.Collections.Generic;
using MessagePipe;
using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation
{
    public sealed class OperatorCorpseBootstrap : IInitializable, IDisposable
    {
        private readonly IOperatorRoster                roster;
        private readonly IRoomOrchestrator               roomOrchestrator;
        private readonly PlayerController                player;
        private readonly ISubscriber<CombatEndedEvent>   combatEndedSubscriber;
        private readonly ISubscriber<RoomTransitionedEvent> roomTransitionedSubscriber;
        private readonly OperatorCorpseRegistry          registry;
        private readonly IOperatorCorpseSpawner          spawner;

        // Session-only bookkeeping: a slot only needs its corpse instantiated once — either
        // immediately when it dies (the player is already standing in that room), or the
        // first time its recorded room is (re)entered after a save load. This has nothing to
        // do with save persistence (that's OperatorCorpseRegistry.IsRecorded); it just stops
        // the same corpse being spawned twice if its room is left and re-entered.
        private readonly HashSet<int> spawnedSlots = new();

        private IDisposable? combatEndedSubscription;
        private IDisposable? roomTransitionedSubscription;

        [Preserve]
        public OperatorCorpseBootstrap(
            IOperatorRoster                 roster,
            IRoomOrchestrator               roomOrchestrator,
            PlayerController                player,
            ISubscriber<CombatEndedEvent>   combatEndedSubscriber,
            ISubscriber<RoomTransitionedEvent> roomTransitionedSubscriber,
            OperatorCorpseRegistry          registry,
            IOperatorCorpseSpawner          spawner)
        {
            this.roster                     = roster;
            this.roomOrchestrator           = roomOrchestrator;
            this.player                     = player;
            this.combatEndedSubscriber      = combatEndedSubscriber;
            this.roomTransitionedSubscriber = roomTransitionedSubscriber;
            this.registry                   = registry;
            this.spawner                    = spawner;
        }

        void IInitializable.Initialize()
        {
            this.combatEndedSubscription      = this.combatEndedSubscriber.Subscribe(OnCombatEnded);
            this.roomTransitionedSubscription = this.roomTransitionedSubscriber.Subscribe(OnRoomTransitioned);

            // Covers both a fresh game's starting room and a just-restored save's room --
            // RoomTransitionedEvent only fires on later door transitions, not this initial
            // activation (mirrors MapStateTracker's same initial-room handling).
            if (this.roomOrchestrator.CurrentRoom != null)
                SpawnRecordedCorpsesForRoom(this.roomOrchestrator.CurrentRoom);
        }

        private void OnCombatEnded(CombatEndedEvent ev)
        {
            RoomController? room = this.roomOrchestrator.CurrentRoom;
            if (room == null) return;

            for (int i = 0; i < this.roster.Count; i++)
            {
                if (this.roster[i].IsAlive) continue;
                if (this.registry.IsRecorded(i)) continue;

                Vector3    pos = this.player.transform.position;
                Quaternion rot = this.player.transform.rotation;

                this.registry.Record(i, room.RoomId, pos, rot);
                this.spawnedSlots.Add(i);
                this.spawner.Spawn(room, pos, rot);
            }
        }

        private void OnRoomTransitioned(RoomTransitionedEvent e)
        {
            if (e.ActiveRoom != null)
                SpawnRecordedCorpsesForRoom(e.ActiveRoom);
        }

        private void SpawnRecordedCorpsesForRoom(RoomController room)
        {
            foreach (var entry in this.registry.GetAll())
            {
                if (entry.RoomId != room.RoomId) continue;
                if (!this.spawnedSlots.Add(entry.SlotIndex)) continue;

                this.spawner.Spawn(room, entry.Position, entry.Rotation);
            }
        }

        void IDisposable.Dispose()
        {
            this.combatEndedSubscription?.Dispose();
            this.roomTransitionedSubscription?.Dispose();
        }
    }
}
