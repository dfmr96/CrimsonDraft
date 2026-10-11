#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapSpriteStandardTests
    {
        [Test]
        public void Majority_picksMostCommonPpuAndFilter()
        {
            var standard = MapSpriteStandard.Majority(new[]
            {
                MapTestData.Sprite(ppu: 16f, filter: FilterMode.Point),
                MapTestData.Sprite(ppu: 16f, filter: FilterMode.Point),
                MapTestData.Sprite(ppu: 100f, filter: FilterMode.Bilinear),
            });
            Assert.AreEqual((16f, FilterMode.Point), standard);
        }

        [Test]
        public void Majority_ignoresNulls_andIsNullWhenEmpty()
        {
            Assert.IsNull(MapSpriteStandard.Majority(new Sprite?[] { null }));
        }

        [Test]
        public void Matches_comparesPpuAndFilter()
        {
            var sprite = MapTestData.Sprite(ppu: 16f, filter: FilterMode.Point);
            Assert.IsTrue(MapSpriteStandard.Matches(sprite, (16f, FilterMode.Point)));
            Assert.IsFalse(MapSpriteStandard.Matches(sprite, (32f, FilterMode.Point)));
            Assert.IsFalse(MapSpriteStandard.Matches(sprite, (16f, FilterMode.Bilinear)));
        }
    }
}
