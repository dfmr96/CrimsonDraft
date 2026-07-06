#nullable enable

using UnityEngine;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.Map
{
    /// <summary>Marks where a door draws on the map.</summary>
    public sealed class MapDoorMarker : MonoBehaviour
    {
        [Header("Map-space placement")]
        [SerializeField] private Vector2 mapOffset;
        [SerializeField] private float mapRotation;
        [SerializeField] private Vector2 size = new(1f, 0.25f);

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

        public Vector2 Size
        {
            get => this.size;
            set => this.size = value;
        }

        public string? ResolveDoorId()
            => GetComponent<IDoorInteractable>()?.DoorId;
    }
}
