#nullable enable
using System;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Combat
{
    public interface ICombatActionMenuView
    {
        event Action<int>? OnOperatorSelected;
        event Action<int>? OnOperatorFocused;
        void FocusOperator(int index);
        void ClearFocus();
        void ReleaseOperatorFocus(int index);
        void PlayActionFeedback(int index);
        RectTransform GetOperatorAnchor(int index);
        RectTransform GetOperatorRect(int index);
        RectTransform GetOperatorOverviewRect(int index);
        void MoveSelectorTo(RectTransform anchor);
        void SetOperatorAmmo(int index, int currentAmmo, int maxAmmo);
        void SetOperatorHealth(int index, float hpRatio);
        void SetOperatorGauge(int index, float gauge01);
        void ExpandOperatorBorder(int index, bool expanded, Action? onComplete = null);
        void SetOperatorWeapon(int index, WeaponItem? weapon);
        void SetDimmed(bool dimmed);
        void SetOperatorDimmed(int index, bool dimmed);
        bool IsOperatorFocused(int index);
        void SetOperatorFocusFireMarked(int index, bool marked);
    }
}
