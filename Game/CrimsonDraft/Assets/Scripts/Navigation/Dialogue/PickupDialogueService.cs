#nullable enable

using UnityEngine.Scripting;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Navigation.Dialogue
{
    public sealed class PickupDialogueService : DialogueService, IPickupDialogueService
    {
        [Preserve]
        public PickupDialogueService(PickupDialogueRunnerRef runnerRef, IInputService inputService)
            : base(runnerRef.Runner, runnerRef.Storage, inputService) { }
    }
}
