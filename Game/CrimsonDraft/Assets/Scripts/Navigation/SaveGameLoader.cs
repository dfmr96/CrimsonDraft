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

            ApplyDoors(data);
            ApplyRooms(data);
            this.world.Pickups.LoadState(data.collectedPickupIds);
            this.world.Notes.LoadState(data.readNoteIds);
            this.world.KnownMaps.LoadState(data.knownMapIds);
            this.world.Enemies.LoadState(data.defeatedEnemyIds);
            ApplyOperatorCorpses(data);
            ApplyInventory(data);
            this.roster.RestoreHp(data.operatorHp);
            this.playtimeTracker.RestoreFrom(data.playtimeSeconds);

            this.roomOrchestrator.ActivateRoomImmediate(data.roomId);
            this.player.transform.SetPositionAndRotation(data.playerPosition, data.playerRotation);
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
            int slotCount = this.inventoryService.SlotCount;
            var slots     = new InventorySlot[slotCount];
            for (int i = 0; i < slotCount; i++)
                slots[i] = new InventorySlot();

            foreach (var entry in data.inventorySlots)
            {
                if (entry.slotIndex < 0 || entry.slotIndex >= slotCount) continue;
                if (!this.itemDatabase.TryGetById(entry.itemId, out var itemData)) continue;

                InventoryItem item = itemData switch
                {
                    WeaponData     wd => new WeaponItem(wd),
                    AmmoBoxData    ad => new AmmoBoxItem(ad, entry.ammoBoxQuantity >= 0 ? entry.ammoBoxQuantity : ad.DefaultQuantity),
                    ConsumableData cd => new ConsumableItem(cd),
                    KeyItemData    kd => new KeyItem(kd),
                    SocketItemData sd => new SocketItem(sd),
                    _ => throw new ArgumentException($"Unknown ItemData subtype: {itemData.GetType().Name}")
                };

                item.IsExamined = entry.isExamined;

                if (item is WeaponItem weaponItem && entry.weaponAmmo >= 0)
                    weaponItem.SetAmmo(entry.weaponAmmo);

                if (item is KeyItem keyItem && entry.keyUsesRemaining >= 0)
                {
                    int toConsume = keyItem.Data.MaxUses - entry.keyUsesRemaining;
                    for (int c = 0; c < toConsume; c++)
                        keyItem.Consume();
                }

                if (entry.equippedOperatorSlot >= 0)
                    item.SetEquipped(entry.equippedOperatorSlot, entry.equippedWeaponSlot);

                slots[entry.slotIndex] = new InventorySlot
                {
                    Item         = item,
                    Quantity     = entry.slotQuantity,
                    GridCol      = entry.gridCol,
                    GridRow      = entry.gridRow,
                    GridRotation = entry.gridRotation,
                };
            }

            this.inventoryService.LoadState(slots);
        }
    }
}
