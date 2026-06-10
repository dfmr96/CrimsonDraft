#nullable enable

using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PickupBootstrap : IInitializable
    {
        private readonly PickupRegistry      registry;
        private readonly PickupInteractable[] pickups;

        [Preserve]
        public PickupBootstrap(PickupRegistry registry, PickupInteractable[] pickups)
        {
            this.registry = registry;
            this.pickups  = pickups;
        }

        void IInitializable.Initialize()
        {
            foreach (var pickup in this.pickups)
                pickup.Construct(this.registry);
        }
    }
}
