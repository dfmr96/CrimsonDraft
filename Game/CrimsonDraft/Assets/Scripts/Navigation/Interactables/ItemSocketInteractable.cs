#nullable enable

using UnityEngine;
using UnityEngine.Events;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class ItemSocketInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private SocketItemData[] requiredItems = System.Array.Empty<SocketItemData>();
        [SerializeField] private UnityEvent       onActivated   = new();

        private bool[] inserted = System.Array.Empty<bool>();

        public bool IsActivated { get; private set; }

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

        public bool TryInsert(ItemData item, PoiController? poi)
        {
            if (this.IsActivated) return false;
            if (item is not SocketItemData) return false;

            var ins = EnsureInserted();
            for (int i = 0; i < this.requiredItems.Length; i++)
            {
                if (ins[i]) continue;
                if (this.requiredItems[i].ItemId != item.ItemId) continue;

                ins[i] = true;
                poi?.Open(new[] { $"Inserted: {item.DisplayName}." });

                if (IsComplete())
                {
                    this.IsActivated = true;
                    this.onActivated.Invoke();
                }

                return true;
            }

            poi?.Open(new[] { $"Can't use {item.DisplayName} here." });
            return false;
        }

        public void Interact(InteractionContext context)
        {
            if (this.IsActivated)
            {
                context.PoiController.Open(new[] { "Already activated." });
                return;
            }

            var ins   = EnsureInserted();
            var lines = new string[this.requiredItems.Length];
            for (int i = 0; i < this.requiredItems.Length; i++)
                lines[i] = $"{(ins[i] ? "[✓]" : "[ ]")} {this.requiredItems[i].DisplayName}";
            context.PoiController.Open(lines);
        }

        private bool[] EnsureInserted()
        {
            if (this.inserted.Length != this.requiredItems.Length)
                this.inserted = new bool[this.requiredItems.Length];
            return this.inserted;
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
