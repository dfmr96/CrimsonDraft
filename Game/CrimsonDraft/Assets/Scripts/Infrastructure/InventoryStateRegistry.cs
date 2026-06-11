#nullable enable

using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public sealed class InventoryStateRegistry
    {
        private object? savedState;

        [Preserve]
        public InventoryStateRegistry() { }

        public bool HasSavedState => this.savedState != null;

        public void Save(object state) => this.savedState = state;

        public T? Load<T>() where T : class => this.savedState as T;
    }
}
