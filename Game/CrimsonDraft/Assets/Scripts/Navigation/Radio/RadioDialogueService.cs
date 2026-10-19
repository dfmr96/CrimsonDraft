#nullable enable

using System;
using System.Collections.Generic;
using MessagePipe;
using UnityEngine.Scripting;
using Yarn.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.Radio
{
    public sealed class RadioDialogueRunnerRef
    {
        public readonly DialogueRunner          Runner;
        public readonly InMemoryVariableStorage Storage;

        public RadioDialogueRunnerRef(DialogueRunner runner, InMemoryVariableStorage storage)
        {
            Runner  = runner;
            Storage = storage;
        }
    }

    /// <summary>
    /// Runs a radio node. Before the node starts it publishes who is dead as Yarn variables
    /// ($dead_deer, $dead_sterna, ...), which is what the generated .yarn files branch on.
    /// </summary>
    public sealed class RadioDialogueService : DialogueService
    {
        private readonly DialogueRunner runner;
        private readonly IOperatorRoster roster;
        private readonly RadioCastData   cast;

        // radio_open "freq" "left" "right,ids" -- first command of every generated node.
        public event Action<string, string, string[]>? RadioOpened;

        [Preserve]
        public RadioDialogueService(
            RadioDialogueRunnerRef                  runnerRef,
            IInputService                           inputService,
            IPublisher<DialogueActiveChangedEvent>  dialoguePublisher,
            IOperatorRoster                         roster,
            RadioCastData                           cast)
            : base(runnerRef.Runner, runnerRef.Storage, inputService, dialoguePublisher)
        {
            this.runner = runnerRef.Runner;
            this.roster = roster;
            this.cast   = cast;
            this.runner.AddCommandHandler("radio_open", (Action<string, string, string>)OnRadioOpen);
        }

        public void Play(string nodeName, Action? onComplete)
        {
            this.roster.EnsureInitialized();
            StartDialogue(nodeName, BuildDeadFlags(), onComplete);
        }

        public void Stop() => _ = this.runner.Stop();

        // The node headers (freq / left / right) written by to-yarn.ps1, readable before the node
        // runs so the radio can show who is on the call while it is still waiting to be answered.
        public bool TryGetMeta(string nodeName, out string freq, out string leftId, out string[] rightIds)
        {
            freq = leftId = string.Empty;
            rightIds = Array.Empty<string>();
            try
            {
                var dialogue = this.runner.Dialogue;
                leftId = dialogue.GetHeaderValue(nodeName, "left");
                if (string.IsNullOrEmpty(leftId)) return false;

                freq     = dialogue.GetHeaderValue(nodeName, "freq") ?? string.Empty;
                rightIds = (dialogue.GetHeaderValue(nodeName, "right") ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Radio calls play over the inventory (which stays open underneath), so control goes back
        // to it -- same reasoning as InspectDialogueService.
        protected override void ReturnToPreviousInputContext() => this.inputService.SwitchToInventory();

        // Roster operators are matched to cast ids by callsign. Anyone in the cast who isn't in
        // the roster (e.g. Vulture, who is on the other end of the radio) is never dead.
        public Dictionary<string, object> BuildDeadFlags()
        {
            var flags = new Dictionary<string, object>();
            foreach (var entry in this.cast.Entries)
                flags["$dead_" + entry.id.ToLowerInvariant()] = false;

            for (int i = 0; i < this.roster.Count; i++)
            {
                var op = this.roster[i];
                if (op.Data == null) continue;
                flags["$dead_" + op.Data.CombatName.ToLowerInvariant()] = !op.IsAlive;
            }

            return flags;
        }

        private void OnRadioOpen(string freq, string leftId, string rightIds)
            => RadioOpened?.Invoke(freq, leftId, rightIds.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }
}
