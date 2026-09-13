#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Drives the operator's turn-charge bar: splits the overall 0-1 ATB gauge evenly
    // across its child OperatorGaugeSegments (each fills its own 1/N slice, staying
    // olive). Only once the WHOLE bar reaches 100% does it flicker every segment to
    // white together; on reset it snaps them all back to olive together.
    public sealed class OperatorGaugeBar : MonoBehaviour
    {
        [SerializeField] private OperatorGaugeSegment[] segments = System.Array.Empty<OperatorGaugeSegment>();
        [SerializeField] private int   completionFlickerCount    = 2;
        [SerializeField] private float completionFlickerDuration = 0.3f;

        private bool wasFull;

        public void SetGauge01(float overall01)
        {
            overall01 = Mathf.Clamp01(overall01);
            if (this.segments.Length == 0) return;

            bool isFull        = overall01 >= 1f - 0.001f; // tolerance: float division can land a hair under 1
            bool justCompleted = isFull && !this.wasFull;

            // On the exact call the whole bar completes, the last segment(s) reaching
            // their own 100% would otherwise start an olive-ending pulse the same frame
            // PlayCompletionFlicker below kills and replaces it with the white one —
            // suppress that individual pulse here so there's nothing to race against.
            float perSegment = 1f / this.segments.Length;
            for (int i = 0; i < this.segments.Length; i++)
            {
                float segmentStart = i * perSegment;
                float local = Mathf.Clamp01((overall01 - segmentStart) / perSegment);
                this.segments[i]?.SetFill(local, suppressPulse: justCompleted);
            }

            if (justCompleted)
            {
                this.wasFull = true;
                foreach (var segment in this.segments)
                    segment?.PlayCompletionFlicker(this.completionFlickerCount, this.completionFlickerDuration);
            }
            else if (!isFull && this.wasFull)
            {
                this.wasFull = false;
                foreach (var segment in this.segments)
                    segment?.SnapToRest();
            }
        }
    }
}
