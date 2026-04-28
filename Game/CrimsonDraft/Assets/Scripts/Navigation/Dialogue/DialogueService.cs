#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using VContainer.Unity;
using Yarn.Unity;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Navigation.Dialogue
{
    public sealed class DialogueService : IDialogueService, IInitializable
    {
        private readonly DialogueRunner          runner;
        private readonly InMemoryVariableStorage variableStorage;
        private readonly IInputService           inputService;

        private Action?      pendingOnComplete;
        private List<string> sessionCommandNames = new();

        [Preserve]
        public DialogueService(
            DialogueRunner          runner,
            InMemoryVariableStorage variableStorage,
            IInputService           inputService)
        {
            this.runner          = runner;
            this.variableStorage = variableStorage;
            this.inputService    = inputService;
        }

        public bool IsRunning => this.runner.IsDialogueRunning;

        void IInitializable.Initialize()
        {
            this.runner.onDialogueComplete?.AddListener(OnDialogueComplete);
        }

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
