#nullable enable

using System;

namespace CrimsonDraft.Infrastructure.Save.UI
{
    internal static class SaveSlotFormat
    {
        public static string FormatPlaytime(float seconds)
        {
            var span = TimeSpan.FromSeconds(seconds);
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }

        public static string ExtractTime(string timestamp)
        {
            int spaceIndex = timestamp.IndexOf(' ');
            return spaceIndex >= 0 ? timestamp[(spaceIndex + 1)..] : timestamp;
        }

        public static string FormatOccupied(SaveSlotSummary summary) =>
            $"#{summary.saveCount}/{summary.roomId}/{FormatPlaytime(summary.playtimeSeconds)}/{ExtractTime(summary.timestampIso)}";
    }
}
