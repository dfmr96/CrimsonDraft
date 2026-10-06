using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Mapping/MapDataSet")]
    public class MapDataSet : ScriptableObject
    {
        public MapData[] Maps;
    }
}
