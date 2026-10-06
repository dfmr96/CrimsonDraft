#nullable enable

namespace CrimsonDraft.Combat
{
    // Single source of truth for "how hurt is this operator" as a discrete step, shared by every
    // system that scales aim difficulty with HP (spawn radius, debuff effects, and eventually the
    // Animator's HealthState pose layer).
    public enum HpTier
    {
        Full   = 0,
        Yellow = 1,
        Orange = 2,
        Danger = 3,
    }
}
