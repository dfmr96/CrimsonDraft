#nullable enable

using NUnit.Framework;
using CrimsonDraft.Navigation;

namespace CrimsonDraft.Tests
{
    public sealed class DebugRoomTeleportTests
    {
        [Test]
        public void IsSameScene_sameName()
            => Assert.IsTrue(DebugRoomTeleport.IsSameScene("Deck_B_Development", "Deck_B_Development"));

        [Test]
        public void IsSameScene_otherDeck()
            => Assert.IsFalse(DebugRoomTeleport.IsSameScene("Deck_B_Development", "Deck_C_Development"));
    }
}
