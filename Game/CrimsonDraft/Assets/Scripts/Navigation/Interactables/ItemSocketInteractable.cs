#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VContainer;
using Yarn.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class ItemSocketInteractable : MonoBehaviour, IInteractable
    {
        // Cross-scene/save identity for this socket -- must be unique per ItemSocketInteractable
        // instance in the project, same convention as PickupInteractable.pickupId.
        [SerializeField] private string           socketId          = "";
        [SerializeField] private SocketItemData[] requiredItems = System.Array.Empty<SocketItemData>();
        [SerializeField] private UnityEvent       onActivated   = new();
        [Tooltip("Fires once the socket is complete AND its insert dialogue has closed -- use this for follow-up sequences that should play after the player has read the message.")]
        [SerializeField] private UnityEvent       onActivatedAfterDialogue = new();
        [SerializeField] private DialogueReference dialogueReference = new();
        [SerializeField] private Collider?        blockingCollider; // optional physical barrier; disabled once the socket is fully activated
        [SerializeField] private GameObject?      revealOnActivate; // optional visual (e.g. the inserted item's mesh); hidden until the socket is fully activated
        [SerializeField] private GameObject?      hideOnActivate;   // optional visual (e.g. a steam/VFX blocker); switched off once the socket is fully activated

        private bool[] inserted = System.Array.Empty<bool>();
        private ItemSocketStateRegistry registry = null!;

        public bool IsActivated { get; private set; }

        // Lets other scripts (e.g. GeneratorSwitchPanel) subscribe in code instead of only via
        // the Inspector's persistent-call list.
        public UnityEvent OnActivated => this.onActivated;

        // Same, but delayed until the success dialogue has been dismissed.
        public UnityEvent OnActivatedAfterDialogue => this.onActivatedAfterDialogue;

        // Fires per slot as it gets filled: (slotIndex, animate). animate=false when the slot is
        // restored from the registry (room revisit/loaded save) -- listeners should snap to the
        // final state instead of replaying their transition. Subscribe in Awake: restoration
        // happens in Construct, which runs after it.
        public event Action<int, bool>? SlotFilled;

        public bool IsSlotFilled(int index)
        {
            var ins = EnsureInserted();
            return index >= 0 && index < ins.Length && ins[index];
        }

        void Awake()
        {
            // Defensive: make sure the "placed" visual isn't left visible by mistake in the
            // editor before anything has actually been inserted. Construct() (run afterwards,
            // once ItemSocketBootstrap has a registry to hand out) overrides this if the socket
            // was already filled/activated in a previous scene or a loaded save.
            if (this.revealOnActivate != null)
                this.revealOnActivate.SetActive(false);
        }

        // Called by ItemSocketBootstrap once per scene load, after Awake -- restores whatever
        // was inserted here the last time this room was visited (or from a loaded save),
        // without replaying the dialogue/onActivated side effects that ran the first time.
        [Inject]
        public void Construct(ItemSocketStateRegistry registry)
        {
            this.registry = registry;

            var saved = registry.GetInserted(this.socketId);
            if (saved.Length == this.requiredItems.Length)
                this.inserted = (bool[])saved.Clone();

            for (int i = 0; i < this.inserted.Length; i++)
                if (this.inserted[i]) SlotFilled?.Invoke(i, false);

            if (IsComplete())
                ApplyActivatedState();
        }

        public bool CanInsert(ItemData item)
        {
            if (this.IsActivated) return false;
            if (item is not SocketItemData) return false;
            var ins = EnsureInserted();
            for (int i = 0; i < this.requiredItems.Length; i++)
            {
                if (!ins[i] && this.requiredItems[i].ItemId == item.ItemId)
                    return true;
            }
            return false;
        }

        public bool TryInsert(ItemData item, IDialogueService? dialogueService)
        {
            if (this.IsActivated) return false;
            if (item is not SocketItemData) return false;

            var ins = EnsureInserted();
            for (int i = 0; i < this.requiredItems.Length; i++)
            {
                if (ins[i]) continue;
                if (this.requiredItems[i].ItemId != item.ItemId) continue;

                ins[i] = true;
                SlotFilled?.Invoke(i, true);
                this.registry.SetInserted(this.socketId, (bool[])ins.Clone());
                int filled   = CountFilled();
                bool complete = IsComplete();

                dialogueService?.StartDialogue(
                    this.dialogueReference.nodeName ?? "",
                    new Dictionary<string, object>
                    {
                        ["$insert_result"] = "success",
                        ["$item_name"]     = item.DisplayName,
                        ["$slots_filled"]  = filled,
                        ["$slots_total"]   = this.requiredItems.Length
                    },
                    onComplete: complete ? () => this.onActivatedAfterDialogue.Invoke() : null);

                if (complete)
                {
                    ApplyActivatedState();
                    this.onActivated.Invoke();

                    // No dialogue service means no dialogue to wait for.
                    if (dialogueService == null)
                        this.onActivatedAfterDialogue.Invoke();
                }

                return true;
            }

            dialogueService?.StartDialogue(
                this.dialogueReference.nodeName ?? "",
                new Dictionary<string, object>
                {
                    ["$insert_result"] = "wrong_item",
                    ["$item_name"]     = item.DisplayName
                });
            return false;
        }

        public void Interact(InteractionContext context)
        {
            int filled = CountFilled();
            int total  = this.requiredItems.Length;

            context.DialogueService.StartDialogue(
                this.dialogueReference.nodeName ?? "",
                new Dictionary<string, object>
                {
                    ["$activated"]    = this.IsActivated,
                    ["$slots_filled"] = filled,
                    ["$slots_total"]  = total
                });
        }

        private void ApplyActivatedState()
        {
            this.IsActivated = true;

            if (this.blockingCollider != null)
                this.blockingCollider.enabled = false;

            if (this.revealOnActivate != null)
                this.revealOnActivate.SetActive(true);

            if (this.hideOnActivate != null)
                this.hideOnActivate.SetActive(false);
        }

        private bool[] EnsureInserted()
        {
            if (this.inserted.Length != this.requiredItems.Length)
                this.inserted = new bool[this.requiredItems.Length];
            return this.inserted;
        }

        private int CountFilled()
        {
            var ins   = EnsureInserted();
            int count = 0;
            for (int i = 0; i < ins.Length; i++)
                if (ins[i]) count++;
            return count;
        }

        private bool IsComplete()
        {
            var ins = EnsureInserted();
            for (int i = 0; i < ins.Length; i++)
                if (!ins[i]) return false;
            return ins.Length > 0;
        }
    }
}
