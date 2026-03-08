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
        void SetShotCount(int shotCount);
        void ShowShotFeedback(Vector2 normalizedPos, int damage, bool isMiss);
        void Show();
        void Confirm();
        void Hide();
    }
}
