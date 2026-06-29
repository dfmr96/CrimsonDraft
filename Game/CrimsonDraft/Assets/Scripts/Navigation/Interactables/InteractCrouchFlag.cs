#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>
    /// Attach alongside an IInteractable to override the Interact animation it plays.
    /// </summary>
    public sealed class InteractCrouchFlag : MonoBehaviour, IAnimatedInteractable
    {
        [SerializeField] private InteractionAnimType animType = InteractionAnimType.Crouch;

        public InteractionAnimType AnimType => this.animType;
    }
}
