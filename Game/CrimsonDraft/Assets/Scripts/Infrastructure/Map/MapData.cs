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
        public Sprite? IncompleteSprite;
        public Sprite? CompleteSprite;
        public Vector2Int Position;
        public int QuarterTurns;
        public bool IsOrphan;
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
        [SerializeField] private List<MapRoomData> rooms = new();
        [SerializeField] private List<MapDoorData> doors = new();

        public string SceneName => this.sceneName;
        public string DisplayName => this.displayName;
        public string Abbreviation => this.abbreviation;
        public string MapItemId => this.mapItemId;
        public IReadOnlyList<MapRoomData> Rooms => this.rooms;
        public IReadOnlyList<MapDoorData> Doors => this.doors;

#if UNITY_EDITOR
        public void EditorSetBakedContent(List<MapRoomData> bakedRooms, List<MapDoorData> bakedDoors)
        {
            this.rooms = bakedRooms;
            this.doors = bakedDoors;
        }

        public bool EditorRemoveRoom(string roomId) => this.rooms.RemoveAll(r => r.RoomId == roomId) > 0;
#endif
    }
}
