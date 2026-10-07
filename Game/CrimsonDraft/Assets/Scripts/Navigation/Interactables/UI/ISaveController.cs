#nullable enable

using System;

namespace CrimsonDraft.Navigation.Interactables
{
    public interface ISaveController
    {
        // Opens the save-slot menu; onSaved runs once a slot has actually been written (not when
        // the menu is closed without saving) -- e.g. to spend the Ticker Tape the save cost.
        void Open(Action? onSaved = null);
    }
}
