#nullable enable

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class MenuIntroController : MonoBehaviour
    {
        [SerializeField] private GameObject bgPre         = null!;
        [SerializeField] private GameObject bgMain        = null!;
        [SerializeField] private GameObject introTitle    = null!;
        [SerializeField] private GameObject introText     = null!;
        [SerializeField] private Animator   bgPreAnimator = null!;
        [SerializeField] private string     transitionStateName = "Intro";

        private IDisposable? anyButtonSubscription;
        private bool         transitionStarted;

        private void OnEnable()
        {
            this.anyButtonSubscription = InputSystem.onAnyButtonPress.CallOnce(_ => StartTransition());
        }

        private void OnDisable()
        {
            this.anyButtonSubscription?.Dispose();
            this.anyButtonSubscription = null;
        }

        private void StartTransition()
        {
            if (this.transitionStarted) return;
            this.transitionStarted = true;

            this.introTitle.SetActive(false);
            this.introText.SetActive(false);

            this.bgPreAnimator.enabled = true;
            this.bgPreAnimator.Play(this.transitionStateName, 0, 0f);
            StartCoroutine(WaitForAnimationEnd());
        }

        private IEnumerator WaitForAnimationEnd()
        {
            yield return null; // let Play() take effect before reading state info

            while (this.bgPreAnimator.IsInTransition(0) ||
                   this.bgPreAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            {
                yield return null;
            }

            this.bgMain.SetActive(true);
            this.bgPre.SetActive(false);
        }
    }
}
