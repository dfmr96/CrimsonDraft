#nullable enable

using System;
using UnityEngine;
using VContainer;
using CrimsonDraft.Navigation.Gates;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>Keeps a door mechanism-locked until a puzzle is solved (e.g. the Energy_Room
    /// maintenance door, dead until the generator's fuse puzzle is done). The door must have
    /// useMechanismLock on, with its mechanismLockedDialogue set. The door's wall switch shows
    /// its Switch-off child while locked and Switch-on once the puzzle is solved -- same
    /// parent-model / Switch-on / Switch-off layout GeneratorSwitchPanel's switches use.
    /// Reads the saved puzzle state, so it holds across scene loads and saves.</summary>
    public sealed class PuzzleDoorLock : MonoBehaviour
    {
        private const string OnChildName  = "Switch-on";
        private const string OffChildName = "Switch-off";

        [SerializeField] private RoomDoorInteractable door = null!;
        [SerializeField] private string               puzzleId = "";
        [SerializeField] private Transform?           doorSwitch;

        [Inject] private IGateStates gates = null!;

        void Start()
        {
            Apply();
            this.gates.Changed += Apply;
        }

        void OnDestroy()
        {
            if (this.gates != null) this.gates.Changed -= Apply;
        }

        private void Apply()
        {
            bool solved = this.gates.IsCompleted(GateIds.Puzzle(this.puzzleId));
            this.door.SetMechanismLocked(!solved);
            ApplySwitchVisual(this.doorSwitch, on: solved);
        }

        // The switch's own mesh is the "default" model; hide it so the on/off child shows.
        private static void ApplySwitchVisual(Transform? switchTransform, bool on)
        {
            if (switchTransform == null) return;

            var ownRenderer = switchTransform.GetComponent<Renderer>();
            if (ownRenderer != null) ownRenderer.enabled = false;

            var onChild  = switchTransform.Find(OnChildName);
            var offChild = switchTransform.Find(OffChildName);
            if (onChild  != null) onChild.gameObject.SetActive(on);
            if (offChild != null) offChild.gameObject.SetActive(!on);
        }
    }
}
