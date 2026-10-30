#nullable enable

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.DebugMenu
{
    /// <summary>Where debug features list themselves. Lives in the root scope so that
    /// navigation and combat can add entries while they are loaded and remove them on dispose.</summary>
    public interface IDebugMenu
    {
        /// <summary>Entries grouped by group (groups in the order they first appeared), registration order within a group.</summary>
        IReadOnlyList<DebugMenuEntry> Entries { get; }

        /// <summary>Raised whenever an entry is added or removed.</summary>
        event Action Changed;

        /// <summary>Adds the entry; dispose the result to remove it again.</summary>
        IDisposable Register(DebugMenuEntry entry);
    }

    public sealed class DebugMenuRegistry : IDebugMenu
    {
        private readonly List<DebugMenuEntry> registered = new List<DebugMenuEntry>();
        private IReadOnlyList<DebugMenuEntry>? cached;

        [Preserve]
        public DebugMenuRegistry() { }

        public event Action? Changed;

        public IReadOnlyList<DebugMenuEntry> Entries
            => this.cached ??= this.registered
                .GroupBy(e => e.Group)
                .SelectMany(g => g)
                .ToList();

        public IDisposable Register(DebugMenuEntry entry)
        {
            this.registered.Add(entry);
            Invalidate();
            return new Registration(this, entry);
        }

        private void Remove(DebugMenuEntry entry)
        {
            if (!this.registered.Remove(entry)) return;
            Invalidate();
        }

        private void Invalidate()
        {
            this.cached = null;
            this.Changed?.Invoke();
        }

        private sealed class Registration : IDisposable
        {
            private DebugMenuRegistry? owner;
            private readonly DebugMenuEntry entry;

            public Registration(DebugMenuRegistry owner, DebugMenuEntry entry)
            {
                this.owner = owner;
                this.entry = entry;
            }

            public void Dispose()
            {
                this.owner?.Remove(this.entry);
                this.owner = null;
            }
        }
    }
}

#endif
