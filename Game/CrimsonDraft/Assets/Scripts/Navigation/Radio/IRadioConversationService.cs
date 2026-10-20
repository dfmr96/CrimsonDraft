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

        /// <summary>
        /// A call is waiting AND the radio is allowed to ring: it goes quiet during combat and
        /// picks up again afterwards (the pending call is kept). While this is true the player
        /// can't interact with the world -- they have to answer the communicator first.
        /// </summary>
        bool IsRinging { get; }

        event Action? PendingChanged;

        /// <summary>A call finished and was recorded as heard (and so will be saved).</summary>
        event Action<string>? ConversationHeard;
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
