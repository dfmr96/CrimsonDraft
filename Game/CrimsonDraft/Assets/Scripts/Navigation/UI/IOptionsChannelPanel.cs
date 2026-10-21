#nullable enable

namespace CrimsonDraft.Navigation.UI
{
    /// <summary>
    /// A tab's content list, as driven by <see cref="OptionsTabController"/>: a fixed number of
    /// vertically-navigable channels, each independently highlightable and adjustable via
    /// Left/Right. A channel may ignore <see cref="Adjust"/> entirely (e.g. a locked toggle) --
    /// the controller doesn't need to know which.
    /// </summary>
    public interface IOptionsChannelPanel
    {
        int ChannelCount { get; }

        /// <summary>Disabled channels are skipped by vertical navigation.</summary>
        bool IsSelectable(int index);

        void ShowOutline(int index);
        void HideOutlines();

        /// <param name="direction">-1 or +1; the panel decides its own step size and clamping.</param>
        void Adjust(int index, int direction);

        /// <summary>Whether holding Left/Right on this channel should keep calling <see cref="Adjust"/>.</summary>
        bool RepeatsOnHold(int index);

        /// <summary>Confirm pressed while this channel is selected.</summary>
        void Confirm(int index);
    }
}
