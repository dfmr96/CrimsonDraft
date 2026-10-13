#nullable enable

using UnityEditor;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapStateResolverTests
    {
        [Test]
        public void IsDeckKnown_whenMapRegistered_returnsTrue()
        {
            var map = ScriptableObject.CreateInstance<MapData>();
            var so = new SerializedObject(map);
            so.FindProperty("sceneName").stringValue = "deck-a";
            so.ApplyModifiedPropertiesWithoutUndo();
            var knownMaps = new KnownMapsRegistry();
            knownMaps.MarkKnown("deck-a");

            Assert.IsTrue(MapStateResolver.IsDeckKnown(map, new RoomStateRegistry(), knownMaps));
            Object.DestroyImmediate(map);
        }
    }
}
