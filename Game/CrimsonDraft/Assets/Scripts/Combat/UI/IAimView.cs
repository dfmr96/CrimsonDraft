#nullable enable
using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface IAimView
    {
        event Action<Vector2, ShotZone>? OnShotFired;
        void ConfigureHitMask(AimHitMaskProfile? profile);
        void Show();
        void Confirm();
        void Hide();
    }
}
