#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public sealed class OperatorCorpseRegistry
    {
        public readonly struct Entry
        {
            public int        SlotIndex { get; }
            public string     RoomId    { get; }
            public Vector3    Position  { get; }
            public Quaternion Rotation  { get; }

            public Entry(int slotIndex, string roomId, Vector3 position, Quaternion rotation)
            {
                SlotIndex = slotIndex;
                RoomId    = roomId;
                Position  = position;
                Rotation  = rotation;
            }
        }

        private readonly Dictionary<int, Entry> recorded = new();

        [Preserve]
        public OperatorCorpseRegistry() { }

        public bool IsRecorded(int slotIndex) => this.recorded.ContainsKey(slotIndex);

        public void Record(int slotIndex, string roomId, Vector3 position, Quaternion rotation)
        {
            if (this.recorded.ContainsKey(slotIndex)) return;
            this.recorded[slotIndex] = new Entry(slotIndex, roomId, position, rotation);
        }

        public IReadOnlyCollection<Entry> GetAll() => this.recorded.Values;

        public void LoadState(IEnumerable<Entry> saved)
        {
            this.recorded.Clear();
            foreach (var entry in saved)
                this.recorded[entry.SlotIndex] = entry;
        }

        public void ClearAll() => this.recorded.Clear();
    }
}
