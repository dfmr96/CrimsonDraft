using System;
using UnityEngine;

namespace HorrorEngine
{
    [Serializable]
    public class RankingData
    {
        public RankingRankData Rank;
        public float TimeThresholdInSeconds;
        public int MaxAllowedSaves;

        [HideInInspector]
        public string FormattedTime;
    }
}
