#nullable enable

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Infrastructure.Save.UI
{
    public sealed class SaveSlotRow : MonoBehaviour
    {
        [SerializeField] private Button           button = null!;
        [SerializeField] private TextMeshProUGUI  label  = null!;

        public void Bind(SaveSlotSummary summary, Action onClick)
        {
            this.label.text = summary.isEmpty
                ? $"Slot {summary.slot + 1} — empty"
                : $"Slot {summary.slot + 1} — {summary.roomId} — {FormatPlaytime(summary.playtimeSeconds)} — {summary.timestampIso}";

            this.button.onClick.RemoveAllListeners();
            this.button.onClick.AddListener(() => onClick());
            gameObject.SetActive(true);
        }

        private static string FormatPlaytime(float seconds)
        {
            var span = TimeSpan.FromSeconds(seconds);
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }
    }
}
