#nullable enable

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class SilhouetteOverlayTests
    {
        private TestSprites sprites = null!;

        [SetUp]
        public void SetUp() => this.sprites = new TestSprites();

        [TearDown]
        public void TearDown() => this.sprites.Dispose();

        private static SilhouetteOverlay MakeOverlay(
            Sprite? visible, Sprite? mask, Sprite? staggeredVisible = null, Sprite? staggeredMask = null)
        {
            var overlay = ScriptableObject.CreateInstance<SilhouetteOverlay>();
            SetField(overlay, "visibleSprite", visible);
            SetField(overlay, "maskSprite", mask);
            SetField(overlay, "staggeredVisibleSprite", staggeredVisible);
            SetField(overlay, "staggeredMaskSprite", staggeredMask);
            return overlay;
        }

        private static void SetField(object target, string name, object? value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, name);
            field!.SetValue(target, value);
        }

        // ── SilhouetteOverlay.Resolve ──

        [Test]
        public void Resolve_standing_returnsStandingPair()
        {
            Sprite visible = this.sprites.Solid(Color.cyan);
            Sprite mask    = this.sprites.Solid(Color.white);
            var overlay    = MakeOverlay(visible, mask);

            ActiveOverlay? result = overlay.Resolve(staggered: false);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(OverlayKind.Armor, result!.Value.Kind);
            Assert.AreSame(visible, result.Value.VisibleSprite);
            Assert.AreSame(mask, result.Value.MaskSprite);
        }

        [Test]
        public void Resolve_staggered_withSprites_returnsStaggeredPair()
        {
            Sprite stVisible = this.sprites.Solid(Color.blue);
            Sprite stMask    = this.sprites.Solid(Color.white);
            var overlay      = MakeOverlay(this.sprites.Solid(Color.cyan), this.sprites.Solid(Color.white), stVisible, stMask);

            ActiveOverlay? result = overlay.Resolve(staggered: true);

            Assert.IsTrue(result.HasValue);
            Assert.AreSame(stVisible, result!.Value.VisibleSprite);
            Assert.AreSame(stMask, result.Value.MaskSprite);
        }

        [Test]
        public void Resolve_staggered_withoutSprites_returnsNull()
        {
            var overlay = MakeOverlay(this.sprites.Solid(Color.cyan), this.sprites.Solid(Color.white));
            Assert.IsFalse(overlay.Resolve(staggered: true).HasValue);
        }

        [Test]
        public void Resolve_standing_missingSprites_returnsNull()
        {
            var overlay = MakeOverlay(visible: null, mask: null);
            Assert.IsFalse(overlay.Resolve(staggered: false).HasValue);
        }

        // ── OverlayPicker.Pick ──

        private sealed class FixedRandom : IRandomSource
        {
            public int Value;
            public int LastMaxExclusive = -1;
            public float NextFloat01() => 0f;
            public int NextInt(int minInclusive, int maxExclusive)
            {
                this.LastMaxExclusive = maxExclusive;
                return this.Value;
            }
        }

        [Test]
        public void Pick_emptyPool_returnsNull() =>
            Assert.IsNull(OverlayPicker.Pick(new SilhouetteOverlay?[0], new FixedRandom()));

        [Test]
        public void Pick_nullPool_returnsNull() =>
            Assert.IsNull(OverlayPicker.Pick(null, new FixedRandom()));

        [Test]
        public void Pick_allNull_returnsNull() =>
            Assert.IsNull(OverlayPicker.Pick(new SilhouetteOverlay?[] { null, null }, new FixedRandom()));

        [Test]
        public void Pick_singleEntry_returnsIt()
        {
            var vest = MakeOverlay(null, null);
            Assert.AreSame(vest, OverlayPicker.Pick(new SilhouetteOverlay?[] { vest }, new FixedRandom()));
        }

        [Test]
        public void Pick_skipsNullEntries()
        {
            var a      = MakeOverlay(null, null);
            var b      = MakeOverlay(null, null);
            var random = new FixedRandom { Value = 1 };

            var result = OverlayPicker.Pick(new SilhouetteOverlay?[] { a, null, b }, random);

            Assert.AreSame(b, result);
            Assert.AreEqual(2, random.LastMaxExclusive); // rolled over the 2 real candidates only
        }

        // ── OverlayCoverage.IsCovered ──

        [Test]
        public void IsCovered_opaquePixel_returnsTrue() =>
            Assert.IsTrue(OverlayCoverage.IsCovered(this.sprites.Solid(Color.white), 0.5f, 0.5f));

        [Test]
        public void IsCovered_transparentPixel_returnsFalse() =>
            Assert.IsFalse(OverlayCoverage.IsCovered(this.sprites.Solid(new Color(1f, 1f, 1f, 0f)), 0.5f, 0.5f));

        [Test]
        public void IsCovered_samplesTheRequestedUv()
        {
            Sprite mask = this.sprites.LeftHalf(Color.white, new Color(0f, 0f, 0f, 0f));
            Assert.IsTrue(OverlayCoverage.IsCovered(mask, 0.1f, 0.5f));
            Assert.IsFalse(OverlayCoverage.IsCovered(mask, 0.9f, 0.5f));
        }

        [Test]
        public void IsCovered_opaqueBlackPixel_returnsFalse() =>
            Assert.IsFalse(OverlayCoverage.IsCovered(this.sprites.Solid(Color.black), 0.5f, 0.5f));

        [Test]
        public void IsCovered_opaqueDarkGreyPixel_returnsFalse() =>
            Assert.IsFalse(OverlayCoverage.IsCovered(this.sprites.Solid(new Color(0.3f, 0.3f, 0.3f, 1f)), 0.5f, 0.5f));

        [Test]
        public void IsCovered_whiteOnBlackMask_samplesTheRequestedUv()
        {
            Sprite mask = this.sprites.LeftHalf(Color.white, Color.black);
            Assert.IsTrue(OverlayCoverage.IsCovered(mask, 0.1f, 0.5f));
            Assert.IsFalse(OverlayCoverage.IsCovered(mask, 0.9f, 0.5f));
        }

        [Test]
        public void IsCovered_nonReadableTexture_returnsFalse() =>
            Assert.IsFalse(OverlayCoverage.IsCovered(this.sprites.Solid(Color.white, readable: false), 0.5f, 0.5f));

        // ── EnemyData ──

        [Test]
        public void EnemyData_defaultOverlayPool_isEmptyNotNull()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyData>();
            Assert.NotNull(enemy.OverlayPool);
            Assert.AreEqual(0, enemy.OverlayPool.Length);
            Object.DestroyImmediate(enemy);
        }
    }
}
