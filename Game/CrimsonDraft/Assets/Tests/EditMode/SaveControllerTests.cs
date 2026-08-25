#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Infrastructure.Save.UI;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class SaveControllerTests
    {
        private sealed class FakeSaveGameService : ISaveGameService
        {
            public int? WrittenSlot;
            public SaveGameData? WrittenData;

            public IReadOnlyList<SaveSlotSummary> ListSlotSummaries() => Array.Empty<SaveSlotSummary>();
            public void WriteToDisk(int slot, SaveGameData data) { this.WrittenSlot = slot; this.WrittenData = data; }
            public SaveGameData? ReadFromDisk(int slot) => null;
            public bool DeleteSlot(int slot) => false;
            public bool LoadSlot(int slot) => false;
            public SaveGameData? ConsumePendingLoad() => null;
        }

        private sealed class FakeInventoryService : IInventoryService
        {
            public InventorySlot[] RawSlots = Array.Empty<InventorySlot>();
            public int SlotCount => this.RawSlots.Length;
            public IReadOnlyList<InventorySlot> Slots => this.RawSlots;
            public bool AddItem(ItemData data, int operatorSlot, int quantity = 0) => false;
            public bool AddExistingItem(InventoryItem item, int operatorSlot) => false;
            public bool AddItemAuto(ItemData data, int quantity = 0) => false;
            public void RemoveItem(int slotIndex) { }
            public void PruneEmptyStacks() { }
            public void MoveItem(int fromSlot, int toSlot) { }
            public void EquipWeapon(int slotIndex, int operatorSlot) { }
            public void UnequipWeapon(int slotIndex) { }
            public int GetEquippedWeaponIndex(int operatorSlot) => -1;
            public bool CanReload(int slotIndex, int operatorSlot) => false;
            public void ReloadOperator(int slotIndex, int operatorSlot) { }
            public bool TryCombine(int slotA, int slotB) => false;
            public KeyUseOutcome TryUseKey(string keyItemId) => new KeyUseOutcome(KeyUseResult.NotFound, -1);
            public void LoadState(InventorySlot[] slots) { }
            public void SetSlotPosition(int slotIndex, int col, int row, int rotation) { }
            public InventorySlot[] GetRawSlots() => this.RawSlots;
        }

        private sealed class FakeRoster : IOperatorRoster
        {
            public int[] Hp = Array.Empty<int>();
            public bool IsInitialized => true;
            public int Count => 1;
            public OperatorRuntime this[int slotIndex] => new OperatorRuntime(slotIndex, null, isPresent: true, maxHp: 100);
            public IReadOnlyList<int> GetAliveSlots() => new List<int> { 0 };
            public void EnsureInitialized() { }
            public int[] GetHpSnapshot() => this.Hp;
            public void RestoreHp(int[] snapshot) { }
        }

        private sealed class FakeRoomOrchestrator : IRoomOrchestrator
        {
            public RoomController? Current;
            public RoomController? CurrentRoom => this.Current;
            public UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab) => UniTask.CompletedTask;
            public void ActivateRoomImmediate(string roomId) { }
        }

        private sealed class FakeInputService : IInputService
        {
            public InputAction Move                   => null!;
            public InputAction Interact               => null!;
            public InputAction OpenInventory          => null!;
            public InputAction OpenMap                => null!;
            public InputAction Aim                    => null!;
            public InputAction AimFire                => null!;
            public InputAction Pause                  => null!;
            public InputAction Sprint                 => null!;
            public InputAction CombatNavigate         => null!;
            public InputAction CombatConfirm          => null!;
            public InputAction CombatCancel           => null!;
            public InputAction CombatUseItem          => null!;
            public InputAction UINavigate             => null!;
            public InputAction UIConfirm              => null!;
            public InputAction UICancel               => null!;
            public InputAction UIBack                 => null!;
            public InputAction DialogueAdvanceLine    => null!;
            public InputAction DialogueCancelDialogue => null!;
            public InputAction DoorTransitionSkip     => null!;
            public InputAction PickupNavigate         => null!;
            public InputAction PickupConfirm          => null!;
            public InputAction InventoryNavigate      => null!;
            public InputAction InventoryConfirm       => null!;
            public InputAction InventoryPickup        => null!;
            public InputAction InventoryCancel        => null!;
            public InputAction InventoryNextTab       => null!;
            public InputAction InventoryPrevTab       => null!;
            public InputAction InventoryCloseMap      => null!;
            public InputAction InventoryClose         => null!;
            public void SwitchToGameplay()      { }
            public void SwitchToCombat()        { }
            public void SwitchToUI()            { }
            public void SwitchToDialogue()      { }
            public void SwitchToDoorTransition() { }
            public void SwitchToPickupPrompt()  { }
            public void SwitchToInventory()     { }
            public void Dispose()               { }
        }

        private static WeaponData MakeWeaponData(string id)
        {
            var d  = ScriptableObject.CreateInstance<WeaponData>();
            var so = new SerializedObject(d);
            so.FindProperty("itemId").stringValue        = id;
            so.FindProperty("itemType").enumValueIndex   = (int)ItemType.Weapon;
            so.FindProperty("displayName").stringValue   = "Test Weapon";
            so.FindProperty("magazineCapacity").intValue = 12;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        [Test]
        public void BuildSaveData_capturesWorldStateAndInventory_andWritesToService()
        {
            var world = new WorldStateRegistries(
                new DoorStateRegistry(), new RoomStateRegistry(), new PickupRegistry(),
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry());
            world.Doors.SetUnlocked("door-1");
            world.Rooms.MarkVisited("room-1");
            world.Pickups.SetCollected("pickup-1");
            world.Notes.SetCollected("note-1");
            world.KnownMaps.MarkKnown("map-1");
            world.Enemies.SetDefeated("enemy-1");

            var weaponData = MakeWeaponData("weapon-1");
            var weaponItem = new WeaponItem(weaponData);
            weaponItem.SetAmmo(7);
            var inventory = new FakeInventoryService
            {
                RawSlots = new[] { new InventorySlot { Item = weaponItem, Quantity = 1 } },
            };

            var roster    = new FakeRoster { Hp = new[] { 42 } };
            var roomGo    = new GameObject("Room");
            var room      = roomGo.AddComponent<RoomController>();
            var roomSo    = new SerializedObject(room);
            roomSo.FindProperty("roomId").stringValue = "room-1";
            roomSo.ApplyModifiedPropertiesWithoutUndo();
            var roomOrch  = new FakeRoomOrchestrator { Current = room };

            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(5f, 0f, 2f);
            var player   = playerGo.AddComponent<PlayerController>();

            var view       = MakeView();
            var saveService = new FakeSaveGameService();
            var inputService = new FakeInputService();

            try
            {
                var controller = new SaveController(
                    inputService, view, saveService, inventory, roster, roomOrch, player, world, new PlaytimeTracker());

                controller.Save(3);

                Assert.AreEqual(3, saveService.WrittenSlot);
                var data = saveService.WrittenData!;
                Assert.AreEqual("room-1", data.roomId);
                Assert.AreEqual(new Vector3(5f, 0f, 2f), data.playerPosition);
                Assert.AreEqual(1, data.doors.Count);
                Assert.AreEqual("door-1", data.doors[0].doorId);
                Assert.AreEqual(1, data.rooms.Count);
                Assert.AreEqual(1, data.collectedPickupIds.Count);
                Assert.AreEqual(1, data.readNoteIds.Count);
                Assert.AreEqual(1, data.knownMapIds.Count);
                Assert.AreEqual(1, data.defeatedEnemyIds.Count);
                CollectionAssert.AreEqual(new[] { 42 }, data.operatorHp);
                Assert.AreEqual(1, data.inventorySlots.Count);
                Assert.AreEqual("weapon-1", data.inventorySlots[0].itemId);
                Assert.AreEqual(7, data.inventorySlots[0].weaponAmmo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
                UnityEngine.Object.DestroyImmediate(weaponData);
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        private static SaveSlotListView MakeView()
        {
            var go   = new GameObject("SaveSlotListView");
            var view = go.AddComponent<SaveSlotListView>();

            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue        = go;
            so.FindProperty("confirmPanel").objectReferenceValue = go;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }
    }
}
