#nullable enable

using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using Yarn.Unity;

namespace CrimsonDraft.Navigation.Interactables
{
    // Same two-step flow as JewelryBoxInteractable: examine line, then a Yes/No prompt that
    // hard-cuts to the music box's own camera via InspectionController. Lives on the same
    // object as the box's ItemSocketInteractable (the key is placed from the inventory, not
    // from inside the inspection) -- keep this component ABOVE the socket so the interaction
    // caster's TryGetComponent<IInteractable> resolves to it.
    public sealed class MusicBoxInteractable : MonoBehaviour, IInteractable
    {
        private const string EnterInspectionCommand = "enter_music_box_inspection";

        [SerializeField] private DialogueReference examineDialogue       = new();
        [SerializeField] private DialogueReference inspectPromptDialogue = new();
        [SerializeField] private CinemachineCamera inspectCamera         = null!;
        [SerializeField] private MusicBoxPanel     panel                 = null!;

        void Awake()
        {
            // Kept off until InspectionController cuts to it (see GeneratorInteractable).
            this.inspectCamera.enabled = false;
        }

        public void Interact(InteractionContext context)
        {
            context.DialogueService.StartDialogue(
                this.examineDialogue.nodeName ?? "",
                onComplete: () => StartInspectPrompt(context));
        }

        private void StartInspectPrompt(InteractionContext context)
        {
            bool enterInspection = false;

            // Entering has to happen from onComplete, not the command (see the same note in
            // GeneratorInteractable).
            context.PickupDialogueService.StartDialogue(
                this.inspectPromptDialogue.nodeName ?? "",
                onComplete: () =>
                {
                    if (!enterInspection) return;

                    context.InspectionController.Enter(
                        this.inspectCamera,
                        onExit: () => this.panel.Deactivate());
                    this.panel.Activate(context);
                },
                commands: new Dictionary<string, Action>
                {
                    [EnterInspectionCommand] = () => enterInspection = true
                });
        }
    }
}
