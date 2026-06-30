#nullable enable

using System;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Operators;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace CrimsonDraft.Navigation
{
    public sealed class OperatorRosterBootstrap : IInitializable, IDisposable
    {
        private readonly IOperatorRoster      roster;
        private readonly RosterHealthRegistry registry;

        [Preserve]
        public OperatorRosterBootstrap(IOperatorRoster roster, RosterHealthRegistry registry)
        {
            this.roster   = roster;
            this.registry = registry;
        }

        void IInitializable.Initialize()
        {
            this.roster.EnsureInitialized();
            var saved = this.registry.Load();
            if (saved != null)
                this.roster.RestoreHp(saved);
        }

        public void Dispose() => this.registry.Save(this.roster.GetHpSnapshot());
    }
}
