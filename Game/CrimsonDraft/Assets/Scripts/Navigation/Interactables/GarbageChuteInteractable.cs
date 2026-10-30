#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

namespace CrimsonDraft.Navigation.Interactables
{
    // A scene door you have to confirm first: a Yes/No prompt through PickupDialogueService (same
    // panel as the pickup confirmation), and only on "yes" does it behave as the base scene door
    // (lock check, transition). Everything else -- ids, arrival, map marker -- is inherited.
    public sealed class GarbageChuteInteractable : SceneDoorInteractable
    {
        private const string JumpCommand = "jump_in_chute";

        [SerializeField] private DialogueReference jumpPromptDialogue = new();

        public override void Interact(InteractionContext context)
        {
            bool jump = false;

            context.PickupDialogueService.StartDialogue(
                this.jumpPromptDialogue.nodeName ?? "",
                // Acting from onComplete, not from the command: the dialogue returns to the previous
                // input context before onComplete runs, and the transition has to start after that
                // (same reason as GeneratorInteractable).
                onComplete: () =>
                {
                    if (jump) base.Interact(context);
                },
                commands: new Dictionary<string, Action>
                {
                    [JumpCommand] = () => jump = true
                });
        }
    }
}
