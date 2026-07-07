#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    [Serializable]
    public sealed class MapElementTransform
    {
        public Vector2 Offset;
        public float Rotation;
        public Vector2 Scale = Vector2.one;
        public float ZOrder;
    }

    [Serializable]
    public sealed class MapRoomData
    {
        public string RoomId = "";
        public Vector2[] Polygon = Array.Empty<Vector2>();
        public MapElementTransform Transform = new();
        public string[] DoorIds = Array.Empty<string>();
        public string[] PickupIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class MapDoorData
    {
        public string DoorId = "";
        public MapElementTransform Transform = new();
        public Vector2 Size = new(1f, 0.25f);
    }

    [CreateAssetMenu(menuName = "CrimsonDraft/Map/Map Data")]
    public sealed class MapData : ScriptableObject
    {
        [SerializeField] private string sceneName = "";
        [SerializeField] private string displayName = "";
        [SerializeField] private string abbreviation = "";
        [SerializeField] private string mapItemId = "";
        [SerializeField] private Vector2Int gridSize = new(25, 25);
        [SerializeField] private float cellSize = 1f;
        [Tooltip("Where the map camera centers by default, in map-space (same space as room MapOffset). Rooms are usually offset in real world coordinates, so this should match the deck's actual coordinate range — it does not have to be (0,0).")]
        [SerializeField] private Vector2 center = Vector2.zero;
        [SerializeField] private List<MapRoomData> rooms = new();
        [SerializeField] private List<MapDoorData> doors = new();

        public string SceneName => this.sceneName;
        public string DisplayName => this.displayName;
        public string Abbreviation => this.abbreviation;
        public string MapItemId => this.mapItemId;
        public Vector2Int GridSize => this.gridSize;
        public float CellSize => this.cellSize;
        public Vector2 Center => this.center;
        public IReadOnlyList<MapRoomData> Rooms => this.rooms;
        public IReadOnlyList<MapDoorData> Doors => this.doors;

#if UNITY_EDITOR
        public void EditorSetBakedContent(List<MapRoomData> bakedRooms, List<MapDoorData> bakedDoors)
        {
            this.rooms = bakedRooms;
            this.doors = bakedDoors;
        }
#endif
    }
}
