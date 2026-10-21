#nullable enable

using System;
using UnityEngine;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>Two-stage visual for a single-slot ItemSocketInteractable: the placed visual shows
    /// once the item is inserted, then swaps to the mutated visual the first time the player
    /// enters <see cref="triggerRoomId"/> AFTER the insert (entering it earlier doesn't count).
    /// The room check lives in RoomVisitMutationTracker because this object's room is switched
    /// off while the player is elsewhere. The "mutated" flag is stored in ItemSocketStateRegistry
    /// under a derived key, so it is saved and reset together with the sockets.</summary>
    public sealed class RoomVisitMutation : MonoBehaviour
    {
        [SerializeField] private ItemSocketInteractable socket = null!;
        [SerializeField] private string                 triggerRoomId = "ENERGY_ROOM";
        [Tooltip("Shown from the moment the item is inserted until the mutation happens.")]
        [SerializeField] private GameObject?            placedVisual;
        [Tooltip("Shown once the mutation has happened (e.g. the pickable KI_19).")]
        [SerializeField] private GameObject?            mutatedVisual;

        private ItemSocketStateRegistry? registry;

        private string MutatedKey => this.socket.SocketId + "#mutated";

        void Awake()
        {
            this.socket.OnActivated.AddListener(Refresh);
        }

        void OnEnable() => Refresh();

        // Called by RoomVisitMutationTracker once per scene load.
        public void Construct(ItemSocketStateRegistry stateRegistry)
        {
            this.registry = stateRegistry;
            Refresh();
        }

        /// <summary>Called on every room transition; mutates if this is the trigger room and the item is already placed.</summary>
        public void OnRoomEntered(string roomId)
        {
            if (this.registry == null) return;
            if (!string.Equals(roomId, this.triggerRoomId, StringComparison.OrdinalIgnoreCase)) return;
            if (!IsPlaced() || IsMutated()) return;

            this.registry.SetInserted(MutatedKey, new[] { true });
            Refresh();
        }

        private bool IsPlaced()
        {
            var slots = this.registry!.GetInserted(this.socket.SocketId);
            return slots.Length > 0 && slots[0];
        }

        private bool IsMutated()
        {
            var flag = this.registry!.GetInserted(MutatedKey);
            return flag.Length > 0 && flag[0];
        }

        private void Refresh()
        {
            if (this.registry == null) return;

            bool mutated = IsMutated();
            if (this.placedVisual != null)  this.placedVisual.SetActive(IsPlaced() && !mutated);
            if (this.mutatedVisual != null) this.mutatedVisual.SetActive(mutated);
        }
    }
}
