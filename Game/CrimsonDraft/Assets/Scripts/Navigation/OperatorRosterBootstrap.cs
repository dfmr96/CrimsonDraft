#nullable enable

using CrimsonDraft.Operators;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace CrimsonDraft.Navigation
{
    public sealed class OperatorRosterBootstrap : IInitializable
    {
        private readonly IOperatorRoster roster;

        [Preserve]
        public OperatorRosterBootstrap(IOperatorRoster roster) => this.roster = roster;

        void IInitializable.Initialize() => this.roster.EnsureInitialized();
    }
}
