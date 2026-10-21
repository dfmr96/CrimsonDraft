#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Pure HP% -> HpTier mapping. The one reusable place this calculation happens -- both the
    // Fase 1 spawn radius and the aim debuff effects key off the same tier instead of each
    // hardcoding their own HP breakpoints.
    public static class HpTierCalculator
    {
        public static HpTier ComputeTier(float hpRatio, HpTierConfig config)
        {
            if (config == null)
                return HpTier.Full;

            float ratio = Mathf.Clamp01(hpRatio);
            if (ratio < config.DangerThreshold) return HpTier.Danger;
            if (ratio < config.OrangeThreshold) return HpTier.Orange;
            if (ratio < config.YellowThreshold) return HpTier.Yellow;
            return HpTier.Full;
        }

        // Maps the discrete tier to an even 0..1 step, for systems (like the Fase 1 spawn radius)
        // that want a continuous-looking 0..1 "danger" input but driven by the same tier
        // boundaries as everything else, instead of raw HP%.
        public static float TierToDanger01(HpTier tier) => tier switch
        {
            HpTier.Full   => 0f,
            HpTier.Yellow => 1f / 3f,
            HpTier.Orange => 2f / 3f,
            HpTier.Danger => 1f,
            _             => 0f,
        };
    }
}
