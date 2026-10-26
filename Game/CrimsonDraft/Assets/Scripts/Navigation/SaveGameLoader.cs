#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation
{
    /// <summary>
    /// Applies a pending loaded save (if any) to the cross-scene registries, inventory, and
    /// player transform. Must run after RoomOrchestrator (so CurrentRoom is already set to a
    /// default before being overridden) and before DoorBootstrap/PickupBootstrap/
    /// MapPickupBootstrap/DocumentPickupBootstrap (so they see the restored registry state).
    /// </summary>
    public sealed class SaveGameLoader : IInitializable
    {
        private readonly ISaveGameService     saveGameService;
        private readonly IInventoryService    inventoryService;
        private readonly IOperatorRoster      roster;
        private readonly IRoomOrchestrator    roomOrchestrator;
        private readonly PlayerController     player;
        private readonly ItemDatabase         itemDatabase;
        private readonly WorldStateRegistries world;
        private readonly PlaytimeTracker      playtimeTracker;

        [Preserve]
        public SaveGameLoader(
            ISaveGameService     saveGameService,
            IInventoryService    inventoryService,
            IOperatorRoster      roster,
            IRoomOrchestrator    roomOrchestrator,
            PlayerController     player,
            ItemDatabase         itemDatabase,
            WorldStateRegistries world,
            PlaytimeTracker      playtimeTracker)
        {
            this.saveGameService  = saveGameService;
            this.inventoryService = inventoryService;
            this.roster           = roster;
            this.roomOrchestrator = roomOrchestrator;
            this.player           = player;
            this.itemDatabase     = itemDatabase;
            this.world            = world;
            this.playtimeTracker  = playtimeTracker;
        }

        void IInitializable.Initialize()
        {
            var data = this.saveGameService.ConsumePendingLoad();
            if (data == null) return;

            // First, so anything raised while the rest restores (e.g. inventory ContentsChanged)
            // already sees the loaded milestones, puzzles and gates.
            this.world.Milestones.LoadState(data.completedMilestoneIds ?? new List<string>());
            this.world.Puzzles.LoadState(data.solvedPuzzleIds ?? new List<string>());
            this.world.Gates.LoadState(data.completedGateIds ?? new List<string>());

            ApplyDoors(data);
            ApplyRooms(data);
            ApplyItemSockets(data);
            ApplyPushables(data);
            this.world.Pickups.LoadState(data.collectedPickupIds);
            this.world.Notes.LoadState(data.readNoteIds);
            this.world.KnownMaps.LoadState(data.knownMapIds);
            ApplyEnemies(data);
            this.world.RadioCalls.LoadState(data.heardRadioCallIds);
            ApplyOperatorCorpses(data);
            ApplyInventory(data);
            this.roster.RestoreHp(data.operatorHp);
            this.playtimeTracker.RestoreFrom(data.playtimeSeconds);

            // Player first: when the save names no camera (older saves), the room picks its shot
            // from where the player stands, which must already be the saved spot.
            this.player.transform.SetPositionAndRotation(data.playerPosition, data.playerRotation);
            this.roomOrchestrator.ActivateRoomImmediate(data.roomId, data.cameraPath);
        }

        private void ApplyDoors(SaveGameData data)
        {
            var dict = new Dictionary<string, DoorMapState>();
            foreach (var entry in data.doors)
                dict[entry.doorId] = entry.state;
            this.world.Doors.LoadState(dict);
        }

        private void ApplyRooms(SaveGameData data)
        {
            var dict = new Dictionary<string, RoomMapState>();
            foreach (var entry in data.rooms)
                dict[entry.roomId] = entry.state;
            this.world.Rooms.LoadState(dict);
        }

        private void ApplyItemSockets(SaveGameData data)
        {
            var dict = new Dictionary<string, bool[]>();
            foreach (var entry in data.itemSockets)
                dict[entry.socketId] = entry.inserted;
            this.world.ItemSockets.LoadState(dict);
        }

        // Registry only -- PushableBootstrap (registered after this loader) moves the boxes.
        private void ApplyPushables(SaveGameData data)
        {
            var dict = new Dictionary<string, UnityEngine.Vector3>();
            foreach (var entry in data.pushables)
                dict[entry.pushableId] = entry.position;
            this.world.Pushables.LoadState(dict);
        }

        // Registry only -- EnemyBootstrap (registered after this loader) lays the bodies down.
        private void ApplyEnemies(SaveGameData data)
        {
            var poses = new List<EnemyStateRegistry.CorpsePose>();
            foreach (var e in data.enemyCorpses ?? new List<EnemyCorpseEntry>())
                poses.Add(new EnemyStateRegistry.CorpsePose(e.enemyId, e.position, e.rotation));
            this.world.Enemies.LoadState(data.defeatedEnemyIds, poses, data.revivedEnemyIds ?? new List<string>());
        }

        // Only restores the registry data — actually spawning each corpse's GameObject is
        // deferred to OperatorCorpseBootstrap, which does it lazily as each room becomes
        // active (on this same startup for the restored current room, or later via
        // RoomTransitionedEvent), rather than instantiating every recorded corpse across
        // every room in the level up front.
        private void ApplyOperatorCorpses(SaveGameData data)
        {
            var entries = new List<OperatorCorpseRegistry.Entry>();
            foreach (var e in data.operatorCorpses)
                entries.Add(new OperatorCorpseRegistry.Entry(e.slotIndex, e.roomId, e.position, e.rotation));
            this.world.OperatorCorpses.LoadState(entries);
        }

        private void ApplyInventory(SaveGameData data)
        {
            var entries = data.inventoryItems.Count > 0 || data.inventorySlots.Count == 0
                ? data.inventoryItems
                : InventorySerializer.FromLegacySlots(data.inventorySlots);
            this.inventoryService.Restore(entries, this.itemDatabase);
        }
    }
}
