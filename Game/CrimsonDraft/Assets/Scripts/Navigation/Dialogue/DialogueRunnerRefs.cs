#nullable enable

using Yarn.Unity;

namespace CrimsonDraft.Navigation.Dialogue
{
    public sealed class GeneralDialogueRunnerRef
    {
        public readonly DialogueRunner          Runner;
        public readonly InMemoryVariableStorage Storage;

        public GeneralDialogueRunnerRef(DialogueRunner runner, InMemoryVariableStorage storage)
        {
            Runner  = runner;
            Storage = storage;
        }
    }

    public sealed class PickupDialogueRunnerRef
    {
        public readonly DialogueRunner          Runner;
        public readonly InMemoryVariableStorage Storage;

        public PickupDialogueRunnerRef(DialogueRunner runner, InMemoryVariableStorage storage)
        {
            Runner  = runner;
            Storage = storage;
        }
    }
}
