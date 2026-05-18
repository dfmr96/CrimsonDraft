#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [Serializable]
    public struct BurstShotEntry
    {
        public Vector2 center;
        public float   semiAxisX;
        public float   semiAxisY;
    }

    [CreateAssetMenu(fileName = "BurstPattern", menuName = "CrimsonDraft/Inventory/Burst Pattern")]
    public sealed class BurstPatternData : ScriptableObject
    {
        [SerializeField] private BurstShotEntry[] shots = new[]
        {
            new BurstShotEntry { center = Vector2.zero, semiAxisX = 20f, semiAxisY = 30f }
        };

        public BurstShotEntry[] Shots => this.shots;

        public void SetShots(BurstShotEntry[] entries) => this.shots = entries;

        public static Vector2 SamplePoint(in BurstShotEntry entry)
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float r     = Mathf.Sqrt(UnityEngine.Random.value);
            float ax    = Mathf.Max(1f, entry.semiAxisX);
            float ay    = Mathf.Max(1f, entry.semiAxisY);
            return new Vector2(
                entry.center.x + ax * r * Mathf.Cos(angle),
                entry.center.y + ay * r * Mathf.Sin(angle));
        }
    }
}
