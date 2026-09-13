#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class SavePointInteractable : MonoBehaviour, IInteractable
    {
        public void Interact(InteractionContext context)
        {
            context.SaveController.Open();
        }
    }
}
