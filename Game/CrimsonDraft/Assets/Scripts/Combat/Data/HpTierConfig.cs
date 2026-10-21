#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Configurable HP% thresholds for the Full/Yellow/Orange/Danger tiers. The only place these
    // boundaries are defined -- HpTierCalculator reads this, nothing hardcodes the percentages.
    [CreateAssetMenu(fileName = "HpTierConfig", menuName = "CrimsonDraft/Combat/Hp Tier Config")]
    public sealed class HpTierConfig : ScriptableObject
    {
        [Tooltip("Below this HP ratio the operator leaves Full (e.g. 0.75 = below 75%).")]
        [Range(0f, 1f)] public float YellowThreshold = 0.75f;

        [Tooltip("Below this HP ratio the operator moves from Yellow to Orange.")]
        [Range(0f, 1f)] public float OrangeThreshold = 0.5f;

        [Tooltip("Below this HP ratio the operator moves from Orange to Danger.")]
        [Range(0f, 1f)] public float DangerThreshold = 0.25f;
    }
}
