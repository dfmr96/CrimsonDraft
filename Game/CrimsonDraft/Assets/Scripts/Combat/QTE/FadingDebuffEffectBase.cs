#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Shared fade-in/hold/fade-out state machine for aim debuff effects. Subclasses only need to
    // say how to apply a 0..1 envelope value to their specific Material property/properties -- this
    // owns the timing, and guarantees IsFinished only goes true once fully back at envelope 0.
    public abstract class FadingDebuffEffectBase : IAimDebuffEffect
    {
        private enum Phase { Idle, FadeIn, Hold, FadeOut }

        private Phase phase = Phase.Idle;
        private float phaseElapsed;
        private float fadeInSeconds  = 0.3f;
        private float fadeOutSeconds = 0.4f;

        protected float TargetIntensity { get; private set; }
        protected float ElapsedTotal    { get; private set; } // time since Begin(), for subclasses that oscillate (breathing/pulse)

        public bool IsFinished => this.phase == Phase.Idle;

        public void Begin(float intensity01, float fadeInSeconds)
        {
            this.TargetIntensity = Mathf.Clamp01(intensity01);
            this.fadeInSeconds   = Mathf.Max(0.01f, fadeInSeconds);
            this.phase           = Phase.FadeIn;
            this.phaseElapsed    = 0f;
            this.ElapsedTotal    = 0f;

            this.OnActivate();
            this.Apply(0f);
        }

        public void Tick(float deltaTime)
        {
            if (this.phase == Phase.Idle)
                return;

            this.phaseElapsed += deltaTime;
            this.ElapsedTotal += deltaTime;

            float envelope;
            switch (this.phase)
            {
                case Phase.FadeIn:
                    envelope = Mathf.Clamp01(this.phaseElapsed / this.fadeInSeconds);
                    if (envelope >= 1f)
                    {
                        this.phase        = Phase.Hold;
                        this.phaseElapsed = 0f;
                    }
                    break;
                case Phase.FadeOut:
                    envelope = Mathf.Lerp(1f, 0f, Mathf.Clamp01(this.phaseElapsed / this.fadeOutSeconds));
                    if (this.phaseElapsed >= this.fadeOutSeconds)
                    {
                        this.Deactivate();
                        return;
                    }
                    break;
                default: // Hold
                    envelope = 1f;
                    break;
            }

            this.Apply(envelope);
        }

        public void End(float fadeOutSeconds)
        {
            if (this.phase == Phase.Idle || this.phase == Phase.FadeOut)
                return;

            this.fadeOutSeconds = Mathf.Max(0.01f, fadeOutSeconds);
            this.phase          = Phase.FadeOut;
            this.phaseElapsed   = 0f;
        }

        public void ForceStop() => this.Deactivate();

        private void Deactivate()
        {
            this.phase = Phase.Idle;
            this.Apply(0f);
            this.OnDeactivate();
        }

        // Called once when the effect starts -- e.g. cache a starting Material property value.
        protected abstract void OnActivate();

        // Called every Tick (and once at Begin with 0, once at full deactivation with 0) with the
        // current fade envelope (0..1). Subclasses combine this with TargetIntensity and, for
        // oscillating effects, ElapsedTotal to compute their actual parameter value(s).
        protected abstract void Apply(float envelope01);

        // Called once when the effect fully finishes fading out -- e.g. set `active = false`.
        protected abstract void OnDeactivate();
    }
}
