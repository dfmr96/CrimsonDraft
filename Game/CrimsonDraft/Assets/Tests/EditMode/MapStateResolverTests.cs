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
        public void ResolveRoom_whenUnknownAndNoMap_returnsHidden()
        {
            Assert.AreEqual(
                MapRoomVisualState.Hidden,
                MapStateResolver.ResolveRoom(hasMap: false, roomState: RoomMapState.Unknown, isCurrentRoom: false, isCompleted: false));
        }

        [Test]
        public void ResolveRoom_whenVisitedAndNotCurrent_returnsVisited()
        {
            Assert.AreEqual(
                MapRoomVisualState.Visited,
                MapStateResolver.ResolveRoom(hasMap: true, roomState: RoomMapState.Visited, isCurrentRoom: false, isCompleted: false));
        }

        [Test]
        public void ResolveRoom_whenVisitedWithoutMap_returnsVisited()
        {
            Assert.AreEqual(
                MapRoomVisualState.Visited,
                MapStateResolver.ResolveRoom(hasMap: false, roomState: RoomMapState.Visited, isCurrentRoom: false, isCompleted: false));
        }

        [Test]
        public void ResolveRoom_whenCompleted_returnsCompleted()
        {
            Assert.AreEqual(
                MapRoomVisualState.Completed,
                MapStateResolver.ResolveRoom(hasMap: true, roomState: RoomMapState.Visited, isCurrentRoom: false, isCompleted: true));
        }

        [Test]
        public void ResolveRoom_whenCurrent_overridesCompleted()
        {
            Assert.AreEqual(
                MapRoomVisualState.Current,
                MapStateResolver.ResolveRoom(hasMap: true, roomState: RoomMapState.Visited, isCurrentRoom: true, isCompleted: true));
        }

        [Test]
        public void ResolveRoom_whenCurrent_returnsCurrent()
        {
            Assert.AreEqual(
                MapRoomVisualState.Current,
                MapStateResolver.ResolveRoom(hasMap: true, roomState: RoomMapState.Visited, isCurrentRoom: true, isCompleted: false));
        }

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
