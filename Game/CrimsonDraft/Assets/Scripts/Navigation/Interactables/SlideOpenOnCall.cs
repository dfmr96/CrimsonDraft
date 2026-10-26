#nullable enable

using DG.Tweening;
using UnityEngine;
using CrimsonDraft.Infrastructure;

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
        private bool       hasClosedPose;
        private bool       isOpen;

        void Awake() => CaptureClosedPose();

        // Also on first use: a save restore can call Open() during scene setup, before this Awake runs
        // (the DI scope builds earlier), and must still measure from the authored closed pose.
        private void CaptureClosedPose()
        {
            if (this.hasClosedPose) return;
            this.hasClosedPose       = true;
            this.closedLocalPosition = transform.localPosition;
            this.closedLocalRotation = transform.localRotation;
        }

        public void Open()
        {
            if (this.isOpen) return;
            CaptureClosedPose();
            this.isOpen = true;
            MoveTo(this.closedLocalPosition + this.openLocalOffset,
                   this.closedLocalRotation * Quaternion.Euler(this.openLocalRotationOffset));
        }

        // Not wired to anything by default -- lets the same component be reused on a drawer
        // that needs to close again (a puzzle that can be retried, a different code re-locking it).
        public void Close()
        {
            if (!this.isOpen) return;
            CaptureClosedPose();
            this.isOpen = false;
            MoveTo(this.closedLocalPosition, this.closedLocalRotation);
        }

        // Slides there -- or, while a save is being restored (WorldRestore), is simply already there.
        private void MoveTo(Vector3 localPosition, Quaternion localRotation)
        {
            transform.DOKill();
            if (WorldRestore.IsActive)
            {
                transform.localPosition = localPosition;
                transform.localRotation = localRotation;
                return;
            }
            transform.DOLocalMove(localPosition, this.duration).SetEase(this.ease);
            transform.DOLocalRotateQuaternion(localRotation, this.duration).SetEase(this.ease);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Vector3 closed = this.hasClosedPose ? this.closedLocalPosition : transform.localPosition;
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
