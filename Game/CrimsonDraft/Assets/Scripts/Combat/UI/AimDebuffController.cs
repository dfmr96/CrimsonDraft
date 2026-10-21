#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Drives the purely-cosmetic aim debuff effects off the operator's HpTier. Every effect is a
    // UI shader/material scoped to the AimView panel itself (overlay Images stretched to its rect,
    // or a shared material on the aim selectors/silhouette) -- never a scene-wide URP Volume, so
    // nothing outside this panel is ever affected. Each tier is a fixed, self-contained look: the
    // active profile is whichever one exactly matches the current tier, at its full configured
    // intensity, with no ramping or carryover from other tiers -- the HP bar's color can always be
    // read as "this is exactly what the debuff state looks like right now," never something
    // in-between. Follows the same "small MonoBehaviour with a clear public API" shape as
    // OperatorCombatWeaponPose: callers (AimViewController) just call SetTier/BeginForShot/
    // EndForShot/EndAll and never touch the effect internals.
    public sealed class AimDebuffController : MonoBehaviour
    {
        [Header("Overlay Materials (washes scoped to the AimView panel -- see the overlay GameObjects' own Y scale for framing)")]
        [SerializeField] private Material? vignetteMaterial;     // BreathingVignetteDebuffEffect
        [SerializeField] private Material? grainMaterial;

        [Header("Distort Material (assigned directly on the selectors/silhouette Images)")]
        [SerializeField] private Material? distortMaterial;      // shared: ChromaticAberrationPulse + BlurPulse (Orange)

        [Header("Heartbeat Material (Orange)")]
        [SerializeField] private Material? heartbeatMaterial;

        [Header("Per-Tier Profiles (null = no debuffs at that tier)")]
        [SerializeField] private AimDebuffTierProfile? fullProfile;
        [SerializeField] private AimDebuffTierProfile? yellowProfile;
        [SerializeField] private AimDebuffTierProfile? orangeProfile;
        [SerializeField] private AimDebuffTierProfile? dangerProfile;

        private HpTier currentTier = HpTier.Full;

        private readonly List<IAimDebuffEffect> activeEffects         = new List<IAimDebuffEffect>();
        private readonly List<float>             activeFadeOutSeconds = new List<float>();

        public void SetTier(HpTier tier) => this.currentTier = tier;

        // Activates whichever profile exactly matches the current tier, at full intensity -- no
        // ramping, no effects carried over from another tier. EndForShot() below is what fades
        // them out, not a timer. Any effects still running from a previous shot are hard-stopped
        // first.
        public void BeginForShot()
        {
            this.EndAllImmediate();

            var profile = this.ProfileForTier(this.currentTier);
            if (profile != null)
                this.Accumulate(profile);

            if (this.activeEffects.Count > 0)
                this.StartCoroutine(this.TickActiveEffects());
        }

        // Both aim axes are now locked in -- start a graceful fade-out instead of holding forever.
        public void EndForShot()
        {
            for (int i = 0; i < this.activeEffects.Count; i++)
                this.activeEffects[i].End(this.activeFadeOutSeconds[i]);
        }

        // Guaranteed shutdown path: called whenever the aim view closes, is cancelled, or the
        // operator dies -- combat can never be left waiting on a cosmetic fade.
        public void EndAll() => this.EndAllImmediate();

        private void OnDisable() => this.EndAllImmediate();

        private AimDebuffTierProfile? ProfileForTier(HpTier tier) => tier switch
        {
            HpTier.Full   => this.fullProfile,
            HpTier.Yellow => this.yellowProfile,
            HpTier.Orange => this.orangeProfile,
            HpTier.Danger => this.dangerProfile,
            _             => null,
        };

        private void Accumulate(AimDebuffTierProfile profile)
        {
            this.TryBegin(profile.BreathingVignette.Enabled, profile,
                this.vignetteMaterial != null ? new BreathingVignetteDebuffEffect(this.vignetteMaterial, profile.BreathingVignette.Color, profile.BreathingVignette.Smoothness, profile.BreathingVignette.BreathHz, profile.BreathingVignette.BreathDepth) : null,
                profile.BreathingVignette.Intensity);

            this.TryBegin(profile.Grain.Enabled, profile,
                this.grainMaterial != null ? new GrainDebuffEffect(this.grainMaterial, profile.Grain.Color, profile.Grain.Response, profile.Grain.Scale) : null,
                profile.Grain.Intensity);

            this.TryBegin(profile.ScreenHeartbeat.Enabled, profile,
                this.heartbeatMaterial != null
                    ? new ScreenHeartbeatDebuffEffect(this.heartbeatMaterial, profile.ScreenHeartbeat.Color,
                        profile.ScreenHeartbeat.CalmBeatInterval, profile.ScreenHeartbeat.PanicBeatInterval,
                        profile.ScreenHeartbeat.DubStrength, profile.ScreenHeartbeat.DubOffsetFraction, profile.ScreenHeartbeat.PulseDecay)
                    : null,
                profile.ScreenHeartbeat.Intensity);

            this.TryBegin(profile.ChromaticAberrationPulse.Enabled, profile,
                this.distortMaterial != null ? new ChromaticAberrationPulseDebuffEffect(this.distortMaterial, profile.ChromaticAberrationPulse) : null,
                profile.ChromaticAberrationPulse.Intensity);

            this.TryBegin(profile.BlurPulse.Enabled, profile,
                this.distortMaterial != null ? new BlurPulseDebuffEffect(this.distortMaterial, profile.BlurPulse) : null,
                profile.BlurPulse.Intensity);
        }

        private void TryBegin(bool enabled, AimDebuffTierProfile profile, IAimDebuffEffect? effect, float intensity)
        {
            if (!enabled || effect == null)
                return;

            // Instant, not faded in: the panel only becomes visible once Show() finishes, and by
            // then every enabled effect is already at its target intensity -- otherwise the player
            // sees the silhouette appear still carrying the previous shot's (weaker/absent) look,
            // then visibly catch up over the fade, which reads as an abrupt late correction rather
            // than a smooth one.
            effect.Begin(intensity, 0f);
            this.activeEffects.Add(effect);
            this.activeFadeOutSeconds.Add(profile.FadeOutSeconds);
        }

        private void EndAllImmediate()
        {
            for (int i = 0; i < this.activeEffects.Count; i++)
                this.activeEffects[i].ForceStop();
            this.activeEffects.Clear();
            this.activeFadeOutSeconds.Clear();
            this.StopAllCoroutines();
        }

        private System.Collections.IEnumerator TickActiveEffects()
        {
            while (this.activeEffects.Count > 0)
            {
                for (int i = this.activeEffects.Count - 1; i >= 0; i--)
                {
                    var effect = this.activeEffects[i];
                    effect.Tick(Time.deltaTime);
                    if (effect.IsFinished)
                    {
                        this.activeEffects.RemoveAt(i);
                        this.activeFadeOutSeconds.RemoveAt(i);
                    }
                }
                yield return null;
            }
        }
    }
}
