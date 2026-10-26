#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI
{
    // Shared by the inventory grid and the operator widget so a weapon's rounds read the same
    // colour whether it sits in the grid or is equipped.
    internal static class ItemCountColors
    {
        // Rounds loaded in a weapon.
        public static readonly Color Loaded = new Color(154f / 255f, 159f / 255f, 92f / 255f, 1f); // the UI's olive

        // Stack counts: ammo boxes, Ticker Tape and any other stackable item.
        public static readonly Color Stack = new Color(0f, 0.85f, 1f, 1f); // 0 217 255
    }
}
