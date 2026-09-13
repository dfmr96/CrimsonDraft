#nullable enable

using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class PressAnyButtonIntro : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text    pressAnyButtonText = null!;
        [SerializeField] private CanvasGroup introGroup         = null!; // Fades text + logo together.

        [Header("Blink")]
        [Tooltip("Duracion de cada tramo del parpadeo (apagado->prendido o viceversa).")]
        [SerializeField] private float blinkInterval = 0.5f;

        [Header("Al confirmar")]
        [Tooltip("Cuanto se queda el texto quieto (visible) antes de agrandarse.")]
        [SerializeField] private float freezeDuration  = 1f;
        [SerializeField] private float scaleUpAmount   = 1.15f;
        [SerializeField] private float scaleDuration   = 0.2f;
        [SerializeField] private float fadeOutDuration = 1f;

        [Header("Events")]
        [SerializeField] private UnityEvent onIntroFinished = new();

        private IDisposable? anyButtonSubscription;
        private Vector3      textBaseScale;
        private Sequence?    confirmSequence;
        private bool         confirmed;

        private void Awake() => this.textBaseScale = this.pressAnyButtonText.rectTransform.localScale;

        private void OnEnable()
        {
            this.confirmed = false;
            this.introGroup.alpha = 1f;
            this.pressAnyButtonText.alpha = 1f;
            this.pressAnyButtonText.rectTransform.localScale = this.textBaseScale;

            this.pressAnyButtonText
                .DOFade(0f, this.blinkInterval)
                .SetTarget(this.pressAnyButtonText)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);

            this.anyButtonSubscription = InputSystem.onAnyButtonPress.CallOnce(_ => OnAnyButtonPressed());
        }

        private void OnDisable()
        {
            this.anyButtonSubscription?.Dispose();
            this.anyButtonSubscription = null;

            DOTween.Kill(this.pressAnyButtonText);
            DOTween.Kill(this.pressAnyButtonText.rectTransform);
            DOTween.Kill(this.introGroup);
            this.confirmSequence?.Kill();
            this.confirmSequence = null;
        }

        private void OnAnyButtonPressed()
        {
            if (this.confirmed) return;
            this.confirmed = true;

            DOTween.Kill(this.pressAnyButtonText); // stop blinking, freeze fully visible
            this.pressAnyButtonText.alpha = 1f;

            this.confirmSequence = DOTween.Sequence()
                .SetTarget(this)
                .SetUpdate(true)
                .AppendInterval(this.freezeDuration)
                .Join(this.pressAnyButtonText.rectTransform.DOScale(this.textBaseScale * this.scaleUpAmount, this.scaleDuration).SetEase(Ease.OutBack))
                .Append(this.introGroup.DOFade(0f, this.fadeOutDuration).SetEase(Ease.InQuad))
                .OnComplete(() =>
                {
                    this.onIntroFinished.Invoke();
                    this.gameObject.SetActive(false);
                });
        }
    }
}
