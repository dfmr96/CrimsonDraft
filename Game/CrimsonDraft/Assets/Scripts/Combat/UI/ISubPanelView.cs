#nullable enable
using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface ISubPanelView
    {
        event Action<int>? OnItemSelected;
        event Action<RectTransform>? OnEntryFocused;
        void Show(SubPanelItem[] items, RectTransform commandPanelRect);
        void Hide();
    }
}
