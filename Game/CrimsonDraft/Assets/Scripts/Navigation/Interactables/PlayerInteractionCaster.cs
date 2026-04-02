#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PlayerInteractionCaster : MonoBehaviour
    {
        [SerializeField] private float     rayDistance = 2f;
        [SerializeField] private LayerMask interactableLayer;

        private IInputService     inputService     = null!;
        private IInventoryService inventoryService = null!;

        [Inject]
        public void Construct(IInputService inputService, IInventoryService inventoryService)
        {
            this.inputService     = inputService;
            this.inventoryService = inventoryService;
            this.inputService.Interact.performed += OnInteract;
        }

        private void OnDestroy()
        {
            if (this.inputService != null)
                this.inputService.Interact.performed -= OnInteract;
        }

        private void OnInteract(InputAction.CallbackContext _)
        {
            if (!Physics.Raycast(transform.position, transform.forward, out var hit, this.rayDistance, this.interactableLayer))
                return;

            if (!hit.collider.TryGetComponent<IInteractable>(out var interactable))
                return;

            var context = new InteractionContext(this.inventoryService, this.inputService);
            interactable.Interact(context);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.forward * this.rayDistance);
        }
#endif
    }
}
