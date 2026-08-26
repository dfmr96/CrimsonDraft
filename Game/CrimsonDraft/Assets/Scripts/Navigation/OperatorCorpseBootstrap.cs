#nullable enable

using System;
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
        private readonly IOperatorRoster               roster;
        private readonly IRoomOrchestrator              roomOrchestrator;
        private readonly PlayerController               player;
        private readonly ISubscriber<CombatEndedEvent>  combatEndedSubscriber;
        private readonly OperatorCorpseRegistry         registry;
        private readonly IOperatorCorpseSpawner         spawner;

        private IDisposable? subscription;

        [Preserve]
        public OperatorCorpseBootstrap(
            IOperatorRoster              roster,
            IRoomOrchestrator            roomOrchestrator,
            PlayerController             player,
            ISubscriber<CombatEndedEvent> combatEndedSubscriber,
            OperatorCorpseRegistry       registry,
            IOperatorCorpseSpawner       spawner)
        {
            this.roster                = roster;
            this.roomOrchestrator      = roomOrchestrator;
            this.player                = player;
            this.combatEndedSubscriber = combatEndedSubscriber;
            this.registry              = registry;
            this.spawner               = spawner;
        }

        void IInitializable.Initialize()
        {
            this.subscription = this.combatEndedSubscriber.Subscribe(OnCombatEnded);
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
                this.spawner.Spawn(room, pos, rot);
            }
        }

        void IDisposable.Dispose()
        {
            this.subscription?.Dispose();
        }
    }
}
