#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Save;

namespace CrimsonDraft.Tests
{
    public sealed class GameStateResetterTests
    {
        [Test]
        public void ResetAll_clearsEveryRegistry()
        {
            var doors     = new DoorStateRegistry();
            var rooms     = new RoomStateRegistry();
            var pickups   = new PickupRegistry();
            var notes     = new NoteRegistry();
            var knownMaps = new KnownMapsRegistry();
            var enemies   = new EnemyStateRegistry();
            var world     = new WorldStateRegistries(doors, rooms, pickups, notes, knownMaps, enemies);
            var inventoryState = new InventoryStateRegistry();
            var rosterHealth   = new RosterHealthRegistry();

            doors.SetUnlocked("door-a");
            rooms.MarkVisited("room-a");
            pickups.SetCollected("pickup-a");
            notes.SetCollected("note-a");
            knownMaps.MarkKnown("map-a");
            enemies.SetDefeated("enemy-a");
            inventoryState.Save(new object());
            rosterHealth.Save(new[] { 100 });

            var resetter = new GameStateResetter(world, inventoryState, rosterHealth);
            resetter.ResetAll();

            Assert.IsFalse(doors.IsUnlocked("door-a"));
            Assert.AreEqual(CrimsonDraft.Infrastructure.RoomMapState.Unknown, rooms.GetState("room-a"));
            Assert.IsFalse(pickups.IsCollected("pickup-a"));
            Assert.IsFalse(notes.IsCollected("note-a"));
            Assert.IsFalse(knownMaps.IsKnown("map-a"));
            Assert.IsFalse(enemies.IsDefeated("enemy-a"));
            Assert.IsFalse(inventoryState.HasSavedState);
            Assert.IsFalse(rosterHealth.HasSavedState);
        }
    }
}
