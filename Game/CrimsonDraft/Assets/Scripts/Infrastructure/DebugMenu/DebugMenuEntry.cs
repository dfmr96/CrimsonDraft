#nullable enable

#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace CrimsonDraft.Infrastructure.DebugMenu
{
    /// <summary>One line of the debug menu: a one-shot action, an on/off toggle, or a choice
    /// (Left/Right cycles a list of options, Enter runs it with the shown option).</summary>
    public sealed class DebugMenuEntry
    {
        private readonly Action?                       activate;
        private readonly Func<bool>?                   getState;
        private readonly Action<bool>?                 setState;
        private readonly Func<IReadOnlyList<string>>?  options;
        private readonly Action<string>?               choose;
        private string?                                chosen;

        private DebugMenuEntry(string group, string label, Action? activate, Func<bool>? getState, Action<bool>? setState,
                               Func<IReadOnlyList<string>>? options = null, Action<string>? choose = null)
        {
            Group         = group;
            Label         = label;
            this.activate = activate;
            this.getState = getState;
            this.setState = setState;
            this.options  = options;
            this.choose   = choose;
        }

        public string Group    { get; }
        public string Label    { get; }
        public bool   IsToggle => this.getState != null;
        public bool   IsChoice => this.options != null;

        /// <summary>Current state of a toggle; always false for an action.</summary>
        public bool IsOn => this.getState != null && this.getState();

        /// <summary>The option a choice shows: the one picked last while it is still listed, else the
        /// first; null without options (and for non-choice entries).</summary>
        public string? CurrentOption
        {
            get
            {
                if (this.options == null) return null;
                var list = this.options();
                if (list.Count == 0) return null;
                if (this.chosen == null || !Contains(list, this.chosen)) this.chosen = list[0];
                return this.chosen;
            }
        }

        public static DebugMenuEntry Action(string group, string label, Action activate)
            => new DebugMenuEntry(group, label, activate, null, null);

        public static DebugMenuEntry Toggle(string group, string label, Func<bool> getState, Action<bool> setState)
            => new DebugMenuEntry(group, label, null, getState, setState);

        public static DebugMenuEntry Choice(string group, string label, Func<IReadOnlyList<string>> options, Action<string> activate)
            => new DebugMenuEntry(group, label, null, null, null, options, activate);

        /// <summary>Moves a choice <paramref name="delta"/> options, wrapping around; no-op otherwise.</summary>
        public void Cycle(int delta)
        {
            var current = CurrentOption;
            if (current == null) return;
            var list  = this.options!();
            int index = IndexOf(list, current);
            this.chosen = list[((index + delta) % list.Count + list.Count) % list.Count];
        }

        /// <summary>Runs the action, flips the toggle, or runs the choice with its current option.</summary>
        public void Activate()
        {
            if (this.options != null)
            {
                var option = CurrentOption;
                if (option != null) this.choose?.Invoke(option);
            }
            else if (this.getState != null && this.setState != null) this.setState(!this.getState());
            else this.activate?.Invoke();
        }

        private static bool Contains(IReadOnlyList<string> list, string value) => IndexOf(list, value) >= 0;

        private static int IndexOf(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], value, StringComparison.Ordinal)) return i;
            return -1;
        }
    }
}

#endif
