#nullable enable

using System;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Inventory;
using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    // Lives here rather than CrimsonDraft.Inventory (alongside HotspotReward/ItemHotspotReward)
    // because it needs DocumentData, and Inventory can't reference Navigation without a
    // circular assembly dependency (Navigation already references Inventory). Discovered by
    // ItemExamineHotspots.Hotspot.reward purely through [SerializeReference] polymorphism --
    // Inventory never needs to know this type exists.
    [Serializable]
    public sealed class NoteHotspotReward : HotspotReward
    {
        [SerializeField] private DocumentData note = null!;

        public override string Grant(HotspotRewardContext context)
        {
            if (context.ConsumedItem != null)
                context.InventoryService.TryRemoveItem(context.ConsumedItem.ItemId);
            context.NoteRegistry.SetCollected(this.note.NoteId);
            return this.note.Title;
        }

        // Closes InspectPanel first, then publishes -- FilesTabController.OnNoteCollected
        // opens the inventory/Files tab and selects the note itself, which would otherwise
        // render underneath InspectPanel if it were still open.
        public override void OnAcknowledged(HotspotRewardContext context)
        {
            context.ClosePanel(null);
            context.NotePublisher.Publish(new NoteCollectedEvent { NoteId = this.note.NoteId });
        }
    }
}
