#nullable enable
using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface ICommandPanelView
    {
        event Action<CombatCommand>? OnCommandSelected;
        event Action<RectTransform>? OnEntryFocused;
        RectTransform PanelRect { get; }
        void Show(RectTransform operatorAnchor);
        void Focus();
        void SetDimmed(bool dimmed);
        void Hide();
    }
}
