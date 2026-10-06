#nullable enable

namespace CrimsonDraft.Combat
{
    public static class ArmorRules
    {
        // Blocked only when the pellet hit the body (the zone mask decides that, an overlay
        // pixel outside the body never counts), landed on Armor coverage, and the weapon
        // doesn't fully penetrate it. A 1.0 multiplier (the MP7) is therefore never blocked.
        public static bool IsBlocked(ShotZone zone, bool covered, OverlayKind? kind, float armorDamageMultiplier) =>
            zone != ShotZone.Miss
            && covered
            && kind == OverlayKind.Armor
            && armorDamageMultiplier < 1f;
    }
}
