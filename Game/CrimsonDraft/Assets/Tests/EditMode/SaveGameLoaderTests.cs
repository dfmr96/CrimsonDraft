#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Cysharp.Threading.Tasks;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class SaveGameLoaderTests
    {
        private sealed class FakeSaveGameService : ISaveGameService
        {
            public SaveGameData? PendingLoad;
            public IReadOnlyList<SaveSlotSummary> ListSlotSummaries() => Array.Empty<SaveSlotSummary>();
            public void WriteToDisk(int slot, SaveGameData data) { }
            public SaveGameData? ReadFromDisk(int slot) => null;
            public bool DeleteSlot(int slot) => false;
            public bool LoadSlot(int slot) => false;
            public SaveGameData? ConsumePendingLoad()
            {
                var data = this.PendingLoad;
                this.PendingLoad = null;
                return data;
            }
        }

        private sealed class FakeInventoryService : IInventoryService
        {
            public int SlotCount { get; set; } = 4;
            public InventorySlot[]? LoadedSlots { get; private set; }
            public IReadOnlyList<InventorySlot> Slots => Array.Empty<InventorySlot>();
            public bool AddItem(ItemData data, int operatorSlot, int quantity = 0) => false;
            public bool AddItemAuto(ItemData data, int quantity = 0) => false;
            public void RemoveItem(int slotIndex) { }
            public void MoveItem(int fromSlot, int toSlot) { }
            public void EquipWeapon(int slotIndex, int operatorSlot) { }
            public void UnequipWeapon(int slotIndex) { }
            public int GetEquippedWeaponIndex(int operatorSlot) => -1;
            public bool CanReload(int slotIndex, int operatorSlot) => false;
            public void ReloadOperator(int slotIndex, int operatorSlot) { }
            public bool TryCombine(int slotA, int slotB) => false;
            public KeyUseOutcome TryUseKey(string keyItemId) => new KeyUseOutcome(KeyUseResult.NotFound, -1);
            public void LoadState(InventorySlot[] slots) => this.LoadedSlots = slots;
            public void SetSlotPosition(int slotIndex, int col, int row, int rotation) { }
            public InventorySlot[] GetRawSlots() => Array.Empty<InventorySlot>();
        }

        private sealed class FakeRoster : IOperatorRoster
        {
            public int[]? RestoredHp { get; private set; }
            public bool IsInitialized => true;
            public int Count => 1;
            public OperatorRuntime this[int slotIndex] => new OperatorRuntime(slotIndex, null, isPresent: true, maxHp: 100);
            public IReadOnlyList<int> GetAliveSlots() => new List<int> { 0 };
            public void EnsureInitialized() { }
            public int[] GetHpSnapshot() => Array.Empty<int>();
            public void RestoreHp(int[] snapshot) => this.RestoredHp = snapshot;
        }

        private sealed class FakeRoomOrchestrator : IRoomOrchestrator
        {
            public string? ActivatedRoomId { get; private set; }
            public RoomController? CurrentRoom => null;
            public UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab) => UniTask.CompletedTask;
            public void ActivateRoomImmediate(string roomId) => this.ActivatedRoomId = roomId;
        }

        private static KeyItemData MakeKeyItemData(string id, int maxUses)
        {
            var d  = ScriptableObject.CreateInstance<KeyItemData>();
            var so = new SerializedObject(d);
            so.FindProperty("itemId").stringValue      = id;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.KeyItem;
            so.FindProperty("displayName").stringValue = "Test Key";
            so.FindProperty("maxUses").intValue         = maxUses;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        private static ItemDatabase MakeDatabase(params ItemData[] items)
        {
            var db  = ScriptableObject.CreateInstance<ItemDatabase>();
            var so  = new SerializedObject(db);
            var arr = so.FindProperty("allItems");
            arr.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return db;
        }

        [Test]
        public void Initialize_withNoPendingLoad_doesNothing()
        {
            var saveService = new FakeSaveGameService();
            var inventory   = new FakeInventoryService();
            var roster      = new FakeRoster();
            var roomOrch    = new FakeRoomOrchestrator();
            var itemDb      = MakeDatabase();
            var world       = new WorldStateRegistries(
                new DoorStateRegistry(), new RoomStateRegistry(), new PickupRegistry(),
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry());
            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            try
            {
                var loader = new SaveGameLoader(saveService, inventory, roster, roomOrch, player, itemDb, world, new PlaytimeTracker());
                ((IInitializable)loader).Initialize();

                Assert.IsNull(inventory.LoadedSlots);
                Assert.IsNull(roomOrch.ActivatedRoomId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerGo);
                UnityEngine.Object.DestroyImmediate(itemDb);
            }
        }

        [Test]
        public void Initialize_withPendingLoad_restoresRegistriesInventoryAndPosition()
        {
            var keyData = MakeKeyItemData("key-1", maxUses: 3);
            var itemDb  = MakeDatabase(keyData);

            var saveService = new FakeSaveGameService
            {
                PendingLoad = new SaveGameData
                {
                    sceneName      = "Deck_B",
                    roomId         = "room-2",
                    playerPosition = new Vector3(1f, 2f, 3f),
                    playerRotation = Quaternion.identity,
                    doors             = new List<DoorStateEntry> { new DoorStateEntry { doorId = "door-1", state = DoorMapState.Unlocked } },
                    rooms             = new List<RoomStateEntry> { new RoomStateEntry { roomId = "room-1", state = RoomMapState.Visited } },
                    collectedPickupIds = new List<string> { "pickup-1" },
                    readNoteIds        = new List<string> { "note-1" },
                    knownMapIds        = new List<string> { "map-1" },
                    defeatedEnemyIds   = new List<string> { "enemy-1" },
                    operatorHp         = new[] { 80 },
                    inventorySlots     = new List<InventorySlotEntry>
                    {
                        new InventorySlotEntry { slotIndex = 0, itemId = "key-1", keyUsesRemaining = 1 },
                    },
                },
            };
            var inventory = new FakeInventoryService { SlotCount = 4 };
            var roster    = new FakeRoster();
            var roomOrch  = new FakeRoomOrchestrator();
            var world = new WorldStateRegistries(
                new DoorStateRegistry(), new RoomStateRegistry(), new PickupRegistry(),
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry());
            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            try
            {
                var loader = new SaveGameLoader(saveService, inventory, roster, roomOrch, player, itemDb, world, new PlaytimeTracker());
                ((IInitializable)loader).Initialize();

                Assert.IsTrue(world.Doors.IsUnlocked("door-1"));
                Assert.AreEqual(RoomMapState.Visited, world.Rooms.GetState("room-1"));
                Assert.IsTrue(world.Pickups.IsCollected("pickup-1"));
                Assert.IsTrue(world.Notes.IsCollected("note-1"));
                Assert.IsTrue(world.KnownMaps.IsKnown("map-1"));
                Assert.IsTrue(world.Enemies.IsDefeated("enemy-1"));
                CollectionAssert.AreEqual(new[] { 80 }, roster.RestoredHp);
                Assert.AreEqual("room-2", roomOrch.ActivatedRoomId);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), player.transform.position);

                Assert.IsNotNull(inventory.LoadedSlots);
                var keyItem = inventory.LoadedSlots![0].Item as KeyItem;
                Assert.IsNotNull(keyItem);
                Assert.AreEqual(1, keyItem!.UsesRemaining);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerGo);
                UnityEngine.Object.DestroyImmediate(itemDb);
                UnityEngine.Object.DestroyImmediate(keyData);
            }
        }
    }
}
