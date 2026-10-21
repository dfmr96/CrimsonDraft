#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public sealed class EnemyStateRegistry
    {
        public readonly struct CorpsePose
        {
            public string     EnemyKey { get; }
            public Vector3    Position { get; }
            public Quaternion Rotation { get; }

            public CorpsePose(string enemyKey, Vector3 position, Quaternion rotation)
            {
                EnemyKey = enemyKey;
                Position = position;
                Rotation = rotation;
            }
        }

        private readonly HashSet<string> defeated = new();

        // Where each defeated enemy's body lies. A key can be defeated without a pose (saves
        // written before corpses existed) -- that body then lies at its scene spawn point.
        private readonly Dictionary<string, CorpsePose> corpses = new();

        [Preserve]
        public EnemyStateRegistry() { }

        public bool IsDefeated(string enemyKey) => this.defeated.Contains(enemyKey);

        public void SetDefeated(string enemyKey) => this.defeated.Add(enemyKey);

        public void SetDefeated(string enemyKey, Vector3 position, Quaternion rotation)
        {
            this.defeated.Add(enemyKey);
            this.corpses[enemyKey] = new CorpsePose(enemyKey, position, rotation);
        }

        public bool TryGetCorpsePose(string enemyKey, out CorpsePose pose) => this.corpses.TryGetValue(enemyKey, out pose);

        public IReadOnlyCollection<string> GetDefeated() => this.defeated;

        public IReadOnlyCollection<CorpsePose> GetCorpsePoses() => this.corpses.Values;

        public void LoadState(IEnumerable<string> saved) => LoadState(saved, System.Array.Empty<CorpsePose>());

        public void LoadState(IEnumerable<string> saved, IEnumerable<CorpsePose> poses)
        {
            this.defeated.Clear();
            this.corpses.Clear();
            foreach (var key in saved)
                this.defeated.Add(key);
            foreach (var pose in poses)
            {
                this.defeated.Add(pose.EnemyKey);
                this.corpses[pose.EnemyKey] = pose;
            }
        }

        public void ClearAll()
        {
            this.defeated.Clear();
            this.corpses.Clear();
        }
    }
}
