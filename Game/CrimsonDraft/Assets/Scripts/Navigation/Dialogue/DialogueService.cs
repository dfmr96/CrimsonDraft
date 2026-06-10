#nullable enable

using System;
using System.Collections.Generic;
using VContainer;
using Yarn.Unity;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Navigation.Dialogue
{
    public class DialogueService : IDialogueService
    {
        private readonly DialogueRunner          runner;
        private readonly InMemoryVariableStorage variableStorage;
        private readonly IInputService           inputService;

        private Action?      pendingOnComplete;
        private List<string> sessionCommandNames = new();

        [Inject]
        [Preserve]
        public DialogueService(GeneralDialogueRunnerRef runnerRef, IInputService inputService)
            : this(runnerRef.Runner, runnerRef.Storage, inputService) { }

        protected DialogueService(
            DialogueRunner          runner,
            InMemoryVariableStorage variableStorage,
            IInputService           inputService)
        {
            this.runner          = runner;
            this.variableStorage = variableStorage;
            this.inputService    = inputService;
            this.runner.onDialogueComplete?.AddListener(OnDialogueComplete);
        }

        public bool IsRunning => this.runner.IsDialogueRunning;

        public void StartDialogue(
            string                                  nodeName,
            IReadOnlyDictionary<string, object>?   variables  = null,
            Action?                                 onComplete = null,
            IReadOnlyDictionary<string, Action>?   commands   = null)
        {
            this.pendingOnComplete = onComplete;

            this.variableStorage.Clear();
            if (variables != null)
            {
                foreach (var (key, value) in variables)
                {
                    switch (value)
                    {
                        case bool b:   this.variableStorage.SetValue(key, b); break;
                        case string s: this.variableStorage.SetValue(key, s); break;
                        case float f:  this.variableStorage.SetValue(key, f); break;
                        case int i:    this.variableStorage.SetValue(key, (float)i); break;
                    }
                }
            }

            this.sessionCommandNames.Clear();
            if (commands != null)
            {
                foreach (var (name, action) in commands)
                {
                    this.runner.AddCommandHandler(name, (Delegate)action);
                    this.sessionCommandNames.Add(name);
                }
            }

            this.inputService.SwitchToDialogue();
            _ = this.runner.StartDialogue(nodeName);
        }

        public void SetVariable(string name, object value)
        {
            switch (value)
            {
                case bool b:   this.variableStorage.SetValue(name, b); break;
                case string s: this.variableStorage.SetValue(name, s); break;
                case float f:  this.variableStorage.SetValue(name, f); break;
                case int i:    this.variableStorage.SetValue(name, (float)i); break;
            }
        }

        private void OnDialogueComplete()
        {
            this.inputService.SwitchToGameplay();

            foreach (var name in this.sessionCommandNames)
                this.runner.RemoveCommandHandler(name);
            this.sessionCommandNames.Clear();

            var callback           = this.pendingOnComplete;
            this.pendingOnComplete = null;
            callback?.Invoke();
        }
    }
}
