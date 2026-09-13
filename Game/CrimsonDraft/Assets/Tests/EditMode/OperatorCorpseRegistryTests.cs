#nullable enable

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Tests
{
    public sealed class OperatorCorpseRegistryTests
    {
        [Test]
        public void Record_marksSlotAsRecorded()
        {
            var registry = new OperatorCorpseRegistry();
            registry.Record(1, "room-a", new Vector3(1f, 0f, 2f), Quaternion.identity);

            Assert.IsTrue(registry.IsRecorded(1));
        }

        [Test]
        public void Record_calledTwiceForSameSlot_keepsFirstEntry()
        {
            var registry = new OperatorCorpseRegistry();
            registry.Record(1, "room-a", new Vector3(1f, 0f, 0f), Quaternion.identity);
            registry.Record(1, "room-b", new Vector3(9f, 0f, 0f), Quaternion.identity);

            var entry = registry.GetAll().Single(e => e.SlotIndex == 1);
            Assert.AreEqual("room-a", entry.RoomId);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), entry.Position);
        }

        [Test]
        public void LoadState_restoresRecordedSlots()
        {
            var registry = new OperatorCorpseRegistry();
            registry.LoadState(new[]
            {
                new OperatorCorpseRegistry.Entry(2, "room-c", new Vector3(3f, 0f, 4f), Quaternion.identity),
            });

            Assert.IsTrue(registry.IsRecorded(2));
            var entry = registry.GetAll().Single();
            Assert.AreEqual("room-c", entry.RoomId);
        }

        [Test]
        public void ClearAll_removesAllRecordedSlots()
        {
            var registry = new OperatorCorpseRegistry();
            registry.Record(0, "room-a", Vector3.zero, Quaternion.identity);
            registry.ClearAll();

            Assert.IsFalse(registry.IsRecorded(0));
            Assert.AreEqual(0, registry.GetAll().Count);
        }
    }
}
