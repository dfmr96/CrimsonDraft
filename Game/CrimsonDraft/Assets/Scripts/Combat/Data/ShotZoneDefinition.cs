using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    [Serializable]
    public struct ShotZoneDefinition
    {
        public Color              color;
        public ShotZone           zone;
        public ShotPrecisionEntry precisionEntry;
    }
}
