#nullable enable

using NUnit.Framework;
using CrimsonDraft.Navigation;

namespace CrimsonDraft.Tests
{
    public sealed class CorpseProximityTrackerTests
    {
        [Test]
        public void CanAccess_initially_false()
        {
            Assert.IsFalse(new CorpseProximityTracker().CanAccess(1));
        }

        [Test]
        public void Enter_grantsAccessToThatSlotOnly()
        {
            var tracker = new CorpseProximityTracker();
            tracker.Enter(1);
            Assert.IsTrue(tracker.CanAccess(1));
            Assert.IsFalse(tracker.CanAccess(2));
        }

        [Test]
        public void Exit_revokesAccess()
        {
            var tracker = new CorpseProximityTracker();
            tracker.Enter(1);
            tracker.Exit(1);
            Assert.IsFalse(tracker.CanAccess(1));
        }

        [Test]
        public void TwoCorpses_areIndependent()
        {
            var tracker = new CorpseProximityTracker();
            tracker.Enter(1);
            tracker.Enter(2);
            tracker.Exit(1);
            Assert.IsFalse(tracker.CanAccess(1));
            Assert.IsTrue(tracker.CanAccess(2));
        }

        [Test]
        public void Exit_withoutEnter_isNoOp()
        {
            var tracker = new CorpseProximityTracker();
            Assert.DoesNotThrow(() => tracker.Exit(3));
            Assert.IsFalse(tracker.CanAccess(3));
        }
    }
}
