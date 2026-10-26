#nullable enable

namespace CrimsonDraft.Navigation.Milestones
{
    public static class MilestoneTriggerEvaluator
    {
        public static bool Holds(MilestoneTrigger trigger, IMilestoneWorldState world)
        {
            switch (trigger.kind)
            {
                case MilestoneTriggerKind.PickupCollected: return Has(trigger.targetId) && world.IsPickupCollected(trigger.targetId);
                case MilestoneTriggerKind.DoorUnlocked:    return Has(trigger.targetId) && world.IsDoorUnlocked(trigger.targetId);
                case MilestoneTriggerKind.GateCompleted:   return Has(trigger.targetId) && world.IsGateCompleted(trigger.targetId);
                case MilestoneTriggerKind.ItemHeld:        return trigger.item != null && Has(trigger.item.ItemId) && world.HasItem(trigger.item.ItemId);
                case MilestoneTriggerKind.NoteRead:        return trigger.note != null && Has(trigger.note.NoteId) && world.IsNoteRead(trigger.note.NoteId);
                default:                                   return false;
            }
        }

        public static MilestoneSource SourceOf(MilestoneTriggerKind kind) => kind switch
        {
            MilestoneTriggerKind.PickupCollected => MilestoneSource.Pickups,
            MilestoneTriggerKind.ItemHeld        => MilestoneSource.Inventory,
            MilestoneTriggerKind.DoorUnlocked    => MilestoneSource.Doors,
            MilestoneTriggerKind.NoteRead        => MilestoneSource.Notes,
            MilestoneTriggerKind.GateCompleted   => MilestoneSource.Gates,
            _                                    => MilestoneSource.None,
        };

        private static bool Has(string? id) => !string.IsNullOrEmpty(id);
    }
}
