#nullable enable

using System;

namespace CrimsonDraft.Navigation.Milestones
{
    public enum MilestoneSource { None, Pickups, Inventory, Sockets, Doors, Notes, Puzzles }

    // Read-only view of the game state milestone triggers test against, plus one change signal
    // per source. MilestoneWorldState adapts the real registries; tests use a fake.
    public interface IMilestoneWorldState : IDisposable
    {
        bool IsPickupCollected(string pickupId);
        bool HasItem(string itemId);
        bool IsSocketComplete(string socketId);
        bool IsDoorUnlocked(string doorId);
        bool IsNoteRead(string noteId);
        bool IsPuzzleSolved(string puzzleId);

        event Action<MilestoneSource>? SourceChanged;
    }
}
