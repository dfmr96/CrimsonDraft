#nullable enable
using System;
using CrimsonDraft.Inventory;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface IAimView
    {
        event Action<ResolvedShot[]>? OnShotsResolved;
        // Melee count: the first missing point of the cut has just been revealed -- the moment the
        // counter-hit lands. Default no-op accessors keep existing implementers (test fakes) compiling.
        event Action? OnMeleeMissRevealed { add { } remove { } }
        void ConfigureHitMask(AimHitMaskProfile? profile);
        void ConfigureOverlay(ActiveOverlay? overlay);
        void ConfigureWeapon(WeaponData? weaponData);
        void ConfigureMeleeWeapon(MeleeWeaponData? meleeData);
        void SetShotCount(int shotCount);
        void SetOperatorHpRatio(float hpRatio);
        // Target's current HP, so the damage counter can tell whether the final total is lethal.
        // Default no-op keeps existing implementers (e.g. test fakes) compiling.
        void SetTargetHp(int currentHp) { }
        // Input after the shots are on screen: true when the view used it (it starts the damage
        // count) and the panel must stay open; false when the caller should close the panel.
        bool TryAdvancePostShots() => false;
        void ShowShotFeedback(Vector2 normalizedPos, int damage, bool isMiss);
        void Show();
        void Confirm();
        void Hide();
    }
}
