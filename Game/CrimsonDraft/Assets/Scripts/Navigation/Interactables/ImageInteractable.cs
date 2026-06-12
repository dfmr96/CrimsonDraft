#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class ImageInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameObject canvasPrefab = null!;

        public void Interact(InteractionContext context)
        {
            context.ImageViewController.Open(this.canvasPrefab);
        }
    }
}
