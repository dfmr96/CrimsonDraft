#nullable enable

using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using Yarn.Unity;

namespace CrimsonDraft.Navigation.Interactables
{
    // First pass of the electric-puzzle rework: approach the generator, interact, and get a
    // two-step flow --
    //   1) an examine line through the general DialogueService, same panel as
    //      PoiInteractable/DoorInteractable use;
    //   2) once that closes, a Yes/No prompt through PickupDialogueService, same panel as the
    //      pickup Yes/No confirmation.
    // On "yes" hard-cut to the generator's own camera via InspectionController. Pressing
    // Cancel (B) while inspecting cuts back to whatever camera was active before.
    public sealed class GeneratorInteractable : MonoBehaviour, IInteractable
    {
        private const string EnterInspectionCommand = "enter_generator_inspection";

        [SerializeField] private DialogueReference    examineDialogue       = new();
        [SerializeField] private DialogueReference    inspectPromptDialogue = new();
        [SerializeField] private CinemachineCamera    inspectCamera         = null!;
        [SerializeField] private GeneratorSwitchPanel switchPanel           = null!;

        void Awake()
        {
            // Defensive: keep it off until InspectionController explicitly cuts to it (via
            // IFixedCameraZoneService), same reasoning as ItemSocketInteractable's defensive
            // hide of revealOnActivate -- don't rely solely on the editor-authored state.
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

            context.PickupDialogueService.StartDialogue(
                this.inspectPromptDialogue.nodeName ?? "",
                // Entering inspection has to happen from onComplete, not from the command
                // handler below: DialogueService.OnDialogueComplete() calls
                // ReturnToPreviousInputContext() (SwitchToGameplay) BEFORE invoking onComplete,
                // and the "yes" branch has nothing after the command so the dialogue completes
                // in the same tick the command runs. Switching to the inspection camera/input
                // inside the command got silently reverted to gameplay right after -- UICancel
                // (B) lives on the UI map, so it never fired and "back" looked like a no-op.
                onComplete: () =>
                {
                    if (!enterInspection) return;
                    // Solved puzzles stay closed for good -- nothing more to check or toggle.
                    if (this.switchPanel.IsSolved) return;

                    context.InspectionController.Enter(
                        this.inspectCamera,
                        onExit: () => this.switchPanel.Deactivate());
                    this.switchPanel.Activate();
                },
                commands: new Dictionary<string, Action>
                {
                    [EnterInspectionCommand] = () => enterInspection = true
                });
        }
    }
}
