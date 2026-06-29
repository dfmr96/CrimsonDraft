#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class InteractCrouchFlag : MonoBehaviour
    {
        [SerializeField] private bool requiresCrouch = true;

        public bool RequiresCrouch => this.requiresCrouch;
    }
}
