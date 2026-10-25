#nullable enable

using NUnit.Framework;
using ColbyO.VNTG.CRT;

namespace CrimsonDraft.Tests
{
    // The CRT keeps the previous frame per camera to emulate refresh rate / interlacing. A camera that
    // was switched off (UI camera gating) must not show that stale frame when it comes back.
    public sealed class CRTHistoryTests
    {
        [Test]
        public void NeverRendered_isStale()
            => Assert.IsTrue(CRTHistory.IsStale(lastRenderedFrame: -1, currentFrame: 100));

        [Test]
        public void RenderedLastFrame_isFresh()
            => Assert.IsFalse(CRTHistory.IsStale(lastRenderedFrame: 99, currentFrame: 100));

        [Test]
        public void RenderedTwiceInTheSameFrame_isFresh()
            => Assert.IsFalse(CRTHistory.IsStale(lastRenderedFrame: 100, currentFrame: 100));

        [Test]
        public void SkippedAFrame_isStale()
            => Assert.IsTrue(CRTHistory.IsStale(lastRenderedFrame: 98, currentFrame: 100));
    }
}
