#nullable enable

using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Map/Map Data Set")]
    public sealed class MapDataSet : ScriptableObject
    {
        [SerializeField] private MapData[] maps = System.Array.Empty<MapData>();

        public MapData[] Maps => this.maps;
    }
}
