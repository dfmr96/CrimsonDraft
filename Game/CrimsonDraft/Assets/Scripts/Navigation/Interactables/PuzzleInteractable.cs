#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PuzzleInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameObject              canvasPrefab = null!;
        [SerializeField] private ItemSocketInteractable? requiredSocket;
        [SerializeField] private bool                    triggersDemoEnd;
        [SerializeField, TextArea] private string         demoEndMessage = "Hasta aquí llega la demo.\nGracias por jugar.";

        private bool isSolved;

        public void Interact(InteractionContext context)
        {
            if (this.isSolved) return;

            if (this.requiredSocket != null && !this.requiredSocket.IsActivated)
            {
                this.requiredSocket.Interact(context);
                return;
            }

            context.PuzzleViewController.Open(this.canvasPrefab, onSolved: () =>
            {
                this.isSolved = true;
                if (this.triggersDemoEnd)
                    context.ScreenFader.ShowEndScreenAsync(this.demoEndMessage).Forget();
            });
        }
    }
}
