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
        void ClearFocus();
        RectTransform GetOperatorAnchor(int index);
        RectTransform GetOperatorRect(int index);
        RectTransform GetOperatorOverviewRect(int index);
        void MoveSelectorTo(RectTransform anchor);
        void SetOperatorAmmo(int index, int currentAmmo, int maxAmmo);
        void SetOperatorHealth(int index, float hpRatio);
        void SetDimmed(bool dimmed);
        void SetOperatorDimmed(int index, bool dimmed);
    }
}
