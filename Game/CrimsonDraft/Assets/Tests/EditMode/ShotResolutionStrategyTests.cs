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

        [Test]
        public void SlashStrategy_beginsBeforeTheConfirmedPoint_andRunsPastIt()
        {
            var strategy = new SlashStrategy(lengthPx: 40f, angleDeg: 0f, angleJitterDeg: 0f);
            var aim      = new Vector2(10f, 20f);

            var points = strategy.GetPelletLocalPositions(aim, Vector2.zero, 0, 5f, null, pelletCount: 3);

            // 35% of 40 px = 14 px behind the aim point, 26 px past it.
            Assert.AreEqual(new Vector2(-4f, 20f), points[0]);
            Assert.AreEqual(new Vector2(36f, 20f), points[points.Length - 1]);
            Assert.That(points, Does.Contain(aim), "the aim point itself lies on the stroke");
        }

        [Test]
        public void SlashStrategy_pointsAreCloserThanAMarker_soTheyReadAsOneTrail()
        {
            var strategy = new SlashStrategy(lengthPx: 60f, angleDeg: 0f, angleJitterDeg: 0f);

            var points = strategy.GetPelletLocalPositions(Vector2.zero, Vector2.zero, 0, 5f, null, pelletCount: 3);

            for (int i = 1; i < points.Length; i++)
                Assert.LessOrEqual(Vector2.Distance(points[i - 1], points[i]), 3f, $"gap between points {i - 1} and {i}");
        }

        [Test]
        public void SlashStrategy_jitterOnlyTiltsTheCutUpOrDown_itNeverReversesIt()
        {
            var strategy = new SlashStrategy(lengthPx: 40f, angleDeg: 0f, angleJitterDeg: 20f);

            for (int run = 0; run < 50; run++)
            {
                var points = strategy.GetPelletLocalPositions(Vector2.zero, Vector2.zero, 0, 5f, null, pelletCount: 3);
                var end    = points[points.Length - 1];

                Assert.Greater(end.x, 20f, "stays a mostly horizontal swing to the right (26 px past the aim point)");
                Assert.LessOrEqual(Mathf.Abs(end.y), 10f, "tilts at most ~20 degrees (26 * sin 20 = 8.9)");
            }
        }

        [Test]
        public void SlashStrategy_ignoresAnyHandAuthoredPattern()
        {
            var pattern  = MakePattern(new BurstShotEntry { center = new Vector2(30f, 30f), semiAxisX = 1f, semiAxisY = 1f });
            var strategy = new SlashStrategy(lengthPx: 20f, angleDeg: 0f, angleJitterDeg: 0f);

            var points = strategy.GetPelletLocalPositions(Vector2.zero, Vector2.zero, 0, 5f, pattern, pelletCount: 3);

            Assert.AreEqual(new Vector2(-7f, 0f), points[0], "20 px * 35% behind the origin, not the pattern's (30, 30)");
            Assert.Greater(points.Length, 3, "dense row, not the pattern's one entry");
        }

        private static ResolvedShot Shot(int index, ShotZone zone, int damage) =>
            new ResolvedShot(index, 0, Vector2.zero, zone, ShotPrecision.Normal, damage);

        [Test]
        public void ApplyFixedSlashDamage_onlyTheFirstBodyPointDealsDamage_andItIsOne()
        {
            var shots = new System.Collections.Generic.List<ResolvedShot>
            {
                Shot(0, ShotZone.Miss, 0),
                Shot(1, ShotZone.Torso, 7),
                Shot(2, ShotZone.Head, 9),
                Shot(3, ShotZone.Torso, 7),
            };

            AimViewController.ApplyFixedSlashDamage(shots);

            int total = 0;
            foreach (var s in shots) total += s.Damage;
            Assert.AreEqual(AimViewController.SlashFixedDamage, total);
            Assert.AreEqual(0, shots[0].Damage);
            Assert.AreEqual(1, shots[1].Damage);
            Assert.AreEqual(ShotZone.Head, shots[2].Zone, "zones are kept (a miss still marks the cut as leaving the body)");
        }

        [Test]
        public void ApplyFixedSlashDamage_whenNoPointTouchesTheBody_dealsNoDamage()
        {
            var shots = new System.Collections.Generic.List<ResolvedShot> { Shot(0, ShotZone.Miss, 0), Shot(1, ShotZone.Miss, 0) };

            AimViewController.ApplyFixedSlashDamage(shots);

            Assert.AreEqual(0, shots[0].Damage + shots[1].Damage);
        }
    }
}
