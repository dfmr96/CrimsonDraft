#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Tests
{
    public sealed class MilestoneTriggerEvaluatorTests
    {
        internal sealed class FakeWorld : IMilestoneWorldState
        {
            public readonly HashSet<string> Pickups = new(), Items = new(), Sockets = new(), Doors = new(), Notes = new(), Puzzles = new();
            public event Action<MilestoneSource>? SourceChanged;
            public void Raise(MilestoneSource source) => SourceChanged?.Invoke(source);
            public bool IsPickupCollected(string id) => this.Pickups.Contains(id);
            public bool HasItem(string itemId)       => this.Items.Contains(itemId);
            public bool IsSocketComplete(string id)  => this.Sockets.Contains(id);
            public bool IsDoorUnlocked(string id)    => this.Doors.Contains(id);
            public bool IsNoteRead(string id)        => this.Notes.Contains(id);
            public bool IsPuzzleSolved(string id)    => this.Puzzles.Contains(id);
            public void Dispose() { }
        }

        private static MilestoneTrigger T(MilestoneTriggerKind kind, string id = "") => new() { kind = kind, targetId = id };

        [Test]
        public void TextIdKinds_followTheirSource()
        {
            var world = new FakeWorld();
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.PickupCollected, "p"), world));
            world.Pickups.Add("p"); world.Sockets.Add("s"); world.Doors.Add("d"); world.Puzzles.Add("z");
            Assert.IsTrue(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.PickupCollected, "p"), world));
            Assert.IsTrue(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.SocketCompleted, "s"), world));
            Assert.IsTrue(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.DoorUnlocked, "d"), world));
            Assert.IsTrue(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.PuzzleSolved, "z"), world));
        }

        [Test]
        public void ItemHeldAndNoteRead_useTheirAssets()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            var note = ScriptableObject.CreateInstance<DocumentData>();
            try
            {
                var itemSo = new SerializedObject(item); itemSo.FindProperty("itemId").stringValue = "sheep_key"; itemSo.ApplyModifiedPropertiesWithoutUndo();
                var noteSo = new SerializedObject(note); noteSo.FindProperty("noteId").stringValue = "beeper_file"; noteSo.ApplyModifiedPropertiesWithoutUndo();
                var world = new FakeWorld();
                var held = new MilestoneTrigger { kind = MilestoneTriggerKind.ItemHeld, item = item };
                var read = new MilestoneTrigger { kind = MilestoneTriggerKind.NoteRead, note = note };

                Assert.IsFalse(MilestoneTriggerEvaluator.Holds(held, world));
                world.Items.Add("sheep_key"); world.Notes.Add("beeper_file");
                Assert.IsTrue(MilestoneTriggerEvaluator.Holds(held, world));
                Assert.IsTrue(MilestoneTriggerEvaluator.Holds(read, world));
            }
            finally { UnityEngine.Object.DestroyImmediate(item); UnityEngine.Object.DestroyImmediate(note); }
        }

        // Review Focus 4: empty / unassigned targets never hold and never throw.
        [Test]
        public void MissingTargets_neverHold()
        {
            var world = new FakeWorld();
            world.Pickups.Add("");
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.None), world));
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(T(MilestoneTriggerKind.PickupCollected, ""), world));
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(new MilestoneTrigger { kind = MilestoneTriggerKind.ItemHeld }, world));
            Assert.IsFalse(MilestoneTriggerEvaluator.Holds(new MilestoneTrigger { kind = MilestoneTriggerKind.NoteRead }, world));
        }

        [Test]
        public void SourceOf_mapsEveryKind()
        {
            Assert.AreEqual(MilestoneSource.Pickups,   MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.PickupCollected));
            Assert.AreEqual(MilestoneSource.Inventory, MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.ItemHeld));
            Assert.AreEqual(MilestoneSource.Sockets,   MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.SocketCompleted));
            Assert.AreEqual(MilestoneSource.Doors,     MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.DoorUnlocked));
            Assert.AreEqual(MilestoneSource.Notes,     MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.NoteRead));
            Assert.AreEqual(MilestoneSource.Puzzles,   MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.PuzzleSolved));
            Assert.AreEqual(MilestoneSource.None,      MilestoneTriggerEvaluator.SourceOf(MilestoneTriggerKind.None));
        }
    }
}
