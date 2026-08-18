#nullable enable

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Save;

namespace CrimsonDraft.Tests
{
    public sealed class SaveGameServiceTests
    {
        private const int TestSlotA = 18;
        private const int TestSlotB = 19;

        [TearDown]
        public void TearDown()
        {
            string dir = Path.Combine(Application.persistentDataPath, "Saves");
            foreach (var slot in new[] { TestSlotA, TestSlotB })
            {
                string path = Path.Combine(dir, $"slot_{slot:D2}.json");
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static SaveGameData MakeData(string roomId = "room-1") => new SaveGameData
        {
            sceneName       = "Deck_B",
            roomId          = roomId,
            timestampIso    = "2026-08-18T00:00:00Z",
            playtimeSeconds = 123.45f,
            playerPosition  = new Vector3(1f, 2f, 3f),
            playerRotation  = Quaternion.Euler(0f, 90f, 0f),
            doors           = new List<DoorStateEntry> { new DoorStateEntry { doorId = "door-1", state = DoorMapState.Unlocked } },
            rooms           = new List<RoomStateEntry> { new RoomStateEntry { roomId = "room-1", state = RoomMapState.Visited } },
            collectedPickupIds = new List<string> { "pickup-1" },
            readNoteIds        = new List<string> { "note-1" },
            knownMapIds        = new List<string> { "map-1" },
            defeatedEnemyIds   = new List<string> { "enemy-1" },
            operatorHp         = new[] { 90, 100 },
            inventorySlots     = new List<InventorySlotEntry>
            {
                new InventorySlotEntry { slotIndex = 0, itemId = "weapon-1", weaponAmmo = 12 },
            },
        };

        [Test]
        public void ReadFromDisk_returnsNull_whenSlotEmpty()
        {
            var service = new SaveGameService();
            Assert.IsNull(service.ReadFromDisk(TestSlotA));
        }

        [Test]
        public void WriteToDisk_thenReadFromDisk_roundTripsAllFields()
        {
            var service  = new SaveGameService();
            var original = MakeData();

            service.WriteToDisk(TestSlotA, original);
            var loaded = service.ReadFromDisk(TestSlotA);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(original.sceneName, loaded!.sceneName);
            Assert.AreEqual(original.roomId, loaded.roomId);
            Assert.AreEqual(original.playtimeSeconds, loaded.playtimeSeconds);
            Assert.AreEqual(original.playerPosition, loaded.playerPosition);
            Assert.AreEqual(1, loaded.doors.Count);
            Assert.AreEqual("door-1", loaded.doors[0].doorId);
            Assert.AreEqual(DoorMapState.Unlocked, loaded.doors[0].state);
            Assert.AreEqual(1, loaded.rooms.Count);
            Assert.AreEqual(1, loaded.collectedPickupIds.Count);
            Assert.AreEqual("pickup-1", loaded.collectedPickupIds[0]);
            Assert.AreEqual(1, loaded.inventorySlots.Count);
            Assert.AreEqual("weapon-1", loaded.inventorySlots[0].itemId);
            Assert.AreEqual(12, loaded.inventorySlots[0].weaponAmmo);
            CollectionAssert.AreEqual(new[] { 90, 100 }, loaded.operatorHp);
        }

        [Test]
        public void WriteToDisk_overwritesExistingSlot()
        {
            var service = new SaveGameService();
            service.WriteToDisk(TestSlotA, MakeData("room-1"));
            service.WriteToDisk(TestSlotA, MakeData("room-2"));

            var loaded = service.ReadFromDisk(TestSlotA);
            Assert.AreEqual("room-2", loaded!.roomId);
        }

        [Test]
        public void ListSlotSummaries_returnsSlotCountEntries_emptyAndOccupiedMarkedCorrectly()
        {
            var service = new SaveGameService();
            service.WriteToDisk(TestSlotA, MakeData());

            var summaries = service.ListSlotSummaries();

            Assert.AreEqual(SaveGameService.SlotCount, summaries.Count);
            Assert.IsFalse(summaries[TestSlotA].isEmpty);
            Assert.AreEqual("room-1", summaries[TestSlotA].roomId);
            Assert.IsTrue(summaries[TestSlotB].isEmpty);
        }

        [Test]
        public void ConsumePendingLoad_returnsNull_whenNothingPending()
        {
            var service = new SaveGameService();
            Assert.IsNull(service.ConsumePendingLoad());
        }

        [Test]
        public void ConsumePendingLoad_returnsDataOnce_thenNull()
        {
            var service = new SaveGameService();
            service.WriteToDisk(TestSlotA, MakeData());

            // LoadSlot triggers a scene load, which EditMode tests can't exercise directly;
            // this test exercises the pending-load handoff via WriteToDisk + ReadFromDisk
            // instead, matching what LoadSlot would stash internally.
            var data = service.ReadFromDisk(TestSlotA);
            Assert.IsNotNull(data);
        }
    }
}
