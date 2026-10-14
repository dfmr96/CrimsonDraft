#nullable enable

using System;
using CrimsonDraft.Infrastructure.Save.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    public interface ISaveController
    {
        // Opens the save-slot menu; onSaved runs once a slot has actually been written (not when
        // the menu is closed without saving) -- e.g. to spend the Ticker Tape the save cost.
        void Open(Action? onSaved = null);

        // Save-room variant: drives the world-space panel instead of the screen overlay and
        // leaves the camera/input to the caller. onClosed runs once the panel is dismissed,
        // saved or not -- after the typewriter reveal and a final input, or a cancel.
        void OpenTelegraph(LoadGameSaveListView view, Action? onSaved, Action? onClosed);
    }
}
