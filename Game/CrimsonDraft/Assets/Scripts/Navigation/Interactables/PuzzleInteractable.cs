#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PuzzleInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameObject              canvasPrefab = null!;
        [SerializeField] private ItemSocketInteractable? requiredSocket;

        private bool isSolved;

        public void Interact(InteractionContext context)
        {
            if (this.isSolved) return;

            if (this.requiredSocket != null && !this.requiredSocket.IsActivated)
            {
                this.requiredSocket.Interact(context);
                return;
            }

            context.PuzzleViewController.Open(this.canvasPrefab, onSolved: () => this.isSolved = true);
        }
    }
}
