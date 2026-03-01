#nullable enable
using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface ICombatActionMenuView
    {
        event Action<int>? OnOperatorSelected;
        event Action<int>? OnOperatorFocused;
        void FocusOperator(int index);
        RectTransform GetOperatorAnchor(int index);
        RectTransform GetOperatorRect(int index);
        void MoveSelectorTo(RectTransform anchor);
        void SetDimmed(bool dimmed);
    }
}
