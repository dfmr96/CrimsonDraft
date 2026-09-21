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
    }
}
