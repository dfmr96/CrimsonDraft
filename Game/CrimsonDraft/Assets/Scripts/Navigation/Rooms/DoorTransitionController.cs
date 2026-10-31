#nullable enable

using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class DoorTransitionController : MonoBehaviour
    {
        [SerializeField] private float        animationTimeout = 5f;
        [SerializeField] private float        fadeInDuration   = 0.8f;
        [SerializeField] private float        fadeOutDuration  = 0.4f;
        [SerializeField] private Ease         fadeInEase       = Ease.InQuad;
        [SerializeField] private Ease         fadeOutEase      = Ease.OutQuad;
        [SerializeField] private CanvasGroup? fadeOverlay;
        [SerializeField] private GameObject?  defaultDoorPrefab;
        [SerializeField] private AK.Wwise.Event  doorEvent   = new();
        [SerializeField] private AK.Wwise.Switch openSwitch  = new();
        [SerializeField] private AK.Wwise.Switch closeSwitch = new();

        private RoomTransitionContext? context;
        // Kept apart from context.SkipAction: NotifyComplete clears that one before OnDestroy runs.
        private InputAction?           skipAction;
        private bool                   completed;
        private bool                   canSkip;
        // Set by the clip's OnDoorOpenSfx event: only a transition that opened a door closes one,
        // so prefabs without that event (e.g. the stairs) stay silent.
        private bool                   doorOpened;

        private void Awake()
        {
            if (this.fadeOverlay != null)
                this.fadeOverlay.alpha = 1f;
        }

        private void OnDestroy()
        {
            if (this.skipAction != null)
                this.skipAction.performed -= OnSkip;
        }

        private void OnSkip(InputAction.CallbackContext _)
        {
            if (!this.canSkip) return;
            this.canSkip = false;
            DOTween.Kill(this.fadeOverlay);
            OnAnimationComplete();
        }

        private void Start()
        {
            this.context = Resources.Load<RoomTransitionContext>("RoomTransitionContext");

            if (this.context == null)
            {
                Debug.LogError("[DoorTransitionController] RoomTransitionContext not found in Resources.");
                return;
            }

            this.skipAction = this.context.SkipAction;
            if (this.skipAction != null)
                this.skipAction.performed += OnSkip;

            var prefab    = this.context.DoorPrefab ?? this.defaultDoorPrefab;
            Animator? animator = null;

            if (prefab != null)
            {
                var door = Instantiate(prefab, transform);
                door.transform.localPosition = Vector3.zero;
                door.transform.localRotation = Quaternion.identity;

                animator = door.GetComponentInChildren<Animator>();
                if (animator != null)
                    animator.gameObject.AddComponent<DoorAnimationRelay>().Init(this);
            }
            else
            {
                Debug.LogWarning("[DoorTransitionController] No door prefab assigned — transition will fade without door.");
            }

            RunTransition(animator).Forget();
        }

        private async UniTaskVoid RunTransition(Animator? animator)
        {
            this.canSkip = true;
            await Fade(0f, this.fadeInDuration, this.fadeInEase);

            if (animator == null)
            {
                OnAnimationComplete();
                return;
            }

            TimeoutFallback().Forget();
        }

        internal void PlayDoorOpenSfx()
        {
            this.doorOpened = true;
            this.openSwitch.SetValue(gameObject);
            this.doorEvent.Post(gameObject);
        }

        internal void OnAnimationComplete()
        {
            if (this.completed) return;
            this.completed = true;
            // Once completing, a skip must not run: its DOTween.Kill would cancel the fade-out
            // without firing OnComplete, so NotifyComplete never comes and DoorTransition stays loaded.
            this.canSkip = false;

            if (this.doorOpened)
            {
                this.closeSwitch.SetValue(gameObject);
                this.doorEvent.Post(gameObject);
            }

            FadeOutAndComplete().Forget();
        }

        private async UniTaskVoid FadeOutAndComplete()
        {
            await Fade(1f, this.fadeOutDuration, this.fadeOutEase);
            this.context?.NotifyComplete();
        }

        private async UniTaskVoid TimeoutFallback()
        {
            await UniTask.WaitForSeconds(this.animationTimeout, ignoreTimeScale: true);

            if (!this.completed)
            {
                Debug.LogWarning("[DoorTransitionController] Animation timeout — forcing transition complete.");
                OnAnimationComplete();
            }
        }

        private UniTask Fade(float to, float duration, Ease ease)
        {
            if (this.fadeOverlay == null)
                return UniTask.CompletedTask;

            var tcs = new UniTaskCompletionSource();
            this.fadeOverlay
                .DOFade(to, duration)
                .SetEase(ease)
                .SetUpdate(true)
                // OnKill also fires after a completed tween, so a Kill can never leave the await hanging.
                .OnKill(() => tcs.TrySetResult());
            return tcs.Task;
        }
    }
}
