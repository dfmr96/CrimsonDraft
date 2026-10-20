#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Scenes;

namespace CrimsonDraft.Navigation.Radio
{
    public sealed class RadioConversationService : IRadioConversationService
    {
        // Offered and not yet heard, in the order they were offered; the latest one is the one
        // the player answers (walking into a new area supersedes an older unanswered call).
        private readonly List<string> offered = new();

        private readonly ISceneTransitionService sceneTransition;
        private readonly RadioCallRegistry       registry;   // heard calls: cross-scene and saved

        [Preserve]
        public RadioConversationService(ISceneTransitionService sceneTransition, RadioCallRegistry registry)
        {
            this.sceneTransition = sceneTransition;
            this.registry        = registry;
        }

        public string? PendingNode => this.offered.Count > 0 ? this.offered[^1] : null;

        public bool IsRinging => PendingNode != null && !this.sceneTransition.IsInCombat;

        public event Action? PendingChanged;
        public event Action<string>? ConversationHeard;
        public event Action? InventoryOpened;
        public event Action? InventoryClosed;

        public void Offer(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName) || this.registry.IsHeard(nodeName)) return;

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
            this.registry.SetHeard(nodeName);
            Withdraw(nodeName);
            ConversationHeard?.Invoke(nodeName);
        }

        public bool HasHeard(string nodeName) => this.registry.IsHeard(nodeName);

        public void NotifyInventoryOpened() => InventoryOpened?.Invoke();

        public void NotifyInventoryClosed() => InventoryClosed?.Invoke();
    }
}
