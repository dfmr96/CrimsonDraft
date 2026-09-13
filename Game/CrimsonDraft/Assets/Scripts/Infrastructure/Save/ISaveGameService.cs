#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Infrastructure.Save
{
    public interface ISaveGameService
    {
        IReadOnlyList<SaveSlotSummary> ListSlotSummaries();
        void WriteToDisk(int slot, SaveGameData data);
        SaveGameData? ReadFromDisk(int slot);

        /// <summary>Deletes the slot's file if it exists. Returns false if the slot was already empty.</summary>
        bool DeleteSlot(int slot);

        /// <summary>Reads the slot, stashes it as the pending load, and loads its scene. Returns false if the slot is empty.</summary>
        bool LoadSlot(int slot);

        /// <summary>Returns and clears the payload stashed by LoadSlot, or null if nothing is pending.</summary>
        SaveGameData? ConsumePendingLoad();
    }
}
