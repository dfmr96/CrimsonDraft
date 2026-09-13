#nullable enable

using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public sealed class RosterHealthRegistry
    {
        private int[]? savedHp;

        [Preserve]
        public RosterHealthRegistry() { }

        public bool HasSavedState => this.savedHp != null;

        public void Save(int[] hp) => this.savedHp = hp;

        public int[]? Load() => this.savedHp;

        public void ClearAll() => this.savedHp = null;
    }
}
