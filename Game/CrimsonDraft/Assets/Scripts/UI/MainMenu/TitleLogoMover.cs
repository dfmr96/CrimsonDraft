#nullable enable

using DG.Tweening;
using UnityEngine;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// The big title logo stays on screen through the "press any button" intro (it lives outside the
    /// INTRO group that fades away) and, once the intro has finished, slides up to its place above the
    /// main menu. <see cref="MoveUp"/> is wired to PressAnyButtonIntro's onIntroFinished.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TitleLogoMover : MonoBehaviour
    {
        [Tooltip("Anchored Y final del logo, en unidades del canvas.")]
        [SerializeField] private float targetY  = 71.3f;
        [SerializeField] private float duration = 1f;
        [SerializeField] private Ease  ease     = Ease.InOutSine;

        private RectTransform rectTransform = null!;
        private float         startY;

        private void Awake()
        {
            this.rectTransform = (RectTransform)transform;
            this.startY        = this.rectTransform.anchoredPosition.y;
        }

        /// <summary>Slides the logo up to <c>targetY</c>.</summary>
        public void MoveUp()
        {
            DOTween.Kill(this.rectTransform);
            this.rectTransform
                .DOAnchorPosY(this.targetY, this.duration)
                .SetTarget(this.rectTransform)
                .SetEase(this.ease)
                .SetUpdate(true);
        }

        private void OnDisable() => DOTween.Kill(this.rectTransform);

        /// <summary>Puts the logo back where it starts (editor / replays).</summary>
        public void ResetPosition()
        {
            DOTween.Kill(this.rectTransform);
            var pos = this.rectTransform.anchoredPosition;
            pos.y = this.startY;
            this.rectTransform.anchoredPosition = pos;
        }
    }
}
