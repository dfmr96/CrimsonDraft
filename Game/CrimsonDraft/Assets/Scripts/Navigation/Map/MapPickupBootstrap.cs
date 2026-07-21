#nullable enable

using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Map
{
    public sealed class MapPickupBootstrap : IInitializable
    {
        private readonly PickupRegistry           pickupRegistry;
        private readonly KnownMapsRegistry        knownMaps;
        private readonly MapPickupInteractable[]  pickups;

        [Preserve]
        public MapPickupBootstrap(
            PickupRegistry          pickupRegistry,
            KnownMapsRegistry       knownMaps,
            MapPickupInteractable[] pickups)
        {
            this.pickupRegistry = pickupRegistry;
            this.knownMaps      = knownMaps;
            this.pickups        = pickups;
        }

        void IInitializable.Initialize()
        {
            foreach (var pickup in this.pickups)
                pickup.Construct(this.pickupRegistry, this.knownMaps);
        }
    }
}
