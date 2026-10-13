#nullable enable

using System;
using CrimsonDraft.Inventory;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface ICombatInventoryView
    {
        event Action<InventoryItem?>? OnItemUsed;
        event Action?      OnCancelled;
        void Show(int operatorSlot, RectTransform operatorOverviewRect);
        void Hide();
    }
}
