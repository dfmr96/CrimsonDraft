#nullable enable
using System;
using CrimsonDraft.Inventory;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface IAimView
    {
        event Action<ResolvedShot[]>? OnShotsResolved;
        void ConfigureHitMask(AimHitMaskProfile? profile);
        void ConfigureWeapon(WeaponData? weaponData);
        void ConfigureMeleeWeapon(MeleeWeaponData? meleeData);
        void SetShotCount(int shotCount);
        void SetOperatorHpRatio(float hpRatio);
        void ShowShotFeedback(Vector2 normalizedPos, int damage, bool isMiss);
        void Show();
        void Confirm();
        void Hide();
        ResolvedShot[] ResolveShotsForWeapon(WeaponData? weaponData, int shotCount);
    }
}
