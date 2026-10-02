#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Per-tier balance for the aim debuffs: one slot per possible effect kind, each with its own
    // Enabled switch and the same shader data fields the dev preview tool (AimDebuffPreviewRig)
    // exposes -- so a look tuned live in the preview can be copied here field-for-field. Every
    // enabled slot runs together for the whole aim (they accumulate, not a random draw) at this
    // tier's fixed values -- no randomness, no ramping, no carryover between tiers: whichever tier
    // the HP bar's color currently shows is exactly what this profile looks like, every time, for
    // any operator. One asset per HpTier, assigned on AimDebuffController. Full has no asset
    // assigned, meaning no debuffs.
    [CreateAssetMenu(fileName = "AimDebuffTierProfile", menuName = "CrimsonDraft/Combat/Aim Debuff Tier Profile")]
    public sealed class AimDebuffTierProfile : ScriptableObject
    {
        // Vignette (held tunnel close-in) and PaletteShift aren't wired here -- no shipped tier
        // currently uses them (every tier uses BreathingVignette instead, and none use a palette
        // wash). Both effects still exist and are fully testable in AimDebuffPreviewRig/
        // AimDebuffPreview.unity if a future tier wants them; re-add the field here and the
        // corresponding TryBegin call in AimDebuffController.Accumulate() to wire one back in.
        [Header("Effect Slots -- the actual shader data per tier")]
        public BreathingVignetteSlot BreathingVignette         = new();
        public GrainSlot             Grain                     = new();
        public TintSlot              Desaturation              = new();
        public HeartbeatSlot         ScreenHeartbeat           = new();
        public PulseDistortSlot      ChromaticAberrationPulse  = new();
        public PulseDistortSlot      BlurPulse                 = new();

        // Applied instantly at shot start (no fade-in -- see AimDebuffController.TryBegin), but
        // still fades out gracefully over this when the aim resolves (AimDebuffController.EndForShot).
        [Header("Fade-Out Timing")]
        public float FadeOutSeconds = 0.4f;
    }
}
