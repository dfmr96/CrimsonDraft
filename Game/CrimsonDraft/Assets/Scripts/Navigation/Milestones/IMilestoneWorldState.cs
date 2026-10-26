#nullable enable

using System;

namespace CrimsonDraft.Navigation.Milestones
{
    public enum MilestoneSource { None, Pickups, Inventory, Doors, Notes, Gates }

    // Read-only view of the game state milestone triggers test against, plus one change signal
    // per source. MilestoneWorldState adapts the real registries; tests use a fake.
    public interface IMilestoneWorldState : IDisposable
    {
        bool IsPickupCollected(string pickupId);
        bool HasItem(string itemId);
        bool IsDoorUnlocked(string doorId);
        bool IsNoteRead(string noteId);
        bool IsGateCompleted(string gateId);

        event Action<MilestoneSource>? SourceChanged;
    }
}
