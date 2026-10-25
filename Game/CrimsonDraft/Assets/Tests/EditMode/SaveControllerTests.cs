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
            public bool TryStashPendingLoad(int slot, out string sceneName) { sceneName = string.Empty; return false; }
            public SaveGameData? ConsumePendingLoad() => null;
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
            public UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab, Vector3 departurePosition) => UniTask.CompletedTask;
            public void ActivateRoomImmediate(string roomId, string? cameraPath = null) { }
            public void TeleportToRoom(RoomController room) { }
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
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry(), new OperatorCorpseRegistry(),
                new ItemSocketStateRegistry(), new RadioCallRegistry(), new PushableStateRegistry(), new MilestoneRegistry(), new PuzzleStateRegistry());
            world.Doors.SetUnlocked("door-1");
            world.Rooms.MarkVisited("room-1");
            world.Pickups.SetCollected("pickup-1");
            world.Notes.SetCollected("note-1");
            world.KnownMaps.MarkKnown("map-1");
            world.Enemies.SetDefeated("enemy-1");
            world.Enemies.SetDefeated("enemy-2", new Vector3(3f, 0f, 7f), Quaternion.identity);
            world.Enemies.SetAlive("enemy-3");
            world.OperatorCorpses.Record(0, "room-1", new Vector3(9f, 0f, 9f), Quaternion.identity);
            world.ItemSockets.SetInserted("socket-1", new[] { true });
            world.Pushables.Record("crate-1", new Vector3(4f, 0f, 5f));
            world.Milestones.Complete("m1");
            world.Puzzles.SetSolved("p1");

            var weaponData = MakeWeaponData("weapon-1");
            var weaponItem = new WeaponItem(weaponData);
            weaponItem.SetAmmo(7);
            var inventory = new FakeInventoryService();
            inventory.Operator0.Place(weaponItem, Vector2Int.zero, 0);

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
                    inputService, view, saveService, inventory, roster, roomOrch, player, world, new PlaytimeTracker(),
                    new FakeCameraZoneService());

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
                Assert.AreEqual(2, data.defeatedEnemyIds.Count);
                Assert.AreEqual(1, data.enemyCorpses.Count);
                Assert.AreEqual("enemy-2", data.enemyCorpses[0].enemyId);
                Assert.AreEqual(new Vector3(3f, 0f, 7f), data.enemyCorpses[0].position);
                CollectionAssert.AreEqual(new[] { "enemy-3" }, data.revivedEnemyIds);
                Assert.AreEqual(1, data.itemSockets.Count);
                Assert.AreEqual("socket-1", data.itemSockets[0].socketId);
                CollectionAssert.AreEqual(new[] { true }, data.itemSockets[0].inserted);
                Assert.AreEqual(1, data.pushables.Count);
                Assert.AreEqual("crate-1", data.pushables[0].pushableId);
                Assert.AreEqual(new Vector3(4f, 0f, 5f), data.pushables[0].position);
                CollectionAssert.AreEqual(new[] { "m1" }, data.completedMilestoneIds);
                CollectionAssert.AreEqual(new[] { "p1" }, data.solvedPuzzleIds);
                Assert.AreEqual(1, data.operatorCorpses.Count);
                Assert.AreEqual(0, data.operatorCorpses[0].slotIndex);
                Assert.AreEqual("room-1", data.operatorCorpses[0].roomId);
                Assert.AreEqual(new Vector3(9f, 0f, 9f), data.operatorCorpses[0].position);
                CollectionAssert.AreEqual(new[] { 42 }, data.operatorHp);
                Assert.AreEqual(1, data.inventoryItems.Count);
                Assert.AreEqual("weapon-1", data.inventoryItems[0].itemId);
                Assert.AreEqual(7, data.inventoryItems[0].weaponAmmo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
                UnityEngine.Object.DestroyImmediate(weaponData);
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        // Saving at the telegraph happens with its close-up live; the save must name the room
        // shot the player returns to, as a path under the room so it survives a scene reload.
        [Test]
        public void Save_recordsGameplayCameraPath_notTheInspectionShot()
        {
            var roomGo = new GameObject("Room");
            var room   = roomGo.AddComponent<RoomController>();
            var gameplay  = AddCamera(roomGo.transform, "Cameras", "Cam2");
            var telegraph = AddCamera(roomGo.transform, "Telegraph", "TelegraphCam");
            var zones     = new FakeCameraZoneService { GameplayZoneCamera = gameplay, CurrentZoneCamera = telegraph };

            var (controller, saveService, cleanup) = MakeController(room, zones);
            try
            {
                controller.Save(0);
                Assert.AreEqual("Cameras/Cam2", saveService.WrittenData!.cameraPath);
            }
            finally
            {
                cleanup();
                UnityEngine.Object.DestroyImmediate(roomGo);
            }
        }

        [Test]
        public void Save_cameraOutsideCurrentRoom_recordsNoCameraPath()
        {
            var roomGo  = new GameObject("Room");
            var room    = roomGo.AddComponent<RoomController>();
            var otherGo = new GameObject("OtherRoom");
            var stray   = AddCamera(otherGo.transform, "Cameras", "Cam1");
            var zones   = new FakeCameraZoneService { GameplayZoneCamera = stray, CurrentZoneCamera = stray };

            var (controller, saveService, cleanup) = MakeController(room, zones);
            try
            {
                controller.Save(0);
                Assert.AreEqual("", saveService.WrittenData!.cameraPath);
            }
            finally
            {
                cleanup();
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(otherGo);
            }
        }

        private static (SaveController, FakeSaveGameService, Action) MakeController(RoomController room, FakeCameraZoneService zones)
        {
            var world = new WorldStateRegistries(
                new DoorStateRegistry(), new RoomStateRegistry(), new PickupRegistry(),
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry(), new OperatorCorpseRegistry(),
                new ItemSocketStateRegistry(), new RadioCallRegistry(), new PushableStateRegistry(), new MilestoneRegistry(), new PuzzleStateRegistry());
            var playerGo    = new GameObject("Player");
            var player      = playerGo.AddComponent<PlayerController>();
            var view        = MakeView();
            var saveService = new FakeSaveGameService();

            var controller = new SaveController(
                new FakeInputService(), view, saveService, new FakeInventoryService(), new FakeRoster(),
                new FakeRoomOrchestrator { Current = room }, player, world, new PlaytimeTracker(), zones);

            return (controller, saveService, () =>
            {
                UnityEngine.Object.DestroyImmediate(playerGo);
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            });
        }

        private static Unity.Cinemachine.CinemachineCamera AddCamera(Transform room, string folder, string name)
        {
            var parent = new GameObject(folder).transform;
            parent.SetParent(room, false);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<Unity.Cinemachine.CinemachineCamera>();
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
