#nullable enable

using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Navigation.Map
{
    /// <summary>Binds a scene to the MapData asset it bakes into.</summary>
    public sealed class MapSceneConfig : MonoBehaviour
    {
        [SerializeField] private MapData map = null!;

        public MapData Map => this.map;
    }
}
