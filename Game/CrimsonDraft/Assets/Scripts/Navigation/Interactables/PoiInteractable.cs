#nullable enable

using UnityEngine;
using VContainer;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PoiInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private PoiData data = null!;

        private PoiController controller = null!;

        [Inject]
        public void Construct(PoiController controller)
        {
            this.controller = controller;
        }

        public void Interact(InteractionContext context)
        {
            this.controller.Open(this.data.Lines);
        }
    }
}
