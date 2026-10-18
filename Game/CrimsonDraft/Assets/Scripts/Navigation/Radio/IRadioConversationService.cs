#nullable enable

using System;

namespace CrimsonDraft.Navigation.Radio
{
    /// <summary>
    /// Which radio conversation is waiting for the player. Zones <see cref="Offer"/> a node when
    /// the player walks in; the HUD icon shows while one is pending; opening the inventory lets
    /// the player take the call. Heard conversations are not offered again.
    /// </summary>
    public interface IRadioConversationService
    {
        /// <summary>Yarn node of the call currently waiting, or null.</summary>
        string? PendingNode { get; }

        event Action? PendingChanged;
        event Action? InventoryOpened;
        event Action? InventoryClosed;

        void Offer(string nodeName);
        void Withdraw(string nodeName);
        void MarkHeard(string nodeName);
        bool HasHeard(string nodeName);

        // Called by the inventory controller (UI assembly), which can't be referenced from here.
        void NotifyInventoryOpened();
        void NotifyInventoryClosed();
    }
}
