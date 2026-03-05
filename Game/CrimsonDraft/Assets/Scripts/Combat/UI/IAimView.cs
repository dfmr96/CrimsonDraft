#nullable enable
using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface IAimView
    {
        event Action<ResolvedShot[]>? OnShotsResolved;
        void ConfigureHitMask(AimHitMaskProfile? profile);
        void SetShotCount(int shotCount);
        void ShowShotFeedback(Vector2 normalizedPos, int damage, bool isMiss);
        void Show();
        void Confirm();
        void Hide();
    }
}
