#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PuzzleInteractable : MonoBehaviour, IInteractable, IPuzzle
    {
        [SerializeField] private string                  puzzleId = "";
        [SerializeField] private GameObject              canvasPrefab = null!;
        [SerializeField] private ItemSocketInteractable? requiredSocket;
        [SerializeField] private bool                    triggersDemoEnd;
        [SerializeField, TextArea] private string         demoEndMessage = "This is the end of the demo.\nThanks for playing.";

        private bool isSolved;
        private PuzzleStateRegistry? puzzleStates;

        public string PuzzleId => this.puzzleId;
        public void BindPuzzleState(PuzzleStateRegistry registry) => this.puzzleStates = registry;

        public void MarkSolved()
        {
            this.isSolved = true;
            this.puzzleStates?.SetSolved(this.puzzleId);
        }

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
                MarkSolved();
                if (this.triggersDemoEnd)
                    context.ScreenFader.ShowEndScreenAsync(this.demoEndMessage).Forget();
            });
        }
    }
}
