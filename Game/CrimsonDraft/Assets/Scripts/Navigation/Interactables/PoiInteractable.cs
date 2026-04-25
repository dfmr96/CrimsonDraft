#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PoiInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private PoiData data = null!;

        public void Interact(InteractionContext context)
        {
            context.DialogueService.StartDialogue(this.data.YarnNodeName);
        }
    }
}
