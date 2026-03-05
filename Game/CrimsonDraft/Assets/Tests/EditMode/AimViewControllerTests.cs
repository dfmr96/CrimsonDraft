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

        [Test]
        public void MapUvToTexturePixel_usesSpriteTextureRect()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.SetPixels(new[]
            {
                Color.black, Color.black, Color.black, Color.black,
                Color.black, Color.red,   Color.green, Color.black,
                Color.black, Color.blue,  Color.white, Color.black,
                Color.black, Color.black, Color.black, Color.black,
            });
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(1f, 1f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
            var min = AimViewController.MapUvToTexturePixel(sprite, 0f, 0f);
            var max = AimViewController.MapUvToTexturePixel(sprite, 1f, 1f);

            Assert.AreEqual(new Vector2Int(1, 1), min);
            Assert.AreEqual(new Vector2Int(2, 2), max);
            Assert.AreEqual(Color.red, tex.GetPixel(min.x, min.y));
            Assert.AreEqual(Color.white, tex.GetPixel(max.x, max.y));

            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(tex);
        }

        [Test]
        public void AimHitMaskProfile_defaults_areSafe()
        {
            var profile = ScriptableObject.CreateInstance<AimHitMaskProfile>();
            Assert.NotNull(profile.ZoneDefinitions);
            Assert.AreEqual(0, profile.ZoneDefinitions.Length);
            Assert.AreEqual(0.1f, profile.ColorTolerance);

            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void ComputeBulletLocalFromPrimary_indexZero_hasNoYOffset()
        {
            var result = AimViewController.ComputeBulletLocalFromPrimary(new Vector2(10f, 20f), 0, 5f);
            Assert.AreEqual(new Vector2(10f, 20f), result);
        }

        [Test]
        public void ComputeBulletLocalFromPrimary_indexOne_addsFiveY()
        {
            var result = AimViewController.ComputeBulletLocalFromPrimary(new Vector2(10f, 20f), 1, 5f);
            Assert.AreEqual(new Vector2(10f, 25f), result);
        }

        [Test]
        public void ComputeBulletLocalFromPrimary_indexTwo_addsTenY()
        {
            var result = AimViewController.ComputeBulletLocalFromPrimary(new Vector2(10f, 20f), 2, 5f);
            Assert.AreEqual(new Vector2(10f, 30f), result);
        }
    }
}
