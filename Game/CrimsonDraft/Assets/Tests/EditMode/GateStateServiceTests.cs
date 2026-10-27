#nullable enable

using NUnit.Framework;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Gates;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class GateIdsTests
    {
        [Test]
        public void Format_andParse_roundTripEveryKind()
        {
            Assert.IsTrue(GateIds.TryParse(GateIds.Socket("fuse"), out var k1, out var id1));
            Assert.AreEqual((GateKind.Socket, "fuse"), (k1, id1));
            Assert.IsTrue(GateIds.TryParse(GateIds.Puzzle("valves"), out var k2, out var id2));
            Assert.AreEqual((GateKind.Puzzle, "valves"), (k2, id2));
            Assert.IsTrue(GateIds.TryParse(GateIds.Radio("radio_01_adrift"), out var k3, out var id3));
            Assert.AreEqual((GateKind.Radio, "radio_01_adrift"), (k3, id3));
            Assert.IsTrue(GateIds.TryParse(GateIds.Generic("seaman_room_2_safe"), out var k4, out var id4));
            Assert.AreEqual((GateKind.Generic, "seaman_room_2_safe"), (k4, id4));
            Assert.IsTrue(GateIds.TryParse(GateIds.SocketSlot("office_jewelry_box_gems", 1), out var k5, out var id5));
            Assert.AreEqual((GateKind.SocketSlot, "office_jewelry_box_gems#1"), (k5, id5));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("fuse")]           // no kind
        [TestCase("door:d1")]        // not a gate kind
        [TestCase("socket:")]        // kind without id
        public void TryParse_rejectsMalformedIds(string? gateId) =>
            Assert.IsFalse(GateIds.TryParse(gateId, out _, out _));
    }

    public sealed class GateStateServiceTests
    {
        private ItemSocketStateRegistry sockets = null!;
        private PuzzleStateRegistry puzzles = null!;
        private RadioCallRegistry radios = null!;
        private GateRegistry generic = null!;
        private GateStateService gates = null!;
        private int changed;

        [SetUp]
        public void SetUp()
        {
            this.sockets = new ItemSocketStateRegistry();
            this.puzzles = new PuzzleStateRegistry();
            this.radios  = new RadioCallRegistry();
            this.generic = new GateRegistry();
            this.gates   = new GateStateService(this.sockets, this.puzzles, this.radios, this.generic);
            this.changed = 0;
            this.gates.Changed += () => this.changed++;
        }

        [TearDown]
        public void TearDown() => this.gates.Dispose();

        [Test]
        public void Socket_isCompleteOnlyOnceTheSocketRecordedItsCompletion()
        {
            // Every slot filled is not enough on its own: a socket with a valve still has to be turned.
            // The socket itself records completion (ItemSocketInteractable.CompletedKey) when it gets there.
            this.sockets.SetInserted("fuse", new[] { true, true });
            Assert.IsFalse(this.gates.IsCompleted(GateIds.Socket("fuse")));

            this.sockets.SetInserted(ItemSocketInteractable.CompletedKey("fuse"), new[] { true });
            Assert.IsTrue(this.gates.IsCompleted(GateIds.Socket("fuse")));
        }

        [Test]
        public void SocketSlot_isCompleteOnceThatSlotIsFilled_whateverTheOthers()
        {
            // Two-gem socket with only the second gem (the yellow one) in.
            this.sockets.SetInserted("gems", new[] { false, true });

            Assert.IsTrue(this.gates.IsCompleted(GateIds.SocketSlot("gems", 1)));
            Assert.IsFalse(this.gates.IsCompleted(GateIds.SocketSlot("gems", 0)));
            Assert.IsFalse(this.gates.IsCompleted(GateIds.Socket("gems")), "the whole socket still waits for the other gem");
        }

        [TestCase(-1)]
        [TestCase(2)]
        [TestCase(99)]
        public void SocketSlot_outOfRange_isNotComplete(int index)
        {
            this.sockets.SetInserted("gems", new[] { true, true });
            Assert.IsFalse(this.gates.IsCompleted(GateIds.SocketSlot("gems", index)));
        }

        [Test]
        public void SocketSlot_ofAnUnknownSocket_isNotComplete() =>
            Assert.IsFalse(this.gates.IsCompleted(GateIds.SocketSlot("never_seen", 0)));

        [Test]
        public void SocketSlot_malformedIndex_isNotComplete()
        {
            this.sockets.SetInserted("gems", new[] { true });
            Assert.IsFalse(this.gates.IsCompleted("slot:gems"));
            Assert.IsFalse(this.gates.IsCompleted("slot:gems#x"));
            Assert.IsFalse(this.gates.IsCompleted("slot:#0"));
        }

        [Test]
        public void SocketSlot_doesNotMixWithTheSocketsHelperKeys()
        {
            // "<id>#completed" / "<id>#turned" live in the same registry; they are not slots.
            this.sockets.SetInserted(ItemSocketInteractable.CompletedKey("gems"), new[] { true });
            Assert.IsFalse(this.gates.IsCompleted("slot:gems#completed"));
            Assert.IsFalse(this.gates.IsCompleted(GateIds.SocketSlot("gems", 0)));
        }

        [Test]
        public void Puzzle_isCompleteOnceSolved()
        {
            Assert.IsFalse(this.gates.IsCompleted(GateIds.Puzzle("valves")));
            this.puzzles.SetSolved("valves");
            Assert.IsTrue(this.gates.IsCompleted(GateIds.Puzzle("valves")));
        }

        [Test]
        public void Radio_isCompleteOnceHeard()
        {
            Assert.IsFalse(this.gates.IsCompleted(GateIds.Radio("radio_01")));
            this.radios.SetHeard("radio_01");
            Assert.IsTrue(this.gates.IsCompleted(GateIds.Radio("radio_01")));
        }

        [Test]
        public void GenericGate_isCompleteOnceMarked()
        {
            Assert.IsFalse(this.gates.IsCompleted(GateIds.Generic("safe")));
            this.generic.SetCompleted("safe");
            Assert.IsTrue(this.gates.IsCompleted(GateIds.Generic("safe")));
        }

        [Test]
        public void IdsOfDifferentKinds_neverMix()
        {
            this.puzzles.SetSolved("x");
            Assert.IsFalse(this.gates.IsCompleted(GateIds.Radio("x")));
            Assert.IsFalse(this.gates.IsCompleted(GateIds.Socket("x")));
            Assert.IsFalse(this.gates.IsCompleted("x"), "unprefixed ids are not gates");
        }

        [Test]
        public void EveryRegistry_raisesChanged()
        {
            this.sockets.SetInserted("s", new[] { true });
            this.puzzles.SetSolved("p");
            this.radios.SetHeard("r");
            this.generic.SetCompleted("g");
            Assert.AreEqual(4, this.changed);
        }

        [Test]
        public void HearingTheSameRadioTwice_raisesChangedOnce()
        {
            this.radios.SetHeard("r");
            this.radios.SetHeard("r");
            Assert.AreEqual(1, this.changed);
        }

        [Test]
        public void Dispose_stopsForwarding()
        {
            this.gates.Dispose();
            this.puzzles.SetSolved("p");
            Assert.AreEqual(0, this.changed);
        }
    }
}
