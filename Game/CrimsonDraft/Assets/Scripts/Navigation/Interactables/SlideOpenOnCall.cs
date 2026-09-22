#nullable enable

using DG.Tweening;
using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>Slides this transform from wherever it starts (the "closed" position) to an
    /// offset "open" position, faking a drawer/panel sliding out. Open() is parameterless so it
    /// can be wired directly as a UnityEvent persistent call in the inspector -- e.g. from
    /// BeeperReceiverInteractable.onCodeAccepted or ItemSocketInteractable.onActivated -- right
    /// alongside a plain GameObject.SetActive(true) call on whatever item was hidden behind it.
    /// Idempotent: calling Open() again while already open/opening does nothing.</summary>
    public sealed class SlideOpenOnCall : MonoBehaviour
    {
        [SerializeField] private Vector3 openLocalOffset         = new(0f, 0f, -0.3f); // relative to the closed position, local space
        [SerializeField] private Vector3 openLocalRotationOffset = Vector3.zero;       // euler angles, relative to the closed rotation, local axes
        [SerializeField] private float   duration        = 0.6f;
        [SerializeField] private Ease    ease             = Ease.OutQuad;

        private Vector3    closedLocalPosition;
        private Quaternion closedLocalRotation;
        private bool       isOpen;

        void Awake()
        {
            this.closedLocalPosition = transform.localPosition;
            this.closedLocalRotation = transform.localRotation;
        }

        public void Open()
        {
            if (this.isOpen) return;
            this.isOpen = true;

            transform.DOKill();
            transform.DOLocalMove(this.closedLocalPosition + this.openLocalOffset, this.duration).SetEase(this.ease);
            transform.DOLocalRotateQuaternion(this.closedLocalRotation * Quaternion.Euler(this.openLocalRotationOffset), this.duration).SetEase(this.ease);
        }

        // Not wired to anything by default -- lets the same component be reused on a drawer
        // that needs to close again (a puzzle that can be retried, a different code re-locking it).
        public void Close()
        {
            if (!this.isOpen) return;
            this.isOpen = false;

            transform.DOKill();
            transform.DOLocalMove(this.closedLocalPosition, this.duration).SetEase(this.ease);
            transform.DOLocalRotateQuaternion(this.closedLocalRotation, this.duration).SetEase(this.ease);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Vector3 closed = Application.isPlaying ? this.closedLocalPosition : transform.localPosition;
            Vector3 openWorld = transform.parent != null
                ? transform.parent.TransformPoint(closed + this.openLocalOffset)
                : closed + this.openLocalOffset;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, openWorld);
            Gizmos.DrawWireSphere(openWorld, 0.05f);
        }
#endif
    }
}
