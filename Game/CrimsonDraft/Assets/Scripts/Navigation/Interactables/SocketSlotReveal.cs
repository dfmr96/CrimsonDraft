#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    // Shows one GameObject per slot of a multi-slot ItemSocketInteractable as that slot gets
    // filled (revealPerSlot[i] <-> requiredItems[i]). Put it next to the socket.
    //
    // Syncs and subscribes in OnEnable on purpose: the socket may sit under an object that is
    // only switched on later (e.g. the chef room's board, revealed by the beeper). Awake would
    // not have run by then, and the registry restore (SlotFilled with animate=false) would
    // have been missed -- IsSlotFilled still holds the restored state once we are enabled.
    public sealed class SocketSlotReveal : MonoBehaviour
    {
        [SerializeField] private ItemSocketInteractable socket = null!;
        [SerializeField] private GameObject[] revealPerSlot = System.Array.Empty<GameObject>();

        void OnEnable()
        {
            this.socket.SlotFilled += OnSlotFilled;
            for (int i = 0; i < this.revealPerSlot.Length; i++)
                if (this.socket.IsSlotFilled(i)) Reveal(i);
        }

        void OnDisable()
        {
            this.socket.SlotFilled -= OnSlotFilled;
        }

        private void OnSlotFilled(int index, bool animate) => Reveal(index);

        private void Reveal(int index)
        {
            if (index >= 0 && index < this.revealPerSlot.Length && this.revealPerSlot[index] != null)
                this.revealPerSlot[index].SetActive(true);
        }
    }
}
