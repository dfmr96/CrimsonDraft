#nullable enable

using System;
using System.Linq;
using UnityEngine.Scripting;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Milestones
{
    public sealed class MilestoneWorldState : IMilestoneWorldState
    {
        private readonly PickupRegistry          pickups;
        private readonly NoteRegistry            notes;
        private readonly ItemSocketStateRegistry sockets;
        private readonly DoorStateRegistry       doors;
        private readonly PuzzleStateRegistry     puzzles;
        private readonly IInventoryService       inventory;

        public event Action<MilestoneSource>? SourceChanged;

        [Preserve]
        public MilestoneWorldState(
            PickupRegistry pickups, NoteRegistry notes, ItemSocketStateRegistry sockets,
            DoorStateRegistry doors, PuzzleStateRegistry puzzles, IInventoryService inventory)
        {
            this.pickups = pickups; this.notes = notes; this.sockets = sockets;
            this.doors = doors; this.puzzles = puzzles; this.inventory = inventory;

            this.pickups.Changed           += OnPickups;
            this.inventory.ContentsChanged += OnInventory;
            this.sockets.Changed           += OnSockets;
            this.doors.Changed             += OnDoors;
            this.notes.Changed             += OnNotes;
            this.puzzles.Changed           += OnPuzzles;
        }

        public bool IsPickupCollected(string pickupId) => this.pickups.IsCollected(pickupId);
        public bool HasItem(string itemId)             => this.inventory.HasItem(itemId);
        public bool IsDoorUnlocked(string doorId)      => this.doors.IsUnlocked(doorId);
        public bool IsNoteRead(string noteId)          => this.notes.IsCollected(noteId);
        public bool IsPuzzleSolved(string puzzleId)    => this.puzzles.IsSolved(puzzleId);

        public bool IsSocketComplete(string socketId)
        {
            var slots = this.sockets.GetInserted(socketId);
            return slots.Length > 0 && slots.All(s => s);
        }

        private void OnPickups()   => SourceChanged?.Invoke(MilestoneSource.Pickups);
        private void OnInventory() => SourceChanged?.Invoke(MilestoneSource.Inventory);
        private void OnSockets()   => SourceChanged?.Invoke(MilestoneSource.Sockets);
        private void OnDoors()     => SourceChanged?.Invoke(MilestoneSource.Doors);
        private void OnNotes()     => SourceChanged?.Invoke(MilestoneSource.Notes);
        private void OnPuzzles()   => SourceChanged?.Invoke(MilestoneSource.Puzzles);

        public void Dispose()
        {
            this.pickups.Changed           -= OnPickups;
            this.inventory.ContentsChanged -= OnInventory;
            this.sockets.Changed           -= OnSockets;
            this.doors.Changed             -= OnDoors;
            this.notes.Changed             -= OnNotes;
            this.puzzles.Changed           -= OnPuzzles;
        }
    }
}
