#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public sealed class EnemyStateRegistry
    {
        private readonly HashSet<string> defeated = new();

        [Preserve]
        public EnemyStateRegistry() { }

        public bool IsDefeated(string enemyKey) => this.defeated.Contains(enemyKey);

        public void SetDefeated(string enemyKey) => this.defeated.Add(enemyKey);

        public IReadOnlyCollection<string> GetDefeated() => this.defeated;

        public void LoadState(IEnumerable<string> saved)
        {
            this.defeated.Clear();
            foreach (var key in saved)
                this.defeated.Add(key);
        }

        public void ClearAll() => this.defeated.Clear();
    }
}
