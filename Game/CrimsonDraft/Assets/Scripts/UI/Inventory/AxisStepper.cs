#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI
{
    /// <summary>Turns an analog vertical axis into one step per press: fires past
    /// PressThreshold, re-arms only once the axis falls back under ReleaseThreshold.
    /// Up (+y) returns -1 (towards the top floor), down returns +1.</summary>
    public sealed class AxisStepper
    {
        public const float PressThreshold   = 0.5f;
        public const float ReleaseThreshold = 0.2f;

        private bool armed = true;

        public int Update(float y)
        {
            float magnitude = Mathf.Abs(y);

            if (!this.armed)
            {
                if (magnitude < ReleaseThreshold)
                    this.armed = true;
                return 0;
            }

            if (magnitude < PressThreshold)
                return 0;

            this.armed = false;
            return y > 0f ? -1 : +1;
        }

        public void Reset() => this.armed = false;
    }
}
