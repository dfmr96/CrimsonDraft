#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Tests
{
    internal static class MapTestData
    {
        public static Sprite Sprite(int width = 10, int height = 10, float ppu = 100f, FilterMode filter = FilterMode.Point)
        {
            var texture = new Texture2D(width, height) { filterMode = filter };
            return UnityEngine.Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), ppu);
        }

        public static MapRoomData Room(
            string id,
            Vector2Int position = default,
            int quarterTurns = 0,
            string[]? pickups = null,
            Sprite? incomplete = null,
            Sprite? complete = null)
            => new()
            {
                RoomId           = id,
                Position         = position,
                QuarterTurns     = quarterTurns,
                PickupIds        = pickups ?? Array.Empty<string>(),
                IncompleteSprite = incomplete,
                CompleteSprite   = complete,
            };

        public static MapData Map(string sceneName, params MapRoomData[] rooms)
        {
            var map = ScriptableObject.CreateInstance<MapData>();
            var so  = new SerializedObject(map);
            so.FindProperty("sceneName").stringValue = sceneName;
            so.ApplyModifiedPropertiesWithoutUndo();
            map.EditorSetBakedContent(new List<MapRoomData>(rooms), new List<MapDoorData>());
            return map;
        }
    }
}
