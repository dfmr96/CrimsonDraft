#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.Map
{
    /// <summary>A deck-plan pickup: on collection it registers the deck as known so its
    /// unvisited rooms start drawing on the map (GDD: fog of war via the map item).</summary>
    public sealed class MapPickupInteractable : MonoBehaviour, IInteractable
    {
        private const string PromptNode = "pickup_prompt";

        [SerializeField] private string   pickupId = null!;
        [SerializeField] private ItemData item     = null!;
        [SerializeField] private MapData  map      = null!;

        private PickupRegistry    pickupRegistry = null!;
        private KnownMapsRegistry knownMaps      = null!;

        public string PickupId => this.pickupId;

        [Inject]
        public void Construct(PickupRegistry registry, KnownMapsRegistry knownMaps)
        {
            this.pickupRegistry = registry;
            this.knownMaps      = knownMaps;
            if (registry.IsCollected(this.pickupId))
                gameObject.SetActive(false);
        }

        public void Interact(InteractionContext context)
        {
            bool pickupSucceeded = false;
            string itemName = !string.IsNullOrEmpty(this.item.SecondaryName)
                ? this.item.SecondaryName
                : this.item.DisplayName;

            context.PickupDialogueService.StartDialogue(
                PromptNode,
                variables: new Dictionary<string, object>
                {
                    ["$item_name"]      = itemName,
                    ["$pickup_success"] = true,
                },
                onComplete: () =>
                {
                    if (!pickupSucceeded) return;
                    this.pickupRegistry.SetCollected(this.pickupId);
                    this.knownMaps.MarkKnown(this.map.SceneName);
                    gameObject.SetActive(false);
                },
                commands: new Dictionary<string, Action>
                {
                    ["try_pickup"] = () =>
                    {
                        pickupSucceeded = context.InventoryService.AddItemAuto(this.item);
                        context.PickupDialogueService.SetVariable("$pickup_success", pickupSucceeded);
                    }
                });
        }
    }
}
