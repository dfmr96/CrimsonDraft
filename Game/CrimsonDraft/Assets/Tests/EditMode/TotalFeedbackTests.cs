#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class TotalFeedbackTests
    {
        private static ResolvedShot Shot(ShotZone zone, int damage, int index = 0) =>
            new ResolvedShot(index, index, Vector2.zero, zone, ShotPrecision.Normal, damage);

        [Test]
        public void Format_noShots_returnsNull() =>
            Assert.IsNull(TotalFeedback.Format(new ResolvedShot[0]));

        [Test]
        public void Format_singleShot_returnsNull() =>
            Assert.IsNull(TotalFeedback.Format(new[] { Shot(ShotZone.Torso, 20) }));

        [Test]
        public void Format_allMiss_returnsMiss() =>
            Assert.AreEqual("MISS", TotalFeedback.Format(new[] { Shot(ShotZone.Miss, 0, 0), Shot(ShotZone.Miss, 0, 1) }));

        [Test]
        public void Format_mixed_returnsNegativeSumOfDamage() =>
            Assert.AreEqual("-25", TotalFeedback.Format(new[]
            {
                Shot(ShotZone.Torso, 20, 0),
                Shot(ShotZone.Miss, 0, 1),
                Shot(ShotZone.Arms, 5, 2),
            }));

        [Test]
        public void Format_hitsForZeroDamage_stillShowsTotalNotMiss() =>
            Assert.AreEqual("-0", TotalFeedback.Format(new[] { Shot(ShotZone.Legs, 0, 0), Shot(ShotZone.Legs, 0, 1) }));
    }
}
