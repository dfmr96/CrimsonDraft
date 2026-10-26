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
            var operatorCorpses = new OperatorCorpseRegistry();
            var itemSockets = new ItemSocketStateRegistry();
            var world     = new WorldStateRegistries(doors, rooms, pickups, notes, knownMaps, enemies, operatorCorpses, itemSockets, new RadioCallRegistry(), new PushableStateRegistry(), new MilestoneRegistry(), new PuzzleStateRegistry(), new GateRegistry());
            var inventoryState = new InventoryStateRegistry();
            var rosterHealth   = new RosterHealthRegistry();

            doors.SetUnlocked("door-a");
            rooms.MarkVisited("room-a");
            pickups.SetCollected("pickup-a");
            notes.SetCollected("note-a");
            knownMaps.MarkKnown("map-a");
            enemies.SetDefeated("enemy-a");
            world.OperatorCorpses.Record(0, "room-a", UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity);
            itemSockets.SetInserted("socket-a", new[] { true });
            world.Pushables.Record("crate-a", UnityEngine.Vector3.one);
            world.Milestones.Complete("m1");
            world.Puzzles.SetSolved("p1");
            world.Gates.SetCompleted("g1");
            inventoryState.Save(new System.Collections.Generic.List<CrimsonDraft.Infrastructure.Save.InventoryItemEntry>());
            rosterHealth.Save(new[] { 100 });

            var resetter = new GameStateResetter(world, inventoryState, rosterHealth, new PlaytimeTracker());
            resetter.ResetAll();

            Assert.IsFalse(doors.IsUnlocked("door-a"));
            Assert.AreEqual(CrimsonDraft.Infrastructure.RoomMapState.Unknown, rooms.GetState("room-a"));
            Assert.IsFalse(pickups.IsCollected("pickup-a"));
            Assert.IsFalse(notes.IsCollected("note-a"));
            Assert.IsFalse(knownMaps.IsKnown("map-a"));
            Assert.IsFalse(enemies.IsDefeated("enemy-a"));
            Assert.IsFalse(world.OperatorCorpses.IsRecorded(0));
            Assert.AreEqual(0, itemSockets.GetInserted("socket-a").Length);
            Assert.IsFalse(world.Pushables.TryGet("crate-a", out _));
            Assert.IsFalse(world.Milestones.IsCompleted("m1"));
            Assert.IsFalse(world.Puzzles.IsSolved("p1"));
            Assert.IsFalse(world.Gates.IsCompleted("g1"));
            Assert.IsFalse(inventoryState.HasSavedState);
            Assert.IsFalse(rosterHealth.HasSavedState);
        }
    }
}
