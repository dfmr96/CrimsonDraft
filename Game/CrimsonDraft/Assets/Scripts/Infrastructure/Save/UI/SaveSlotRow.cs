#nullable enable

using System;
using TMPro;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Save.UI
{
    public sealed class SaveSlotRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label = null!;

        public void Bind(SaveSlotSummary summary, bool isSelected)
        {
            string prefix = isSelected ? "> " : "  ";
            this.label.text = summary.isEmpty
                ? $"{prefix}Slot {summary.slot + 1} — empty"
                : $"{prefix}#{summary.saveCount}/{summary.roomId}/{FormatPlaytime(summary.playtimeSeconds)}/{ExtractTime(summary.timestampIso)}";

            gameObject.SetActive(true);
        }

        private static string FormatPlaytime(float seconds)
        {
            var span = TimeSpan.FromSeconds(seconds);
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }

        private static string ExtractTime(string timestamp)
        {
            int spaceIndex = timestamp.IndexOf(' ');
            return spaceIndex >= 0 ? timestamp[(spaceIndex + 1)..] : timestamp;
        }
    }
}
