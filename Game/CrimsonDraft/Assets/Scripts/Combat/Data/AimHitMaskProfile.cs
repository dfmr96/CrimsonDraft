#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    [CreateAssetMenu(fileName = "AimHitMaskProfile", menuName = "CrimsonDraft/Combat/Aim Hit Mask Profile")]
    public sealed class AimHitMaskProfile : ScriptableObject
    {
        [SerializeField] private Sprite               zoneMaskSprite  = null!;
        [SerializeField] private ShotZoneDefinition[] zoneDefinitions = Array.Empty<ShotZoneDefinition>();
        [SerializeField] private float                colorTolerance  = 0.1f;

        public Sprite ZoneMaskSprite                 => this.zoneMaskSprite;
        public ShotZoneDefinition[] ZoneDefinitions  => this.zoneDefinitions;
        public float ColorTolerance                  => this.colorTolerance;
    }
}
