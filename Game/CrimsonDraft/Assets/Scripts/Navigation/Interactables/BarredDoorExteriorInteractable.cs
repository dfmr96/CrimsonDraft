#nullable enable

using UnityEngine;
using UnityEngine.Events;
using Yarn.Unity;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class BarredDoorExteriorInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private BarredDoorInteriorInteractable interior      = null!;
        [SerializeField] private DialogueReference              lockedDialogue = new();
        [SerializeField] private UnityEvent                     onOpen         = new();

        public void Interact(InteractionContext context)
        {
            if (this.interior.IsUnbarred)
                this.onOpen.Invoke();
            else
                context.DialogueService.StartDialogue(this.lockedDialogue.nodeName ?? "");
        }
    }
}
