#nullable enable

using UnityEngine;
using UnityEngine.Events;
using Yarn.Unity;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class BarredDoorInteriorInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private DialogueReference unlockedDialogue = new();
        [SerializeField] private UnityEvent        onOpen           = new();

        internal bool IsUnbarred { get; private set; }

        public void Interact(InteractionContext context)
        {
            if (this.IsUnbarred)
            {
                this.onOpen.Invoke();
                return;
            }

            this.IsUnbarred = true;
            context.DialogueService.StartDialogue(
                this.unlockedDialogue.nodeName ?? "",
                onComplete: () => this.onOpen.Invoke());
        }
    }
}
