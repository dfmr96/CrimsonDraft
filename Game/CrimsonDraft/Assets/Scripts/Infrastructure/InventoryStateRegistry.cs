#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;
using CrimsonDraft.Infrastructure.Save;

namespace CrimsonDraft.Infrastructure
{
    public sealed class InventoryStateRegistry
    {
        private List<InventoryItemEntry>? savedState;

        [Preserve]
        public InventoryStateRegistry() { }

        public bool HasSavedState => this.savedState != null;

        public void Save(IReadOnlyList<InventoryItemEntry> state) => this.savedState = new List<InventoryItemEntry>(state);

        public IReadOnlyList<InventoryItemEntry>? Load() => this.savedState;

        public void ClearAll() => this.savedState = null;
    }
}
