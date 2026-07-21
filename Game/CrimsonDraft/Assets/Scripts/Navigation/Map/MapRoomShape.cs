#nullable enable

using UnityEngine;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Map
{
    /// <summary>Authored 2D silhouette of a room in local XZ space.</summary>
    [RequireComponent(typeof(RoomController))]
    public sealed class MapRoomShape : MonoBehaviour
    {
        [SerializeField] private Vector2[] localPoints = System.Array.Empty<Vector2>();

        [Header("Map-space placement")]
        [SerializeField] private Vector2 mapOffset;
        [SerializeField] private float mapRotation;
        [SerializeField] private Vector2 mapScale = Vector2.one;
        [SerializeField] private float zOrder;

        public Vector2[] LocalPoints
        {
            get => this.localPoints;
            set => this.localPoints = value;
        }

        public Vector2 MapOffset
        {
            get => this.mapOffset;
            set => this.mapOffset = value;
        }

        public float MapRotation
        {
            get => this.mapRotation;
            set => this.mapRotation = value;
        }

        public Vector2 MapScale
        {
            get => this.mapScale;
            set => this.mapScale = value;
        }

        public float ZOrder
        {
            get => this.zOrder;
            set => this.zOrder = value;
        }

        public RoomController Room => GetComponent<RoomController>();
    }
}
