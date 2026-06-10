#nullable enable

using System;
using System.Collections.Generic;

namespace CrimsonDraft.Navigation.Dialogue
{
    public interface IDialogueService
    {
        bool IsRunning { get; }

        void StartDialogue(
            string                                  nodeName,
            IReadOnlyDictionary<string, object>?   variables  = null,
            Action?                                 onComplete = null,
            IReadOnlyDictionary<string, Action>?   commands   = null);

        void SetVariable(string name, object value);
    }
}
