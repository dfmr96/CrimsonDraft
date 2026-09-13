#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Combat
{
    // Drives the color contrast between an operator card's "not their turn" and
    // "their turn" states. Both colors are the card's normal olive tint (9A9F5C) --
    // the border no longer changes color on turn change, only the flicker sequence
    // (kept for the attention beat) plays between identical colors.
    public sealed class OperatorTurnHighlight : MonoBehaviour
    {
        [SerializeField] private Image[] tintTargets = System.Array.Empty<Image>();
        [SerializeField] private Color inactiveColor = new(0.6039216f, 0.62352943f, 0.36078432f, 1f);
        [SerializeField] private Color activeColor = new(0.6039216f, 0.62352943f, 0.36078432f, 1f);
        [SerializeField] private float inactiveFadeDuration = 0.2f;
        [SerializeField, Min(1)] private int flickerCount = 3;
        [SerializeField] private float flickerDuration = 0.4f;

        public void SetActive(bool isActive)
        {
            foreach (var img in this.tintTargets)
            {
                if (img == null) continue;
                img.DOKill();

                if (!isActive)
                {
                    img.DOColor(this.inactiveColor, this.inactiveFadeDuration);
                    continue;
                }

                float step = this.flickerDuration / (this.flickerCount * 2f);
                var seq = DOTween.Sequence().SetTarget(img);
                for (int i = 0; i < this.flickerCount; i++)
                {
                    seq.Append(img.DOColor(this.inactiveColor, step));
                    seq.Append(img.DOColor(this.activeColor, step));
                }
            }
        }

        private void OnDisable()
        {
            foreach (var img in this.tintTargets)
                img?.DOKill();
        }
    }
}
