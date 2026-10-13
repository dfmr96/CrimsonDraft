#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>Wires this room's beeper pickup (a PickupInteractable whose openNoteInstead
    /// points at Note_B01 -- the operator card the player reads to unlock the BEEPER tab, see
    /// TabManager's beeperUnlockNoteId), its BeeperReceiverInteractable code-toggle, and the
    /// radio's visual feedback together. Sits beside BeeperReceiverInteractable and
    /// BeeperRadioIndicator on the trigger GameObject. Picking up beeperPickup locks door,
    /// reveals bigPaint, and switches the radio on (BeeperRadioIndicator.Activate() -- before
    /// that the radio keeps its default screen material). Each accepted SOS while in range then
    /// toggles door lock / bigPaint, and the radio flashes acceptedFlashMaterial for
    /// acceptedFlashDuration before resuming its in/out-of-range cycle.</summary>
    public sealed class BeeperDoorMechanism : MonoBehaviour
    {
        [SerializeField] private PickupInteractable         beeperPickup = null!;
        [SerializeField] private BeeperReceiverInteractable receiver     = null!;
        [SerializeField] private BeeperRadioIndicator       radio        = null!;
        [SerializeField] private RoomDoorInteractable       door         = null!;
        [SerializeField] private GameObject                 bigPaint     = null!;

        [SerializeField] private Material acceptedFlashMaterial = null!;
        [SerializeField] private float    acceptedFlashDuration = 2f;

        private bool triggered;

        void Awake()
        {
            this.beeperPickup.OnPickedUp.AddListener(OnBeeperCollected);
            this.receiver.OnCodeAccepted.AddListener(OnCodeAccepted);
        }

        private void OnBeeperCollected()
        {
            if (this.triggered) return;
            this.triggered = true;

            this.door.SetMechanismLocked(true);
            if (this.bigPaint != null) this.bigPaint.SetActive(true);
            this.radio.Activate();
        }

        private void OnCodeAccepted()
        {
            if (!this.triggered) return;

            bool nowLocked = !this.door.MechanismLocked;
            this.door.SetMechanismLocked(nowLocked);
            if (this.bigPaint != null) this.bigPaint.SetActive(nowLocked);

            this.radio.PlayAcceptedFlash(this.acceptedFlashMaterial, this.acceptedFlashDuration);
        }
    }
}
