#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.CamaraSystem
{
    public interface ICameraRelativeMovementService
    {
        Vector3 Forward { get; }
        Vector3 Right { get; }

        void Tick(Vector2 heldDirection);
    }
}
