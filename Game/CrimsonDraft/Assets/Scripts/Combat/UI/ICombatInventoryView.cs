#nullable enable

using System;

namespace CrimsonDraft.Combat
{
    public interface ICombatInventoryView
    {
        event Action<int>? OnItemUsed;
        event Action?      OnCancelled;
        void Show(int operatorSlot);
        void Hide();
    }
}
