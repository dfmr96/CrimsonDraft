#nullable enable
using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface IAimView
    {
        event Action<Vector2>? OnShotFired;
        void Show();
        void Confirm();
        void Hide();
    }
}
