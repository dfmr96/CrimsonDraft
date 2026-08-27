#nullable enable

using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class MainMenuRevealSequence : MonoBehaviour
    {
        [Header("Bar")]
        [Tooltip("Pivot debe estar en el borde izquierdo (0, 0.5) para que crezca hacia la derecha.")]
        [SerializeField] private RectTransform bar         = null!;
        [SerializeField] private float         barDuration = 0.5f;

        [Header("Small Logo")]
        [SerializeField] private Image smallLogo           = null!;
        [SerializeField] private int   logoFlickerCount    = 3;
        [SerializeField] private float logoFlickerInterval = 0.08f;

        [Header("Botones (orden de aparicion)")]
        [SerializeField] private CanvasGroup[] buttons            = Array.Empty<CanvasGroup>();
        [SerializeField] private float         buttonFadeDuration = 0.2f;
        [SerializeField] private float         buttonStagger      = 0.12f;
        [SerializeField] private Selectable    firstSelected      = null!;

        // Plays once, the first time the intro hands off to this menu. Later returns to this
        // canvas (e.g. Back from the New Game submenu) just show the end state instantly --
        // toggling the parent Title_canva re-fires OnEnable here even though this GameObject's
        // own active flag never changed.
        private bool hasRevealed;

        private void OnEnable()
        {
            if (this.hasRevealed)
            {
                ShowRevealedInstantly();
                return;
            }

            this.hasRevealed = true;

            this.bar.localScale = new Vector3(0f, 1f, 1f);
            SetAlpha(this.smallLogo, 0f);

            foreach (var group in this.buttons)
            {
                group.alpha           = 0f;
                group.interactable    = false;
                group.blocksRaycasts  = false;
            }

            DOTween.Sequence()
                .SetTarget(this)
                .SetUpdate(true)
                .Append(this.bar.DOScaleX(1f, this.barDuration).SetEase(Ease.OutQuad))
                .Append(BuildLogoFlickerSequence())
                .AppendCallback(RevealButtons);
        }

        private void ShowRevealedInstantly()
        {
            this.bar.localScale = Vector3.one;
            SetAlpha(this.smallLogo, 1f);

            foreach (var group in this.buttons)
            {
                group.alpha           = 1f;
                group.interactable    = true;
                group.blocksRaycasts  = true;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem != null && this.firstSelected != null)
                eventSystem.SetSelectedGameObject(this.firstSelected.gameObject);
        }

        private void OnDisable() => DOTween.Kill(this);

        private Sequence BuildLogoFlickerSequence()
        {
            var flicker = DOTween.Sequence().SetTarget(this).SetUpdate(true);
            for (int i = 0; i < this.logoFlickerCount; i++)
            {
                flicker.Append(this.smallLogo.DOFade(1f, this.logoFlickerInterval));
                flicker.Append(this.smallLogo.DOFade(0f, this.logoFlickerInterval));
            }
            flicker.Append(this.smallLogo.DOFade(1f, this.logoFlickerInterval));
            return flicker;
        }

        private void RevealButtons()
        {
            for (int i = 0; i < this.buttons.Length; i++)
            {
                var group = this.buttons[i];
                DOVirtual.DelayedCall(i * this.buttonStagger, () =>
                {
                    group.DOFade(1f, this.buttonFadeDuration).SetTarget(this).SetUpdate(true);
                    group.interactable   = true;
                    group.blocksRaycasts = true;
                }).SetTarget(this).SetUpdate(true);
            }

            var eventSystem = EventSystem.current;
            if (eventSystem != null && this.firstSelected != null)
                eventSystem.SetSelectedGameObject(this.firstSelected.gameObject);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
