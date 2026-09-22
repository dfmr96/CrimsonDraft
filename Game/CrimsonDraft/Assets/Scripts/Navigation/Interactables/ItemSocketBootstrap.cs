#nullable enable

using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class ItemSocketBootstrap : IInitializable
    {
        private readonly ItemSocketStateRegistry  registry;
        private readonly ItemSocketInteractable[] sockets;

        [Preserve]
        public ItemSocketBootstrap(ItemSocketStateRegistry registry, ItemSocketInteractable[] sockets)
        {
            this.registry = registry;
            this.sockets  = sockets;
        }

        void IInitializable.Initialize()
        {
            foreach (var socket in this.sockets)
                socket.Construct(this.registry);
        }
    }
}
