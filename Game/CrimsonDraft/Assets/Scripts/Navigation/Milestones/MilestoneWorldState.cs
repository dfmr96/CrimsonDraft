#nullable enable

using System;
using UnityEngine.Scripting;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Gates;

namespace CrimsonDraft.Navigation.Milestones
{
    public sealed class MilestoneWorldState : IMilestoneWorldState
    {
        private readonly PickupRegistry    pickups;
        private readonly NoteRegistry      notes;
        private readonly DoorStateRegistry doors;
        private readonly IGateStates       gates;
        private readonly IInventoryService inventory;

        public event Action<MilestoneSource>? SourceChanged;

        [Preserve]
        public MilestoneWorldState(
            PickupRegistry pickups, NoteRegistry notes, DoorStateRegistry doors, IGateStates gates, IInventoryService inventory)
        {
            this.pickups = pickups; this.notes = notes; this.doors = doors;
            this.gates = gates; this.inventory = inventory;

            this.pickups.Changed           += OnPickups;
            this.inventory.ContentsChanged += OnInventory;
            this.doors.Changed             += OnDoors;
            this.notes.Changed             += OnNotes;
            this.gates.Changed             += OnGates;
        }

        public bool IsPickupCollected(string pickupId) => this.pickups.IsCollected(pickupId);
        public bool HasItem(string itemId)             => this.inventory.HasItem(itemId);
        public bool IsDoorUnlocked(string doorId)      => this.doors.IsUnlocked(doorId);
        public bool IsNoteRead(string noteId)          => this.notes.IsCollected(noteId);
        public bool IsGateCompleted(string gateId)     => this.gates.IsCompleted(gateId);

        private void OnPickups()   => SourceChanged?.Invoke(MilestoneSource.Pickups);
        private void OnInventory() => SourceChanged?.Invoke(MilestoneSource.Inventory);
        private void OnDoors()     => SourceChanged?.Invoke(MilestoneSource.Doors);
        private void OnNotes()     => SourceChanged?.Invoke(MilestoneSource.Notes);
        private void OnGates()     => SourceChanged?.Invoke(MilestoneSource.Gates);

        public void Dispose()
        {
            this.pickups.Changed           -= OnPickups;
            this.inventory.ContentsChanged -= OnInventory;
            this.doors.Changed             -= OnDoors;
            this.notes.Changed             -= OnNotes;
            this.gates.Changed             -= OnGates;
        }
    }
}
