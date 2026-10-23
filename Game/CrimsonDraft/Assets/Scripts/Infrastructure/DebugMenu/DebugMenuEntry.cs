#nullable enable

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;

namespace CrimsonDraft.Infrastructure.DebugMenu
{
    /// <summary>One line of the debug menu: either a one-shot action or an on/off toggle.</summary>
    public sealed class DebugMenuEntry
    {
        private readonly Action?       activate;
        private readonly Func<bool>?   getState;
        private readonly Action<bool>? setState;

        private DebugMenuEntry(string group, string label, Action? activate, Func<bool>? getState, Action<bool>? setState)
        {
            Group         = group;
            Label         = label;
            this.activate = activate;
            this.getState = getState;
            this.setState = setState;
        }

        public string Group { get; }
        public string Label { get; }
        public bool   IsToggle => this.getState != null;

        /// <summary>Current state of a toggle; always false for an action.</summary>
        public bool IsOn => this.getState != null && this.getState();

        public static DebugMenuEntry Action(string group, string label, Action activate)
            => new DebugMenuEntry(group, label, activate, null, null);

        public static DebugMenuEntry Toggle(string group, string label, Func<bool> getState, Action<bool> setState)
            => new DebugMenuEntry(group, label, null, getState, setState);

        /// <summary>Runs the action, or flips the toggle.</summary>
        public void Activate()
        {
            if (this.getState != null && this.setState != null) this.setState(!this.getState());
            else this.activate?.Invoke();
        }
    }
}

#endif
