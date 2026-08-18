#nullable enable

using System;

namespace CrimsonDraft.Infrastructure.Save
{
    [Serializable]
    public struct SaveSlotSummary
    {
        public int    slot;
        public bool   isEmpty;
        public string roomId;
        public string timestampIso;
        public float  playtimeSeconds;
    }
}
