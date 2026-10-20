#nullable enable

using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Pushables
{
    // Hands every pushable in the scene the registry of saved positions. Registered after
    // SaveGameLoader, so a loaded save's positions are already in the registry.
    public sealed class PushableBootstrap : IInitializable
    {
        private readonly PushableStateRegistry registry;
        private readonly PushableObject[]      pushables;

        [Preserve]
        public PushableBootstrap(PushableStateRegistry registry, PushableObject[] pushables)
        {
            this.registry  = registry;
            this.pushables = pushables;
        }

        void IInitializable.Initialize()
        {
            foreach (var pushable in this.pushables)
                pushable.Construct(this.registry);
        }
    }
}
