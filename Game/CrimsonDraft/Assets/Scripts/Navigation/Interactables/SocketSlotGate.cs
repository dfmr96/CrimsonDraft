#nullable enable

using UnityEngine;
using CrimsonDraft.Navigation.Gates;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>
    /// A gate that completes when ONE slot of a socket is filled, for sockets that take several items
    /// (the jewelry box: "the yellow gem is in"). The socket's own gate waits for every slot. Put this on
    /// its own child object so dragging that object into a camera zone's gates (or picking it in a
    /// milestone) selects the gate, not another component of the socket's object.
    /// </summary>
    public sealed class SocketSlotGate : MonoBehaviour, IGate
    {
        [SerializeField] private ItemSocketInteractable? socket;

        [Tooltip("Index in the socket's required items (the order they are listed in the socket).")]
        [SerializeField, Min(0)] private int slotIndex;

        public string GateId => this.socket != null ? GateIds.SocketSlot(this.socket.SocketId, this.slotIndex) : "";

        private void Reset() => this.socket = GetComponentInParent<ItemSocketInteractable>();
    }
}
