#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PlayerInteractionCaster : MonoBehaviour, IInteractionCaster
    {
        private const int StandInteractType  = 0;
        private const int CrouchInteractType = 2;

        private static readonly int IntTypeHash     = Animator.StringToHash("IntType");
        private static readonly int InteractingHash = Animator.StringToHash("Interacting");

        [SerializeField] private float     rayDistance = 2f;
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField] private Animator  animator = null!;
        [SerializeField] private float     interactingDuration = 1f;

        private Coroutine? interactingRoutine;

        private IInputService          inputService          = null!;
        private IInventoryService      inventoryService      = null!;
        private IDialogueService       dialogueService       = null!;
        private IPickupDialogueService pickupDialogueService = null!;
        private DocumentController     documentController    = null!;
        private ContainerController    containerController   = null!;
        private PuzzleViewController    puzzleViewController   = null!;

        [Inject]
        public void Construct(
            IInputService          inputService,
            IInventoryService      inventoryService,
            IDialogueService       dialogueService,
            IPickupDialogueService pickupDialogueService,
            DocumentController     documentController,
            ContainerController    containerController,
            PuzzleViewController    puzzleViewController)
        {
            this.inputService          = inputService;
            this.inventoryService      = inventoryService;
            this.dialogueService       = dialogueService;
            this.pickupDialogueService = pickupDialogueService;
            this.documentController    = documentController;
            this.containerController   = containerController;
            this.puzzleViewController   = puzzleViewController;
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

            var requiresCrouch = hit.collider.TryGetComponent<InteractCrouchFlag>(out var crouchFlag)
                && crouchFlag.RequiresCrouch;
            this.animator.SetInteger(IntTypeHash, requiresCrouch ? CrouchInteractType : StandInteractType);

            if (this.interactingRoutine != null)
                StopCoroutine(this.interactingRoutine);
            this.animator.SetBool(InteractingHash, true);
            this.interactingRoutine = StartCoroutine(ClearInteractingAfterDelay());

            var context = new InteractionContext(
                this.inventoryService,
                this.inputService,
                this.dialogueService,
                this.documentController,
                this.containerController,
                this.pickupDialogueService,
                this.puzzleViewController);
            interactable.Interact(context);
        }

        private System.Collections.IEnumerator ClearInteractingAfterDelay()
        {
            yield return new WaitForSeconds(this.interactingDuration);
            this.animator.SetBool(InteractingHash, false);
            this.interactingRoutine = null;
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

            return socket.TryInsert(item, this.dialogueService);
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
