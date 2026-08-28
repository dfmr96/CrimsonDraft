#nullable enable

using System;
using MessagePipe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PlayerInteractionCaster : MonoBehaviour, IInteractionCaster
    {
        
        private static readonly int InteractStandHash     = Animator.StringToHash("InteractStandEnter");
        private static readonly int InteractCrouchHash     = Animator.StringToHash("InteractCrouchEnter");
        private static readonly int InteractExitHash     = Animator.StringToHash("InteractExit");
        

        [SerializeField] private float     rayDistance = 2f;
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField] private Animator  animator = null!;

        [SerializeField] private float interactStandHeight = 0.65f;
        

        private Coroutine? interactingRoutine;

        private IInputService          inputService          = null!;
        private IInventoryService      inventoryService      = null!;
        private IDialogueService       dialogueService       = null!;
        private IPickupDialogueService pickupDialogueService = null!;
        private DocumentController     documentController    = null!;
        private ContainerController    containerController   = null!;
        private PuzzleViewController    puzzleViewController   = null!;
        private ScreenFader             screenFader            = null!;
        private PickupPreviewController pickupPreviewController = null!;
        private SaveController          saveController          = null!;

        private ISubscriber<DialogueActiveChangedEvent>? dialogueActiveSubscriber;
        private IDisposable?                              dialogueActiveSub;

        [Inject]
        public void Construct(
            IInputService          inputService,
            IInventoryService      inventoryService,
            IDialogueService       dialogueService,
            IPickupDialogueService pickupDialogueService,
            DocumentController     documentController,
            ContainerController    containerController,
            PuzzleViewController    puzzleViewController,
            ScreenFader             screenFader,
            PickupPreviewController pickupPreviewController,
            SaveController          saveController,
            ISubscriber<DialogueActiveChangedEvent> dialogueActiveSubscriber)
        {
            this.inputService          = inputService;
            this.inventoryService      = inventoryService;
            this.dialogueService       = dialogueService;
            this.pickupDialogueService = pickupDialogueService;
            this.documentController    = documentController;
            this.containerController   = containerController;
            this.puzzleViewController   = puzzleViewController;
            this.screenFader            = screenFader;
            this.pickupPreviewController = pickupPreviewController;
            this.saveController          = saveController;
            this.dialogueActiveSubscriber = dialogueActiveSubscriber;
            this.inputService.Interact.performed += OnInteract;
            this.dialogueActiveSub = this.dialogueActiveSubscriber?.Subscribe(OnDialogueActiveChanged);
        }

        private void OnDestroy()
        {
            if (this.inputService != null)
                this.inputService.Interact.performed -= OnInteract;
            this.dialogueActiveSub?.Dispose();
        }

        private void OnDialogueActiveChanged(DialogueActiveChangedEvent ev)
        {
            if (ev.IsActive) return;
            this.animator.SetTrigger(InteractExitHash);
        }

        private void OnInteract(InputAction.CallbackContext _)
        {
            if (!Physics.Raycast(transform.position, transform.forward, out var hit, this.rayDistance, this.interactableLayer))
                return;

            if (!hit.collider.TryGetComponent<IInteractable>(out var interactable))
                return;



            var isPickup = hit.collider.TryGetComponent<PickupInteractable>(out var pickup);
            var isPoi    = hit.collider.TryGetComponent<PoiInteractable>(out var poi);
            if (isPickup || isPoi)
            {
                if (hit.transform.position.y <= interactStandHeight)
                {
                    this.animator.SetTrigger(InteractCrouchHash);
                }
                else
                {
                    this.animator.SetTrigger(InteractStandHash);
                }
            }


            var context = new InteractionContext(
                this.inventoryService,
                this.inputService,
                this.dialogueService,
                this.documentController,
                this.containerController,
                this.pickupDialogueService,
                this.puzzleViewController,
                this.screenFader,
                this.pickupPreviewController,
                this.saveController);
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
