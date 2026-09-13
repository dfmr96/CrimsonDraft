#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Combat
{
    // One 10%-slice of the turn-charge bar. Fills itself (0-1) as the overall gauge
    // sweeps through its slice of the range. Every time THIS segment individually hits
    // 100% it flickers and punches its height — but settles back down to the resting
    // olive color, not white. White is reserved for the whole-bar completion, which
    // OperatorGaugeBar plays across every segment together via PlayCompletionFlicker.
    public sealed class OperatorGaugeSegment : MonoBehaviour
    {
        [SerializeField] private Color restColor      = new(0.6039216f, 0.62352943f, 0.36078432f, 1f);
        [SerializeField] private Color filledColor     = Color.white;
        [SerializeField] private int   flickerCount    = 3;
        [SerializeField] private float flickerDuration = 0.28f;
        [SerializeField] private float expandHeightBy  = 3f;
        [SerializeField] private float expandDuration  = 0.12f;
        [SerializeField] private float settleDuration  = 0.1f;

        private Image          fillImage    = null!;
        private RectTransform  rectTransform = null!;
        private float          restHeight;
        private bool           wasFull;
        private bool           initialized;

        private void EnsureInitialized()
        {
            if (this.initialized) return;
            this.fillImage     = GetComponent<Image>();
            this.rectTransform = (RectTransform)this.transform;
            this.restHeight    = this.rectTransform.sizeDelta.y;
            this.fillImage.color = this.restColor;
            this.initialized = true;
        }

        // suppressPulse: OperatorGaugeBar sets this on the exact call where the WHOLE
        // bar also completes, so this segment doesn't start its own olive-ending pulse
        // a frame before PlayCompletionFlicker kills and replaces it with the white one.
        public void SetFill(float t01, bool suppressPulse = false)
        {
            this.EnsureInitialized();
            t01 = Mathf.Clamp01(t01);
            this.fillImage.fillAmount = t01;

            bool isFull = t01 >= 1f - 0.001f; // tolerance: float division can land a hair under 1
            if (isFull && !this.wasFull)
            {
                this.wasFull = true;
                if (!suppressPulse) PlayFillPulse();
            }
            else if (!isFull && this.wasFull)
            {
                this.wasFull = false;
                SnapToRest();
            }
        }

        // This segment alone just reached 100% — flicker and height-punch play together
        // (not one after the other), then settle back to the resting olive color (NOT
        // white; that only happens bar-wide, via PlayCompletionFlicker).
        private void PlayFillPulse()
        {
            this.rectTransform.DOKill();
            this.fillImage.DOKill();

            float step = this.flickerDuration / (this.flickerCount * 2f);
            var colorSeq = DOTween.Sequence().SetTarget(this.fillImage);
            for (int i = 0; i < this.flickerCount; i++)
            {
                colorSeq.Append(this.fillImage.DOColor(this.filledColor, step));
                colorSeq.Append(this.fillImage.DOColor(this.restColor, step)); // ends on rest color
            }
            // Belt-and-suspenders: guarantee the rest color lands exactly, even if this
            // sequence gets interrupted/killed mid-flight by something else.
            colorSeq.OnKill(() => { if (this.fillImage != null) this.fillImage.color = this.restColor; });

            var sizeSeq = DOTween.Sequence().SetTarget(this.rectTransform);
            sizeSeq.Append(this.rectTransform.DOSizeDelta(
                    new Vector2(this.rectTransform.sizeDelta.x, this.restHeight + this.expandHeightBy),
                    this.expandDuration)
                .SetEase(Ease.OutQuad));
            sizeSeq.Append(this.rectTransform.DOSizeDelta(
                    new Vector2(this.rectTransform.sizeDelta.x, this.restHeight),
                    this.settleDuration)
                .SetEase(Ease.InQuad));
        }

        // Called by OperatorGaugeBar on every segment at once when the WHOLE bar hits
        // 100% — flickers and ends white (this is the only place white becomes permanent).
        public void PlayCompletionFlicker(int flickerCount, float flickerDuration)
        {
            this.EnsureInitialized();
            this.rectTransform.DOKill();
            this.fillImage.DOKill();

            float step = flickerDuration / (flickerCount * 2f);
            var seq = DOTween.Sequence().SetTarget(this.fillImage);
            for (int i = 0; i < flickerCount; i++)
            {
                seq.Append(this.fillImage.DOColor(this.restColor, step));
                seq.Append(this.fillImage.DOColor(this.filledColor, step)); // ends white
            }
            // Belt-and-suspenders: guarantee white lands exactly, even if this sequence
            // gets interrupted/killed mid-flight (e.g. by a reset racing the completion).
            seq.OnComplete(() => { if (this.fillImage != null) this.fillImage.color = this.filledColor; });
            seq.OnKill(() => { if (this.fillImage != null) this.fillImage.color = this.filledColor; });
        }

        // Called by OperatorGaugeBar on every segment at once when the gauge resets.
        public void SnapToRest()
        {
            this.EnsureInitialized();
            this.rectTransform.DOKill();
            this.fillImage.DOKill();
            this.rectTransform.sizeDelta = new Vector2(this.rectTransform.sizeDelta.x, this.restHeight);
            this.fillImage.color = this.restColor;
        }

        private void OnDisable()
        {
            this.rectTransform?.DOKill();
            this.fillImage?.DOKill();
        }
    }
}
