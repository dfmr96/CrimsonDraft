#nullable enable

using NUnit.Framework;
using CrimsonDraft.Rendering.Blood;

namespace CrimsonDraft.Tests
{
    public sealed class CorpseBloodBlobTests
    {
        [Test]
        public void ScaleAt_beforeDelay_isZero()
            => Assert.AreEqual(0f, CorpseBloodBlob.ScaleAt(1f, 1.5f, 3f));

        [Test]
        public void ScaleAt_halfwayThroughGrowth_isHalf()
            => Assert.AreEqual(0.5f, CorpseBloodBlob.ScaleAt(3f, 1.5f, 3f), 1e-5f);

        [Test]
        public void ScaleAt_afterGrowth_isClampedToOne()
            => Assert.AreEqual(1f, CorpseBloodBlob.ScaleAt(10f, 1.5f, 3f));
    }
}
