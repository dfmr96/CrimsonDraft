#nullable enable

using DG.Tweening;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Small punch on the operator card when its Operator takes a hit. Lives on the
    // OperatorOverview root (not Visual, which OperatorFocusBounce already animates for
    // roster browse focus) so the two tweens never fight over the same anchoredPosition.
    public sealed class OperatorCardShake : MonoBehaviour
    {
        [SerializeField] private RectTransform target     = null!;
        [SerializeField] private float         strength   = 6f;
        [SerializeField] private int           vibrato    = 18;
        [SerializeField] private float         duration   = 0.25f;
        [SerializeField] private float         randomness = 90f;

        private Vector2 restPosition;
        private bool    initialized;

        public void PlayDamageShake()
        {
            this.EnsureInitialized();
            this.target.DOKill();
            this.target.anchoredPosition = this.restPosition;
            this.target.DOShakeAnchorPos(this.duration, this.strength, this.vibrato, this.randomness, false, true)
                .OnComplete(() => this.target.anchoredPosition = this.restPosition);
        }

        private void EnsureInitialized()
        {
            if (this.initialized) return;
            if (this.target == null) this.target = (RectTransform)this.transform;
            this.restPosition = this.target.anchoredPosition;
            this.initialized = true;
        }

        private void OnDisable() => this.target?.DOKill();
    }
}
