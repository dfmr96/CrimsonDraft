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
            public bool TryStashPendingLoad(int slot, out string sceneName) { sceneName = string.Empty; return false; }
            public SaveGameData? ConsumePendingLoad()
            {
                var data = this.PendingLoad;
                this.PendingLoad = null;
                return data;
            }
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
            public string? ActivatedCameraPath { get; private set; }
            // Where the player stood when the room was activated -- the room picks its fallback
            // shot from that position, so it must already be the saved one.
            public Transform? Player;
            public Vector3? PlayerPositionAtActivation { get; private set; }
            public RoomController? CurrentRoom => null;
            public UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab, Vector3 departurePosition) => UniTask.CompletedTask;
            public void TeleportToRoom(RoomController room) { }
            public void ActivateRoomImmediate(string roomId, string? cameraPath = null)
            {
                this.ActivatedRoomId            = roomId;
                this.ActivatedCameraPath        = cameraPath;
                this.PlayerPositionAtActivation = this.Player != null ? this.Player.position : null;
            }
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
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry(), new OperatorCorpseRegistry(),
                new ItemSocketStateRegistry(), new RadioCallRegistry(), new PushableStateRegistry(), new MilestoneRegistry(), new PuzzleStateRegistry());
            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            try
            {
                var loader = new SaveGameLoader(saveService, inventory, roster, roomOrch, player, itemDb, world, new PlaytimeTracker());
                ((IInitializable)loader).Initialize();

                Assert.IsNull(inventory.RestoredEntries);
                Assert.IsNull(roomOrch.ActivatedRoomId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerGo);
                UnityEngine.Object.DestroyImmediate(itemDb);
            }
        }

        // A save written before milestones existed has neither list: both load empty.
        [Test]
        public void Initialize_withSaveFromBeforeMilestones_loadsEmptyLists()
        {
            var saveService = new FakeSaveGameService
            {
                PendingLoad = JsonUtility.FromJson<SaveGameData>("{\"sceneName\":\"Deck_B\",\"roomId\":\"room-1\"}"),
            };
            var itemDb = MakeDatabase();
            var world  = new WorldStateRegistries(
                new DoorStateRegistry(), new RoomStateRegistry(), new PickupRegistry(),
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry(), new OperatorCorpseRegistry(),
                new ItemSocketStateRegistry(), new RadioCallRegistry(), new PushableStateRegistry(),
                new MilestoneRegistry(), new PuzzleStateRegistry());
            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            try
            {
                var loader = new SaveGameLoader(saveService, new FakeInventoryService(), new FakeRoster(),
                    new FakeRoomOrchestrator(), player, itemDb, world, new PlaytimeTracker());

                Assert.DoesNotThrow(() => ((IInitializable)loader).Initialize());
                Assert.AreEqual(0, world.Milestones.CompletedIds.Count);
                Assert.AreEqual(0, world.Puzzles.SolvedIds.Count);
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
                    cameraPath     = "Cameras/Cam2",
                    playerPosition = new Vector3(1f, 2f, 3f),
                    playerRotation = Quaternion.identity,
                    doors             = new List<DoorStateEntry> { new DoorStateEntry { doorId = "door-1", state = DoorMapState.Unlocked } },
                    rooms             = new List<RoomStateEntry> { new RoomStateEntry { roomId = "room-1", state = RoomMapState.Visited } },
                    collectedPickupIds = new List<string> { "pickup-1" },
                    readNoteIds        = new List<string> { "note-1" },
                    knownMapIds        = new List<string> { "map-1" },
                    defeatedEnemyIds   = new List<string> { "enemy-1", "enemy-2" },
                    enemyCorpses       = new List<EnemyCorpseEntry>
                    {
                        new EnemyCorpseEntry { enemyId = "enemy-2", position = new Vector3(3f, 0f, 7f), rotation = Quaternion.identity },
                    },
                    revivedEnemyIds    = new List<string> { "enemy-3" },
                    itemSockets        = new List<ItemSocketStateEntry>
                    {
                        new ItemSocketStateEntry { socketId = "socket-1", inserted = new[] { true, false } },
                    },
                    operatorCorpses   = new List<OperatorCorpseEntry>
                    {
                        new OperatorCorpseEntry { slotIndex = 0, roomId = "room-2", position = new Vector3(1f, 0f, 1f), rotation = Quaternion.identity },
                    },
                    pushables          = new List<PushableStateEntry>
                    {
                        new PushableStateEntry { pushableId = "crate-1", position = new Vector3(4f, 0f, 5f) },
                    },
                    completedMilestoneIds = new List<string> { "m1" },
                    solvedPuzzleIds       = new List<string> { "p1" },
                    operatorHp         = new[] { 80 },
                    inventorySlots     = new List<InventorySlotEntry>
                    {
                        new InventorySlotEntry { slotIndex = 0, itemId = "key-1", keyUsesRemaining = 1 },
                    },
                },
            };
            var inventory = new FakeInventoryService();
            var roster    = new FakeRoster();
            var roomOrch  = new FakeRoomOrchestrator();
            var world = new WorldStateRegistries(
                new DoorStateRegistry(), new RoomStateRegistry(), new PickupRegistry(),
                new NoteRegistry(), new KnownMapsRegistry(), new EnemyStateRegistry(), new OperatorCorpseRegistry(),
                new ItemSocketStateRegistry(), new RadioCallRegistry(), new PushableStateRegistry(), new MilestoneRegistry(), new PuzzleStateRegistry());
            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            roomOrch.Player = player.transform;

            try
            {
                var loader = new SaveGameLoader(saveService, inventory, roster, roomOrch, player, itemDb, world, new PlaytimeTracker());
                ((IInitializable)loader).Initialize();

                Assert.AreEqual("Cameras/Cam2", roomOrch.ActivatedCameraPath);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), roomOrch.PlayerPositionAtActivation,
                    "the player must already stand at the saved spot when the room picks its camera");
                Assert.IsTrue(world.Doors.IsUnlocked("door-1"));
                Assert.AreEqual(RoomMapState.Visited, world.Rooms.GetState("room-1"));
                Assert.IsTrue(world.Pickups.IsCollected("pickup-1"));
                Assert.IsTrue(world.Notes.IsCollected("note-1"));
                Assert.IsTrue(world.KnownMaps.IsKnown("map-1"));
                Assert.IsTrue(world.Enemies.IsDefeated("enemy-1"));
                Assert.IsFalse(world.Enemies.TryGetCorpsePose("enemy-1", out _));
                Assert.IsTrue(world.Enemies.TryGetCorpsePose("enemy-2", out var corpse));
                Assert.AreEqual(new Vector3(3f, 0f, 7f), corpse.Position);
                Assert.IsTrue(world.Enemies.IsRevived("enemy-3"));
                Assert.IsTrue(world.OperatorCorpses.IsRecorded(0));
                CollectionAssert.AreEqual(new[] { true, false }, world.ItemSockets.GetInserted("socket-1"));
                Assert.IsTrue(world.Pushables.TryGet("crate-1", out var cratePosition));
                Assert.AreEqual(new Vector3(4f, 0f, 5f), cratePosition);
                Assert.IsTrue(world.Milestones.IsCompleted("m1"));
                Assert.IsTrue(world.Puzzles.IsSolved("p1"));
                CollectionAssert.AreEqual(new[] { 80 }, roster.RestoredHp);
                Assert.AreEqual("room-2", roomOrch.ActivatedRoomId);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), player.transform.position);

                Assert.IsNotNull(inventory.RestoredEntries);
                Assert.AreEqual("key-1", inventory.RestoredEntries![0].itemId);
                Assert.AreEqual(1, inventory.RestoredEntries[0].keyUsesRemaining);
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
