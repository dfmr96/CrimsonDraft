#nullable enable

using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Gates;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>Wires this room's beeper pickup (a PickupInteractable whose openNoteInstead
    /// points at Note_B01 and has alsoAddItem set, so it shows the note and puts the beeper item KI_21
    /// in the inventory -- TabManager shows the BEEPER tab while a living operator carries it), its BeeperReceiverInteractable code-toggle, and the
    /// radio's visual feedback together. Sits beside BeeperReceiverInteractable and
    /// BeeperRadioIndicator on the trigger GameObject. Picking up beeperPickup locks door,
    /// reveals bigPaint, and switches the radio on (BeeperRadioIndicator.Activate() -- before
    /// that the radio keeps its default screen material). Each accepted SOS while in range then
    /// toggles door lock / bigPaint, and the radio flashes acceptedFlashMaterial for
    /// acceptedFlashDuration before resuming its in/out-of-range cycle.</summary>
    public sealed class BeeperDoorMechanism : MonoBehaviour, IPuzzle
    {
        [SerializeField] private PickupInteractable         beeperPickup = null!;
        [SerializeField] private BeeperReceiverInteractable receiver     = null!;
        [SerializeField] private BeeperRadioIndicator       radio        = null!;
        [SerializeField] private RoomDoorInteractable       door         = null!;
        [SerializeField] private GameObject                 bigPaint     = null!;

        [SerializeField] private Material acceptedFlashMaterial = null!;
        [SerializeField] private float    acceptedFlashDuration = 2f;

        [SerializeField] private string puzzleId = "beeper_door";

        private bool triggered;
        private PuzzleStateRegistry? puzzleStates;

        public string PuzzleId => this.puzzleId;
        public string GateId => GateIds.Puzzle(this.PuzzleId);
        public void BindPuzzleState(PuzzleStateRegistry registry) => this.puzzleStates = registry;

        void Awake()
        {
            this.beeperPickup.OnPickedUp.AddListener(OnBeeperCollected);
            this.receiver.OnCodeAccepted.AddListener(OnCodeAccepted);

            // The paint stays hidden until the beeper is collected (or an accepted SOS reveals it).
            if (this.bigPaint != null) this.bigPaint.SetActive(false);
        }

        // Runs after DI has injected the pickup's registry, so IsCollected is reliable here. On a
        // scene change or a save load the beeper is already collected and its pickup is gone, so
        // OnBeeperCollected never fires -- switch the radio back on directly. Door/paint are left
        // at their fresh-scene state (unlocked, hidden), as the mechanism toggle isn't persisted.
        void Start()
        {
            if (this.triggered || !this.beeperPickup.IsCollected) return;

            this.triggered = true;
            this.radio.Activate();
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
            this.puzzleStates?.SetSolved(this.puzzleId);

            bool nowLocked = !this.door.MechanismLocked;
            this.door.SetMechanismLocked(nowLocked);
            if (this.bigPaint != null) this.bigPaint.SetActive(nowLocked);

            this.radio.PlayAcceptedFlash(this.acceptedFlashMaterial, this.acceptedFlashDuration);
        }
    }
}
