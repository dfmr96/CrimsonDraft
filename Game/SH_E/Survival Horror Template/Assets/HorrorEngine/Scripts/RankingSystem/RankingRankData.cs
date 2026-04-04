using System;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "RankingRank", menuName = "Horror Engine/Ranking/RankingRank")]
    public class RankingRankData : ScriptableObject
    {
        public string Name;
        public Color Color = Color.white;
    }
}
