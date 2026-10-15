#nullable enable

using DG.Tweening;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Drives the operator's turn-charge bar: splits the overall 0-1 ATB gauge evenly
    // across its child OperatorGaugeSegments (each fills its own 1/N slice, staying
    // olive). Only once the WHOLE bar reaches 100% does it flicker every segment to
    // white together; on reset it snaps them all back to olive together.
    //
    // When combat opens with the operators already fully charged (the player shot first in
    // the world), the very first value is 100%: instead of appearing full out of nowhere the bar
    // charges up quickly, the last segment stretches in white, and only then does the usual
    // completion flicker change the colors -- so it reads as "your turn bars just filled".
    public sealed class OperatorGaugeBar : MonoBehaviour
    {
        [SerializeField] private OperatorGaugeSegment[] segments = System.Array.Empty<OperatorGaugeSegment>();
        [SerializeField] private int   completionFlickerCount    = 2;
        [SerializeField] private float completionFlickerDuration = 0.3f;

        [Header("Charge-up when combat starts full")]
        [SerializeField] private bool  chargeUpWhenStartingFull = true;
        [SerializeField] private float chargeUpStartDelay       = 0.35f;  // let the screen fade in first (ScreenFader = 0.4 s)
        [SerializeField] private float chargeUpDuration         = 0.35f;
        [SerializeField] private float lastStretchBy            = 8f;     // extra px of height for the last segment
        [SerializeField] private float lastStretchUpDuration    = 0.07f;
        [SerializeField] private float lastStretchDownDuration  = 0.09f;

        private bool  wasFull;
        private bool  sawFirstValue;
        private bool  chargingUp;
        private Tween? chargeTween;

        public void SetGauge01(float overall01)
        {
            overall01 = Mathf.Clamp01(overall01);
            if (this.segments.Length == 0) return;

            bool firstValue = !this.sawFirstValue;
            this.sawFirstValue = true;

            // The charge-up owns the segments until it finishes; the orchestrator keeps pushing 1.0.
            if (this.chargingUp) return;

            if (firstValue && this.chargeUpWhenStartingFull && overall01 >= 1f - 0.001f)
            {
                this.PlayChargeUp();
                return;
            }

            bool isFull        = overall01 >= 1f - 0.001f; // tolerance: float division can land a hair under 1
            bool justCompleted = isFull && !this.wasFull;

            // While the bar is registered full, no segment may run its own olive-ending
            // pulse -- suppressing only on the justCompleted frame let the last segment's
            // OWN 1/N-scale tolerance check lag a frame behind the bar's (a per-segment
            // local value only reaches ITS "isFull" once overall01 is even closer to 1 than
            // the bar's own -0.001f tolerance needs), so it could fire PlayFillPulse() a
            // frame after PlayCompletionFlicker already painted every segment white,
            // killing that sequence and settling back on the rest color permanently.
            this.ApplyToSegments(overall01, suppressPulse: isFull);

            if (justCompleted)
            {
                this.wasFull = true;
                this.PlayCompletionFlicker();
            }
            else if (!isFull && this.wasFull)
            {
                this.wasFull = false;
                foreach (var segment in this.segments)
                    segment?.SnapToRest();
            }
        }

        private void ApplyToSegments(float overall01, bool suppressPulse)
        {
            float perSegment = 1f / this.segments.Length;
            for (int i = 0; i < this.segments.Length; i++)
            {
                float segmentStart = i * perSegment;
                float local = Mathf.Clamp01((overall01 - segmentStart) / perSegment);
                this.segments[i]?.SetFill(local, suppressPulse);
            }
        }

        private void PlayCompletionFlicker()
        {
            foreach (var segment in this.segments)
                segment?.PlayCompletionFlicker(this.completionFlickerCount, this.completionFlickerDuration);
        }

        // Fills 0 -> 100% fast (segments stay olive and skip their own pulses), then the last
        // segment stretches in white and settles, then the whole bar flickers to its completed color.
        private void PlayChargeUp()
        {
            this.chargingUp = true;
            this.chargeTween?.Kill();

            foreach (var segment in this.segments)
                segment?.SnapToRest();
            this.ApplyToSegments(0f, suppressPulse: true);

            float progress = 0f;
            var charge = DOTween.To(() => progress, v =>
                {
                    progress = v;
                    this.ApplyToSegments(v, suppressPulse: true);
                }, 1f, Mathf.Max(0.01f, this.chargeUpDuration))
                .SetEase(Ease.OutQuad);

            // The empty bar waits out the scene's fade-in so the charge is actually seen.
            this.chargeTween = DOTween.Sequence()
                .AppendInterval(Mathf.Max(0f, this.chargeUpStartDelay))
                .Append(charge)
                .SetTarget(this)
                .OnComplete(this.FinishChargeUp);
        }

        private void FinishChargeUp()
        {
            this.ApplyToSegments(1f, suppressPulse: true);

            var last = this.segments[this.segments.Length - 1];
            if (last != null)
            {
                last.PlayLastStretch(this.lastStretchBy, this.lastStretchUpDuration, this.lastStretchDownDuration, () => this.CompleteChargeUp());
            }
            else
            {
                this.CompleteChargeUp();
            }
        }

        private void CompleteChargeUp()
        {
            if (this == null) return;
            this.chargingUp = false;
            this.wasFull    = true;
            this.PlayCompletionFlicker();
        }

        private void OnDisable()
        {
            this.chargeTween?.Kill();
            this.chargeTween = null;
            this.chargingUp  = false;
        }
    }
}
