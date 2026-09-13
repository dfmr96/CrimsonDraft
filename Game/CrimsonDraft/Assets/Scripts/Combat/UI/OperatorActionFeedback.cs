#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Combat
{
    // Plays the shared "use" flipbook (selector_animation.png / Image.controller — the same
    // clip the combat inventory panel plays for item-use feedback) across the whole card as
    // a fire-and-forget flourish when an order is given. Purely decorative — nothing in the
    // combat flow waits on it, it just shows briefly and hides itself again.
    public sealed class OperatorActionFeedback : MonoBehaviour
    {
        [SerializeField] private float timeoutSeconds = 1.5f; // safety net if the event never fires
        [SerializeField] private float playbackSpeed   = 2f;  // this instance only — the shared clip/controller stays untouched, so the inventory's own use-animation keeps its speed

        private static readonly int UseTriggerHash = Animator.StringToHash("Use");

        private Animator animator = null!;
        private Image    image    = null!;
        private bool     completed = true;

        private void Awake()
        {
            this.animator = GetComponent<Animator>();
            this.image    = GetComponent<Image>();
            this.animator.speed = this.playbackSpeed;
            this.image.enabled = false;
        }

        public void Play()
        {
            if (this.animator == null) return;

            this.completed      = false;
            this.image.enabled  = true;
            this.animator.ResetTrigger(UseTriggerHash);
            this.animator.SetTrigger(UseTriggerHash);
            TimeoutFallback().Forget();
        }

        // Animation Event target on the shared clip — name/signature must stay exactly this.
        public void OnUseAnimationComplete()
        {
            if (this.completed) return;
            this.completed = true;
            this.image.enabled = false;
        }

        private async UniTaskVoid TimeoutFallback()
        {
            await UniTask.WaitForSeconds(this.timeoutSeconds, ignoreTimeScale: true);
            if (!this.completed) OnUseAnimationComplete();
        }

        private void OnDisable()
        {
            this.completed = true;
        }
    }
}
