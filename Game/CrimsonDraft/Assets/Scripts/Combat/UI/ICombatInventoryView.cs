#nullable enable

using System;
using System.Collections.Generic;
using CrimsonDraft.Inventory;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface ICombatInventoryView
    {
        // item (null for a reload), then the operator slot the item is applied to.
        event Action<InventoryItem?, int>? OnItemUsed;
        event Action?      OnCancelled;
        // Shows every operator's inventory under its own card (operatorCardRects[i] is operator i's card);
        // only operatorSlot's one is interactive, the rest are dimmed.
        void Show(int operatorSlot, IReadOnlyList<RectTransform> operatorCardRects);
        void Hide();
        // Same as Hide, but plays the closing animation first (the opening one in reverse) and calls onComplete after.
        void Close(Action onComplete);
    }
}
