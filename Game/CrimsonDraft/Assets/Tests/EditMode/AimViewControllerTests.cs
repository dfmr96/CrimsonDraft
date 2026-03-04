using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class AimViewControllerTests
    {
        private static ShotZoneDefinition[] StandardPalette() => new[]
        {
            new ShotZoneDefinition { color = Color.white, zone = ShotZone.Hit  },
            new ShotZoneDefinition { color = Color.black, zone = ShotZone.Miss },
        };

        [Test]
        public void ResolveZone_exactWhite_returnsHit()
        {
            var result = AimViewController.ResolveZone(Color.white, StandardPalette(), 0.1f);
            Assert.AreEqual(ShotZone.Hit, result);
        }

        [Test]
        public void ResolveZone_exactBlack_returnsMiss()
        {
            var result = AimViewController.ResolveZone(Color.black, StandardPalette(), 0.1f);
            Assert.AreEqual(ShotZone.Miss, result);
        }

        [Test]
        public void ResolveZone_nearWhite_withinTolerance_returnsHit()
        {
            var nearWhite = new Color(0.95f, 0.95f, 0.95f);
            var result = AimViewController.ResolveZone(nearWhite, StandardPalette(), 0.1f);
            Assert.AreEqual(ShotZone.Hit, result);
        }

        [Test]
        public void ResolveZone_unknownColor_outsideTolerance_returnsMiss()
        {
            var result = AimViewController.ResolveZone(Color.red, StandardPalette(), 0.1f);
            Assert.AreEqual(ShotZone.Miss, result);
        }

        [Test]
        public void ResolveZone_emptyPalette_returnsMiss()
        {
            var result = AimViewController.ResolveZone(Color.white, new ShotZoneDefinition[0], 0.1f);
            Assert.AreEqual(ShotZone.Miss, result);
        }
    }
}
