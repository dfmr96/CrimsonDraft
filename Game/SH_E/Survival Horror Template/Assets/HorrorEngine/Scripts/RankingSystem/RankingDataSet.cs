using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "RankingDataSet", menuName = "Horror Engine/Ranking/RankingDataSet")]
    public class RankingDataSet : ScriptableObject
    {
        public RankingData[] Ranking;
    }
}
