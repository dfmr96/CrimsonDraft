#nullable enable

using System;
using MessagePipe;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Inventory
{
    // Everything a HotspotReward needs to grant itself and react once the player acknowledges
    // it -- assembled by InspectPanel (the only place with access to all of these) and handed
    // down rather than injected directly into the reward, since HotspotReward instances are
    // plain [SerializeReference] data (authored in the Inspector), not VContainer-managed.
    public readonly struct HotspotRewardContext
    {
        public readonly IInventoryService                InventoryService;
        public readonly NoteRegistry                      NoteRegistry;
        public readonly IPublisher<NoteCollectedEvent>    NotePublisher;
        public readonly ItemData?                          ConsumedItem;
        public readonly Action<string?>                    ClosePanel;

        public HotspotRewardContext(
            IInventoryService inventoryService, NoteRegistry noteRegistry,
            IPublisher<NoteCollectedEvent> notePublisher, ItemData? consumedItem, Action<string?> closePanel)
        {
            this.InventoryService = inventoryService;
            this.NoteRegistry     = noteRegistry;
            this.NotePublisher    = notePublisher;
            this.ConsumedItem     = consumedItem;
            this.ClosePanel       = closePanel;
        }
    }

    // A hotspot's reward is polymorphic ([SerializeReference]) so new reward kinds (item, note,
    // ...) can be added without ItemExamineHotspots/InspectPanel knowing about each one --
    // see ItemHotspotReward here and NoteHotspotReward in CrimsonDraft.Navigation (it needs
    // DocumentData, which Inventory can't reference without a circular assembly dependency).
    [Serializable]
    public abstract class HotspotReward
    {
        [SerializeField] private DialogueReference announcementDialogue;

        // The node InspectPanel types out after Grant() -- its $rewarded_name variable is
        // filled with Grant()'s return value (see InspectPanel.GrantReward).
        public DialogueReference AnnouncementDialogue => this.announcementDialogue;

        // Mutates game state (grant an item, register a note, ...) and returns the display
        // name to substitute into AnnouncementDialogue's $rewarded_name.
        public abstract string Grant(HotspotRewardContext context);

        // Called once the player confirms away the announcement text. Responsible for closing
        // InspectPanel (via context.ClosePanel) and any reward-specific follow-up, e.g. select
        // the granted item in the grid, or hand off to the Files tab for a note.
        public abstract void OnAcknowledged(HotspotRewardContext context);
    }

    [Serializable]
    public sealed class ItemHotspotReward : HotspotReward
    {
        [SerializeField] private ItemData rewardItem = null!;

        public override string Grant(HotspotRewardContext context)
        {
            if (context.ConsumedItem != null)
                context.InventoryService.TryRemoveItem(context.ConsumedItem.ItemId);
            context.InventoryService.AddItemAuto(this.rewardItem);

            // Freshly granted/unidentified, so the announcement uses SecondaryName -- same
            // "not yet identified" naming GridCursor's tooltip shows for any item the player
            // hasn't inspected yet -- falling back to DisplayName if unset.
            return !string.IsNullOrEmpty(this.rewardItem.SecondaryName)
                ? this.rewardItem.SecondaryName
                : this.rewardItem.DisplayName;
        }

        // Carries the granted item's id so GridCursor selects it once the panel closes.
        public override void OnAcknowledged(HotspotRewardContext context) =>
            context.ClosePanel(this.rewardItem.ItemId);
    }
}
