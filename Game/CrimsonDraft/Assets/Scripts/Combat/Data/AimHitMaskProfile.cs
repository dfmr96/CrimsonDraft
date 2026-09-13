#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    [CreateAssetMenu(fileName = "AimHitMaskProfile", menuName = "CrimsonDraft/Combat/Aim Hit Mask Profile")]
    public sealed class AimHitMaskProfile : ScriptableObject
    {
        [SerializeField] private Sprite               zoneMaskSprite      = null!;
        [SerializeField] private Sprite?              silhouetteSprite    = null;
        [SerializeField] private ShotZoneDefinition[] zoneDefinitions     = Array.Empty<ShotZoneDefinition>();
        [SerializeField] private float                colorTolerance      = 0.1f;

        public Sprite ZoneMaskSprite                 => this.zoneMaskSprite;
        // The black & white silhouette shown to the player during the aim QTE — separate from
        // ZoneMaskSprite, which is the color-coded sprite sampled for zone/hit detection and is
        // never itself shown on screen.
        public Sprite? SilhouetteSprite              => this.silhouetteSprite;
        public ShotZoneDefinition[] ZoneDefinitions  => this.zoneDefinitions;
        public float ColorTolerance                  => this.colorTolerance;
    }
}
