#nullable enable

using System.Collections;
using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    // Container that opens once its ItemSocketInteractable is completed: the door moves from
    // its authored (closed) local pose to openLocalPosition/openLocalEuler, then the item
    // stored inside (inactive until now) is revealed so it can be picked up normally.
    //
    // Put this on the same GameObject as the socket. The animation starts from the socket's
    // OnActivatedAfterDialogue, so it plays after the insert line has been read. Re-entering
    // the room with the socket already activated skips the animation and snaps to the final
    // state (ItemSocketInteractable restores without replaying events, so a short polling
    // window right after enable covers that case -- same approach as SocketLockerSequence).
    public sealed class SocketDoorOpener : MonoBehaviour
    {
        // How long after enable an already-activated socket is still treated as "restored".
        private const float RestoreWindow = 1f;

        [SerializeField] private ItemSocketInteractable socket = null!;

        [Header("Door")]
        [SerializeField] private Transform door = null!;
        [SerializeField] private Vector3   openLocalPosition;
        [SerializeField] private Vector3   openLocalEuler;
        [SerializeField, Min(0f)]    private float startDelay = 0.2f;
        [SerializeField, Min(0.01f)] private float duration   = 1f;

        [Header("Contents")]
        [Tooltip("Starts inactive; revealed once the door is open, unless the player already took it.")]
        [SerializeField] private PickupInteractable? contents;

        private Vector3    closedLocalPosition;
        private Quaternion closedLocalRotation;
        private float      enabledAt;
        private bool       played;

        void Awake()
        {
            this.closedLocalPosition = this.door.localPosition;
            this.closedLocalRotation = this.door.localRotation;
        }

        void OnEnable()
        {
            this.enabledAt = Time.time;
            this.socket.OnActivatedAfterDialogue.AddListener(Play);
        }

        void OnDisable()
        {
            this.socket.OnActivatedAfterDialogue.RemoveListener(Play);
        }

        void Update()
        {
            if (this.played || Time.time - this.enabledAt > RestoreWindow) return;
            if (!this.socket.IsActivated) return;

            this.played = true;
            SetDoor(1f);
            RevealContents();
        }

        private void Play()
        {
            if (this.played) return;
            this.played = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (this.startDelay > 0f)
                yield return new WaitForSeconds(this.startDelay);

            for (float t = 0f; t < 1f; t += Time.deltaTime / this.duration)
            {
                // Ease-out: starts fast, settles as it reaches the open pose.
                SetDoor(1f - (1f - t) * (1f - t));
                yield return null;
            }
            SetDoor(1f);
            RevealContents();
        }

        private void SetDoor(float k)
        {
            this.door.localPosition = Vector3.Lerp(this.closedLocalPosition, this.openLocalPosition, k);
            this.door.localRotation = Quaternion.Slerp(this.closedLocalRotation, Quaternion.Euler(this.openLocalEuler), k);
        }

        private void RevealContents()
        {
            // A collected pickup deactivates itself in Construct -- don't bring it back.
            if (this.contents != null && !this.contents.IsCollected)
                this.contents.gameObject.SetActive(true);
        }
    }
}
