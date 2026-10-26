#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI
{
    /// <summary>
    /// The visual half of a combine: a copy of the source item's icon that travels in a straight line
    /// onto the target item and fades out there. It is only a picture -- the real items are whatever
    /// the inventory says after the combine (a source that still has quantity left stays where it is).
    /// </summary>
    public static class CombineFlyVisual
    {
        private const float Duration    = 0.35f;
        private const float EndScale    = 0.5f;

        public sealed class Flight
        {
            private readonly RectTransform ghost;
            private readonly Vector3       targetWorld;

            internal Flight(RectTransform ghost, Vector3 targetWorld)
            {
                this.ghost       = ghost;
                this.targetWorld = targetWorld;
            }

            /// <summary>
            /// The combine went through: fly onto the target, shrink and fade, then disappear.
            /// <paramref name="onArrived"/> runs once the flight is over (also if it is cut short).
            /// </summary>
            public void Launch(System.Action? onArrived = null)
            {
                var image = this.ghost.GetComponent<Image>();
                var scale = this.ghost.localScale;

                DOTween.Sequence().SetUpdate(true).SetLink(this.ghost.gameObject)
                    .Join(this.ghost.DOMove(this.targetWorld, Duration).SetEase(Ease.InOutCubic))
                    .Join(this.ghost.DOScale(scale * EndScale, Duration).SetEase(Ease.InCubic))
                    .Join(image.DOFade(0f, Duration * 0.35f).SetDelay(Duration * 0.65f))
                    .OnComplete(() => Object.Destroy(this.ghost.gameObject))
                    .OnKill(() => onArrived?.Invoke());
            }

            /// <summary>The combine was refused: nothing flies.</summary>
            public void Cancel()
            {
                if (this.ghost != null) Object.Destroy(this.ghost.gameObject);
            }
        }

        /// <summary>
        /// Copies <paramref name="source"/> on top of itself and remembers where <paramref name="target"/>
        /// is. Must run before the combine, because the views are rebuilt as soon as the inventory changes.
        /// </summary>
        public static Flight? Begin(InventoryItemView source, InventoryItemView target)
        {
            var sourceRect = (RectTransform)source.transform;
            var targetRect = (RectTransform)target.transform;
            var sprite     = source.GetComponent<Image>().sprite;
            var canvas     = source.GetComponentInParent<Canvas>();
            if (sprite == null || canvas == null) return null;

            var go    = new GameObject("CombineFlight", typeof(RectTransform), typeof(Image));
            var ghost = (RectTransform)go.transform;
            ghost.SetParent(canvas.transform, false);
            ghost.SetAsLastSibling();

            var image            = go.GetComponent<Image>();
            image.sprite         = sprite;
            image.preserveAspect = true;
            image.raycastTarget  = false;

            ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
            ghost.pivot     = new Vector2(0.5f, 0.5f);
            ghost.sizeDelta = sourceRect.rect.size;

            // Same place, rotation and on-screen size as the source (the canvas may be scaled).
            var parentScale = canvas.transform.lossyScale;
            var scale       = sourceRect.lossyScale;
            ghost.position   = sourceRect.TransformPoint(sourceRect.rect.center);
            ghost.rotation   = sourceRect.rotation;
            ghost.localScale = new Vector3(
                parentScale.x != 0f ? scale.x / parentScale.x : 1f,
                parentScale.y != 0f ? scale.y / parentScale.y : 1f,
                1f);

            return new Flight(ghost, targetRect.TransformPoint(targetRect.rect.center));
        }
    }
}
