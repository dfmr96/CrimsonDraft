#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Tunable knobs for how far off-center the vertical/horizontal aim selectors start each shot.
    // Purely cosmetic: it changes how hard the QTE bars are to read, never the outcome odds --
    // DOTween's fixed-duration tweening means a farther spawn still covers its first leg in the
    // same time as a normal leg, so the reaction window never actually shrinks.
    [CreateAssetMenu(fileName = "AimSpawnConfig", menuName = "CrimsonDraft/Combat/Aim Spawn Config")]
    public sealed class AimSpawnConfig : ScriptableObject
    {
        [Header("Offset Range (fraction of half-rail)")]
        [Tooltip("Minimum distance from center a spawn point can land, regardless of HP.")]
        [Range(0f, 0.9f)] public float MinOffsetRatio = 0.3f;

        [Tooltip("Maximum spawn offset at full HP.")]
        [Range(0f, 1f)] public float BaseMaxOffsetRatio = 0.65f;

        [Tooltip("Maximum spawn offset at 0 HP.")]
        [Range(0f, 1f)] public float LowHpMaxOffsetRatio = 0.9f;

        [Header("Edge Spawns")]
        [Tooltip("Offsets at or beyond this fraction of the current max count as an 'edge' spawn.")]
        [Range(0f, 1f)] public float EdgeZoneRatio = 0.75f;

        [Tooltip("Chance a spawn is forced into the edge zone, at full HP.")]
        [Range(0f, 1f)] public float BaseEdgeSpawnChance = 0.2f;

        [Tooltip("Chance a spawn is forced into the edge zone, at 0 HP.")]
        [Range(0f, 1f)] public float LowHpEdgeSpawnChance = 0.6f;

        [Header("Repeat Avoidance")]
        [Tooltip("Minimum separation (fraction of half-rail) from the previous spawn's offset before a candidate is accepted.")]
        [Range(0f, 1f)] public float MinSeparationRatio = 0.1f;

        [Tooltip("Max redraw attempts before falling back to mirroring the previous offset.")]
        [Range(1, 16)] public int MaxRerollAttempts = 6;
    }
}
