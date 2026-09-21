#nullable enable

using MessagePipe;
using UnityEngine.Scripting;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Navigation.Dialogue
{
    public sealed class InspectDialogueService : DialogueService, IInspectDialogueService
    {
        [Preserve]
        public InspectDialogueService(
            InspectDialogueRunnerRef                       runnerRef,
            IInputService                                  inputService,
            IPublisher<DialogueActiveChangedEvent>         dialoguePublisher)
            : base(runnerRef.Runner, runnerRef.Storage, inputService, dialoguePublisher) { }

        // These prompts run while InspectPanel (part of the inventory/inspect UI) is still
        // open underneath -- falling back to the gameplay map here would leave
        // InventoryConfirm/InventoryNavigate disabled once the prompt ends, locking out any
        // further interaction with the still-open panel (e.g. a hotspot's reward text).
        protected override void ReturnToPreviousInputContext() => this.inputService.SwitchToInventory();
    }
}
