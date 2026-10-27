#nullable enable
using System;
using System.Collections.Generic;
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
        IReadOnlyList<RectTransform> GetOperatorOverviewRects();
        // Grows the operator strip taller (upward) to make room for the combat inventory underneath its
        // cards, or shrinks it back; onComplete fires once it reached its final height.
        void SetInventoryLift(bool lifted, Action? onComplete = null);
        // Re-raises the focus bounce ReleaseOperatorFocus dropped (used when backing out of the inventory).
        void RestoreOperatorFocus(int index);
        void MoveSelectorTo(RectTransform anchor);
        void SetOperatorAmmo(int index, int currentAmmo, int maxAmmo);
        void SetOperatorHealth(int index, float hpRatio, bool isAlive);
        void SetOperatorName(int index, string name);
        // Squad logo on the card; null keeps whatever the prefab already shows (same rule as the inventory card).
        void SetOperatorLogo(int index, Sprite? logo);
        void PlayOperatorDamageShake(int index);
        void PlayOperatorDamageGlitch(int index);
        void PlayOperatorHealFlash(int index);
        void SetOperatorActionPending(int index, bool pending);
        void SetOperatorTurnOrder(int index, int position);
        void SetOperatorGauge(int index, float gauge01);
        void SetOperatorWeapon(int index, WeaponItem? weapon);
        void SetDimmed(bool dimmed);
        void SetOperatorDimmed(int index, bool dimmed);
        // Slot with no operator: icon and ECG trace hidden, "MIA" as status, card very dim and not selectable.
        void SetOperatorMissing(int index, bool missing);
        // Number of operator cards on screen (can exceed the roster size).
        int OperatorSlotCount { get; }
        bool IsOperatorFocused(int index);
    }
}
