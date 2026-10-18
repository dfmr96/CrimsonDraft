#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Navigation.Radio
{
    public sealed class RadioConversationService : IRadioConversationService
    {
        // Offered and not yet heard, in the order they were offered; the latest one is the one
        // the player answers (walking into a new area supersedes an older unanswered call).
        private readonly List<string>    offered = new();
        private readonly HashSet<string> heard   = new();

        [Preserve]
        public RadioConversationService() { }

        public string? PendingNode => this.offered.Count > 0 ? this.offered[^1] : null;

        public event Action? PendingChanged;
        public event Action? InventoryOpened;
        public event Action? InventoryClosed;

        public void Offer(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName) || this.heard.Contains(nodeName)) return;

            this.offered.Remove(nodeName);
            this.offered.Add(nodeName);
            PendingChanged?.Invoke();
        }

        public void Withdraw(string nodeName)
        {
            if (this.offered.Remove(nodeName)) PendingChanged?.Invoke();
        }

        public void MarkHeard(string nodeName)
        {
            this.heard.Add(nodeName);
            Withdraw(nodeName);
        }

        public bool HasHeard(string nodeName) => this.heard.Contains(nodeName);

        public void NotifyInventoryOpened() => InventoryOpened?.Invoke();

        public void NotifyInventoryClosed() => InventoryClosed?.Invoke();
    }
}
