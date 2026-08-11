#nullable enable

using System;
using DG.Tweening;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    // The operator card's own frame. At rest it just covers the card (matches the
    // OperatorOverview footprint); when the command panel opens, it grows straight up
    // by extraHeight — exactly the height CommandPanel occupies — so the same border
    // visually contains both the card and the command options above it, instead of the
    // command panel having its own separate frame. additionalTargets (e.g. the black
    // backing behind the border) grow in perfect lockstep with this one.
    public sealed class OperatorBorderPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform[] additionalTargets = Array.Empty<RectTransform>();
        [SerializeField] private float extraHeight      = 72f;
        [SerializeField] private float expandDuration   = 0.2f;
        [SerializeField] private float collapseDuration = 0.15f;
        [SerializeField] private Ease  expandEase       = Ease.OutQuad;
        [SerializeField] private Ease  collapseEase     = Ease.InQuad;

        private RectTransform[] allTargets = Array.Empty<RectTransform>();
        private float           restHeight;
        private bool            initialized;
        private bool            expanded;

        private void EnsureInitialized()
        {
            if (this.initialized) return;

            var self = (RectTransform)this.transform;
            this.allTargets = new RectTransform[this.additionalTargets.Length + 1];
            this.allTargets[0] = self;
            Array.Copy(this.additionalTargets, 0, this.allTargets, 1, this.additionalTargets.Length);

            this.restHeight  = self.sizeDelta.y;
            this.initialized = true;
        }

        public void SetExpanded(bool expand, Action? onComplete = null)
        {
            this.EnsureInitialized();
            if (this.expanded == expand)
            {
                onComplete?.Invoke();
                return;
            }

            this.expanded = expand;

            float targetHeight = expand ? this.restHeight + this.extraHeight : this.restHeight;
            float duration     = expand ? this.expandDuration : this.collapseDuration;
            Ease  ease         = expand ? this.expandEase : this.collapseEase;

            for (int i = 0; i < this.allTargets.Length; i++)
            {
                var target = this.allTargets[i];
                if (target == null) continue;

                target.DOKill();
                var tween = target.DOSizeDelta(new Vector2(target.sizeDelta.x, targetHeight), duration).SetEase(ease);
                if (i == 0) tween.OnComplete(() => onComplete?.Invoke()); // fire the callback once, not per-target
            }
        }

        private void OnDisable()
        {
            foreach (var target in this.allTargets)
                target?.DOKill();
        }
    }
}
