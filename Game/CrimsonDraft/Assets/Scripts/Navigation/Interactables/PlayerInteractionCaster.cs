#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PlayerInteractionCaster : MonoBehaviour, IInteractionCaster
    {
        [SerializeField] private float     rayDistance = 2f;
        [SerializeField] private LayerMask interactableLayer;

        private IInputService       inputService        = null!;
        private IInventoryService   inventoryService    = null!;
        private PoiController       poiController       = null!;
        private DocumentController  documentController  = null!;
        private ContainerController containerController = null!;

        [Inject]
        public void Construct(
            IInputService       inputService,
            IInventoryService   inventoryService,
            PoiController       poiController,
            DocumentController  documentController,
            ContainerController containerController)
        {
            this.inputService        = inputService;
            this.inventoryService    = inventoryService;
            this.poiController       = poiController;
            this.documentController  = documentController;
            this.containerController = containerController;
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

            var context = new InteractionContext(
                this.inventoryService,
                this.inputService,
                this.poiController,
                this.documentController,
                this.containerController);
            interactable.Interact(context);
        }

        public bool CanUseItem(ItemData item)
        {
            if (!Physics.Raycast(transform.position, transform.forward, out var hit, this.rayDistance, this.interactableLayer))
                return false;
            if (!hit.collider.TryGetComponent<ItemSocketInteractable>(out var socket))
                return false;
            return socket.CanInsert(item);
        }

        public bool TryUseItem(ItemData item)
        {
            if (!Physics.Raycast(transform.position, transform.forward, out var hit, this.rayDistance, this.interactableLayer))
                return false;

            if (!hit.collider.TryGetComponent<ItemSocketInteractable>(out var socket))
                return false;

            return socket.TryInsert(item, this.poiController);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var origin = transform.position;
            var tip    = origin + transform.forward * this.rayDistance;

            bool hit = Physics.Raycast(origin, transform.forward, out var hitInfo, this.rayDistance, this.interactableLayer);

            Gizmos.color = hit ? Color.green : Color.cyan;
            Gizmos.DrawRay(origin, transform.forward * this.rayDistance);
            Gizmos.DrawWireSphere(tip, 0.08f);

            if (hit)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(hitInfo.point, 0.12f);

                UnityEditor.Handles.color = Color.green;
                UnityEditor.Handles.Label(hitInfo.point + Vector3.up * 0.3f, hitInfo.collider.name);
            }
        }
#endif
    }
}
