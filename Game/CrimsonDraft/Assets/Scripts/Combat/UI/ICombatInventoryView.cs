#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface ICombatInventoryView
    {
        event Action<int>? OnItemUsed;
        event Action?      OnCancelled;
        void Show(int operatorSlot, RectTransform operatorOverviewRect);
        void Hide();
    }
}
