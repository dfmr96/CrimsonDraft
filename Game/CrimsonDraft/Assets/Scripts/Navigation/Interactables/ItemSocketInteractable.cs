#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Yarn.Unity;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class ItemSocketInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private SocketItemData[] requiredItems = System.Array.Empty<SocketItemData>();
        [SerializeField] private UnityEvent       onActivated   = new();
        [SerializeField] private DialogueReference dialogueReference = new();
        [SerializeField] private Collider?        blockingCollider; // optional physical barrier; disabled once the socket is fully activated
        [SerializeField] private GameObject?      revealOnActivate; // optional visual (e.g. the inserted item's mesh); hidden until the socket is fully activated
        [SerializeField] private GameObject?      hideOnActivate;   // optional visual (e.g. a steam/VFX blocker); switched off once the socket is fully activated

        private bool[] inserted = System.Array.Empty<bool>();

        public bool IsActivated { get; private set; }

        void Awake()
        {
            // Defensive: make sure the "placed" visual isn't left visible by mistake in the
            // editor before anything has actually been inserted.
            if (this.revealOnActivate != null)
                this.revealOnActivate.SetActive(false);
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
                int filled = CountFilled();

                dialogueService?.StartDialogue(
                    this.dialogueReference.nodeName ?? "",
                    new Dictionary<string, object>
                    {
                        ["$insert_result"] = "success",
                        ["$item_name"]     = item.DisplayName,
                        ["$slots_filled"]  = filled,
                        ["$slots_total"]   = this.requiredItems.Length
                    });

                if (IsComplete())
                {
                    this.IsActivated = true;

                    if (this.blockingCollider != null)
                        this.blockingCollider.enabled = false;

                    if (this.revealOnActivate != null)
                        this.revealOnActivate.SetActive(true);

                    if (this.hideOnActivate != null)
                        this.hideOnActivate.SetActive(false);

                    this.onActivated.Invoke();
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
