#nullable enable

using UnityEngine;
using UnityEngine.Scripting;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation
{
    public sealed class OperatorCorpseSpawner : IOperatorCorpseSpawner
    {
        private readonly OperatorCorpseSettings settings;

        [Preserve]
        public OperatorCorpseSpawner(OperatorCorpseSettings settings) => this.settings = settings;

        public void Spawn(RoomController room, Vector3 position, Quaternion rotation)
            => Object.Instantiate(this.settings.CorpsePrefab, position, rotation, room.transform);
    }
}
