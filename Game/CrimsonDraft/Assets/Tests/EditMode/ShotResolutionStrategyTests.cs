using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Combat;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class ShotResolutionStrategyTests
    {
        private static BurstPatternData MakePattern(params BurstShotEntry[] entries)
        {
            var pattern = ScriptableObject.CreateInstance<BurstPatternData>();
            pattern.SetShots(entries);
            return pattern;
        }

        [Test]
        public void SingleShotStrategy_noPattern_matchesComputeBulletLocalFromPrimary()
        {
            var strategy = new SingleShotStrategy();
            var primary  = new Vector2(10f, 20f);

            for (int bulletIndex = 0; bulletIndex < 3; bulletIndex++)
            {
                var result   = strategy.GetPelletLocalPositions(Vector2.zero, primary, bulletIndex, 5f, null, pelletCount: 8);
                var expected = AimViewController.ComputeBulletLocalFromPrimary(primary, bulletIndex, 5f);

                Assert.AreEqual(1, result.Length);
                Assert.AreEqual(expected, result[0]);
            }
        }

        [Test]
        public void SingleShotStrategy_withPattern_ignoresPelletCount_returnsExactlyOnePosition()
        {
            var pattern = MakePattern(
                new BurstShotEntry { center = Vector2.zero, semiAxisX = 10f, semiAxisY = 10f },
                new BurstShotEntry { center = Vector2.zero, semiAxisX = 10f, semiAxisY = 10f },
                new BurstShotEntry { center = Vector2.zero, semiAxisX = 10f, semiAxisY = 10f });
            var strategy = new SingleShotStrategy();

            var result = strategy.GetPelletLocalPositions(Vector2.zero, Vector2.zero, 1, 5f, pattern, pelletCount: 8);

            Assert.AreEqual(1, result.Length);
        }

        [Test]
        public void PelletSpreadStrategy_withPattern_returnsOnePositionPerRequestedPellet()
        {
            var pattern = MakePattern(
                new BurstShotEntry { center = Vector2.zero, semiAxisX = 10f, semiAxisY = 10f });
            var strategy = new PelletSpreadStrategy();

            var result = strategy.GetPelletLocalPositions(Vector2.zero, Vector2.zero, 0, 5f, pattern, pelletCount: 8);

            Assert.AreEqual(8, result.Length);
        }

        [Test]
        public void PelletSpreadStrategy_usesEllipseIndexedByBulletIndex_notPelletCount()
        {
            // Two ellipses (one per possible bullet), each should still fan out into
            // `pelletCount` pellets - the entry count indexes bullets, not pellet quantity.
            var pattern = MakePattern(
                new BurstShotEntry { center = new Vector2(-50f, 0f), semiAxisX = 5f, semiAxisY = 5f },
                new BurstShotEntry { center = new Vector2(50f, 0f), semiAxisX = 5f, semiAxisY = 5f });
            var strategy = new PelletSpreadStrategy();

            var bulletZeroPellets = strategy.GetPelletLocalPositions(Vector2.zero, Vector2.zero, 0, 5f, pattern, pelletCount: 4);
            var bulletOnePellets  = strategy.GetPelletLocalPositions(Vector2.zero, Vector2.zero, 1, 5f, pattern, pelletCount: 4);

            Assert.AreEqual(4, bulletZeroPellets.Length);
            Assert.AreEqual(4, bulletOnePellets.Length);

            foreach (var pos in bulletZeroPellets)
                Assert.Less(pos.x, 0f, "Bullet 0's pellets should scatter around its own ellipse (centered at x=-50).");
            foreach (var pos in bulletOnePellets)
                Assert.Greater(pos.x, 0f, "Bullet 1's pellets should scatter around its own ellipse (centered at x=50).");
        }

        [Test]
        public void PelletSpreadStrategy_noPattern_fallsBackToStackedSinglePosition()
        {
            var strategy = new PelletSpreadStrategy();
            var primary  = new Vector2(10f, 20f);

            var result = strategy.GetPelletLocalPositions(Vector2.zero, primary, 0, 5f, null, pelletCount: 8);

            Assert.AreEqual(8, result.Length);
            var expected = AimViewController.ComputeBulletLocalFromPrimary(primary, 0, 5f);
            foreach (var pos in result)
                Assert.AreEqual(expected, pos);
        }

        [Test]
        public void PelletSpreadStrategy_emptyPattern_fallsBackToStackedSinglePosition()
        {
            var pattern  = MakePattern();
            var strategy = new PelletSpreadStrategy();

            var result = strategy.GetPelletLocalPositions(Vector2.zero, Vector2.zero, 0, 5f, pattern, pelletCount: 5);

            Assert.AreEqual(5, result.Length);
        }

        [Test]
        public void PelletSpreadStrategy_positions_fallWithinEllipseBounds()
        {
            var entry = new BurstShotEntry { center = new Vector2(3f, -4f), semiAxisX = 20f, semiAxisY = 30f };
            var pattern = MakePattern(entry);
            var strategy = new PelletSpreadStrategy();
            var confirmedLocalPos = new Vector2(100f, 50f);

            var result = strategy.GetPelletLocalPositions(confirmedLocalPos, Vector2.zero, 0, 5f, pattern, pelletCount: 50);
            Assert.AreEqual(50, result.Length);

            foreach (var pellet in result)
            {
                Vector2 offsetFromEllipseCenter = pellet - confirmedLocalPos - entry.center;
                float normalizedMagnitude = Mathf.Sqrt(
                    Mathf.Pow(offsetFromEllipseCenter.x / entry.semiAxisX, 2f) +
                    Mathf.Pow(offsetFromEllipseCenter.y / entry.semiAxisY, 2f));

                Assert.LessOrEqual(normalizedMagnitude, 1.01f, "Pellet position must land within its ellipse (allowing rounding slack).");
            }
        }

        [Test]
        public void CountBullets_singleShotArray_equalsLength()
        {
            var shots = new[]
            {
                new ResolvedShot(0, 0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20),
                new ResolvedShot(1, 1, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20),
                new ResolvedShot(2, 2, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20),
            };

            Assert.AreEqual(3, AimViewController.CountBullets(shots));
        }

        [Test]
        public void CountBullets_pelletArray_equalsMaxBulletIndexPlusOne()
        {
            var shots = new[]
            {
                new ResolvedShot(0, 0, Vector2.zero, ShotZone.Torso, ShotPrecision.Normal, 20),
                new ResolvedShot(1, 0, Vector2.zero, ShotZone.Head, ShotPrecision.Normal, 40),
                new ResolvedShot(2, 0, Vector2.zero, ShotZone.Miss, ShotPrecision.Normal, 0),
                new ResolvedShot(3, 1, Vector2.zero, ShotZone.Legs, ShotPrecision.Normal, 16),
                new ResolvedShot(4, 1, Vector2.zero, ShotZone.Miss, ShotPrecision.Normal, 0),
            };

            Assert.AreEqual(2, AimViewController.CountBullets(shots));
        }

        [Test]
        public void CountBullets_emptyArray_returnsOne()
        {
            Assert.AreEqual(1, AimViewController.CountBullets(System.Array.Empty<ResolvedShot>()));
        }
    }
}
