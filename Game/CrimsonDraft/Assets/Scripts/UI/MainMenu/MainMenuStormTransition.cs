#nullable enable

using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// The slow, dramatic way into New Game / Load Game: the title buttons fade out, a beat of
    /// silence, a flash of lightning lights the 3D world up for an instant
    /// (<see cref="LightningStrike"/>), the thunder arrives a moment later, and the world behind the
    /// UI is slowly dimmed to near black
    /// so only the sub menu remains. It cannot be skipped. <see cref="Restore"/> undoes the dimming
    /// when the player comes back to the title.
    ///
    /// The dimming is a black Image in an overlay canvas sorted below the UI composite, so it only
    /// darkens the diorama and never the UI drawn on top of it.
    /// </summary>
    public sealed class MainMenuStormTransition : MonoBehaviour
    {
        [SerializeField] private LightningStrike lightning = null!;
        [Tooltip("Raiz del menu principal (MAINMENU): se apaga con fade al empezar.")]
        [SerializeField] private GameObject mainMenuRoot = null!;
        [Tooltip("Imagen negra a pantalla completa, en un canvas Overlay por debajo del composite de la UI.")]
        [SerializeField] private Image backdrop = null!;
        [Range(0f, 1f)]
        [SerializeField] private float dimAlpha = 1f;

        [Header("Plain dim (Options)")]
        [Tooltip("Cuanto se oscurece el fondo al entrar a Options (1 = negro).")]
        [Range(0f, 1f)]
        [SerializeField] private float optionsDimAlpha    = 1f;
        [Tooltip("Debe durar lo que tarda en apagarse la lluvia.")]
        [SerializeField] private float optionsDimDuration = 0.9f;

        [Header("Timing (seconds)")]
        [Tooltip("Los botones del titulo se apagan mientras el cielo empieza a brillar, desde el primer instante.")]
        [SerializeField] private float menuFadeOut   = 0.4f;
        [Tooltip("Extra despues de que la pantalla empieza a salir del blanco, antes de que arranque el oscurecimiento.")]
        [SerializeField] private float dimDelay      = 0f;
        [SerializeField] private float dimDuration   = 2.5f;
        [Tooltip("Desde el impacto hasta el trueno.")]
        [SerializeField] private float thunderDelay  = 0.1f;
        [SerializeField] private float restoreDuration = 0.8f;

        private Sequence? sequence;
        private Tween?    dimTween;
        private CanvasGroup? menuGroup;

        public bool IsPlaying { get; private set; }

        /// <param name="onThunder">Time to play the thunder sound.</param>
        /// <param name="onDarkened">The background is dark: open the sub menu now.</param>
        public void Play(Action? onThunder, Action onDarkened)
        {
            Kill();
            this.IsPlaying = true;

            this.menuGroup = this.mainMenuRoot.GetComponent<CanvasGroup>();
            if (this.menuGroup == null) this.menuGroup = this.mainMenuRoot.AddComponent<CanvasGroup>();
            this.menuGroup.blocksRaycasts = false;

            SetBackdropAlpha(0f);
            this.backdrop.gameObject.SetActive(true);

            // The effect starts the instant the button is pressed; the title fades out under it.
            this.sequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(this)
                .Append(DOVirtual.Float(this.menuGroup.alpha, 0f, this.menuFadeOut, v => this.menuGroup.alpha = v));

            this.lightning.Play(
                onImpact:   () => OnImpact(onThunder, onDarkened),
                onFinished: null);
        }

        /// <summary>
        /// Just a dim, no lightning: the title buttons fade out and the world darkens a bit.
        /// <paramref name="onDimmed"/> runs when it is done (the caller opens Options then).
        /// <see cref="Restore"/> undoes it.
        /// </summary>
        public void PlayDim(Action onDimmed)
        {
            Kill();
            this.IsPlaying = true;

            this.menuGroup = this.mainMenuRoot.GetComponent<CanvasGroup>();
            if (this.menuGroup == null) this.menuGroup = this.mainMenuRoot.AddComponent<CanvasGroup>();
            this.menuGroup.blocksRaycasts = false;

            this.backdrop.gameObject.SetActive(true);
            float from = this.backdrop.color.a;

            this.sequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(this)
                .Append(DOVirtual.Float(this.menuGroup.alpha, 0f, this.menuFadeOut, v => this.menuGroup.alpha = v))
                .Join(DOVirtual.Float(from, this.optionsDimAlpha, this.optionsDimDuration, SetBackdropAlpha).SetEase(Ease.InOutSine))
                .OnComplete(() =>
                {
                    this.sequence  = null;
                    this.IsPlaying = false;
                    onDimmed();
                });
        }

        private void OnImpact(Action? onThunder, Action onDarkened)
        {
            DOVirtual.DelayedCall(this.thunderDelay, () => onThunder?.Invoke(), ignoreTimeScale: true).SetTarget(this);

            this.dimTween = DOVirtual.Float(0f, this.dimAlpha, this.dimDuration, SetBackdropAlpha)
                .SetDelay(this.lightning.WhiteHoldAfterImpact + this.dimDelay)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    this.dimTween = null;
                    this.IsPlaying = false;
                    onDarkened();
                });
        }

        /// <summary>Back at the title: the title buttons return at once and the world brightens again.</summary>
        public void Restore()
        {
            Kill();
            this.IsPlaying = false;

            if (this.menuGroup != null)
            {
                this.menuGroup.alpha = 1f;
                this.menuGroup.blocksRaycasts = true;
            }

            if (!this.backdrop.gameObject.activeSelf) return;
            this.dimTween = DOVirtual.Float(this.backdrop.color.a, 0f, this.restoreDuration, SetBackdropAlpha)
                .SetEase(Ease.OutSine)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    this.dimTween = null;
                    this.backdrop.gameObject.SetActive(false);
                });
        }

        private void OnDestroy() => Kill();

        private void Kill()
        {
            DOTween.Kill(this);
            this.sequence = null;
            this.dimTween = null;
        }

        private void SetBackdropAlpha(float value)
        {
            var color = this.backdrop.color;
            color.a = value;
            this.backdrop.color = color;
        }
    }
}
