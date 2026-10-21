#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class StorageBoxInteractable : MonoBehaviour, IInteractable
    {
        public void Interact(InteractionContext context) =>
            context.StorageOpenPublisher.Publish(new StorageOpenRequestedEvent());
    }
}
