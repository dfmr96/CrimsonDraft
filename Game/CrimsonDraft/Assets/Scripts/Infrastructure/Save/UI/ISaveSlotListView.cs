#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Infrastructure.Save.UI
{
    /// <summary>
    /// View contract for <see cref="SaveSlotNavigator"/> -- lets the same cursor-driven
    /// navigate/confirm/back state machine drive either the flat screen-space overlay
    /// (<see cref="SaveSlotListView"/>) or the world-space main menu panel
    /// (<see cref="LoadGameSaveListView"/>).
    /// </summary>
    public interface ISaveSlotListView
    {
        void Show(IReadOnlyList<SaveSlotSummary> slots, int cursorIndex);
        void ShowConfirm(SaveSlotSummary summary, string confirmVerb);
        void Hide();
    }
}
