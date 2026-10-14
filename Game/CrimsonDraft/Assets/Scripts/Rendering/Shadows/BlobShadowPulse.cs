#nullable enable

using System;

namespace CrimsonDraft.Rendering.Shadows
{
    public sealed class BlobShadowPulse
    {
        private float peak;
        private float duration = 1f;
        private float elapsed;

        public float ScaleMultiplier => 1f + CurrentOffset;

        private float CurrentOffset
        {
            get
            {
                if (this.elapsed >= this.duration) return 0f;

                var remaining = 1f - this.elapsed / this.duration;
                return this.peak * remaining * remaining;
            }
        }

        public void Trigger(float amplitude, float duration)
        {
            if (duration <= 0f)
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Pulse duration must be positive.");

            this.peak     = Math.Max(CurrentOffset, amplitude);
            this.duration = duration;
            this.elapsed  = 0f;
        }

        public void Tick(float deltaTime) => this.elapsed += deltaTime;
    }
}
