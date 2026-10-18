#nullable enable

using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Save.UI
{
    /// <summary>
    /// One row of the world-space Load Game slot list in LoadGame_canva/Save_Games/Saves_data.
    /// Cursor is shown via ManualSelectScale (same look as MenuButtonSelectScale elsewhere in
    /// the main menu), driven manually by LoadGameSaveListView instead of Unity's EventSystem.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ManualSelectScale))]
    public sealed class LoadGameSlotRow : MonoBehaviour
    {
        private TextMeshProUGUI label = null!;
        private ManualSelectScale scaler = null!;

        private void Awake()
        {
            this.label  = GetComponent<TextMeshProUGUI>();
            this.scaler = GetComponent<ManualSelectScale>();
        }

        public void Bind(SaveSlotSummary summary, bool isSelected)
        {
            DOTween.Kill(this);
            this.label.maxVisibleCharacters = int.MaxValue;
            this.label.text = summary.isEmpty ? SaveSlotFormat.EmptyText : SaveSlotFormat.FormatOccupied(summary);
            this.scaler.SetSelected(isSelected);
        }

        private void OnDisable() => DOTween.Kill(this);

        /// <summary>
        /// Types the already-bound text out one character at a time (typewriter). Runs on unscaled
        /// time so it still plays while the game is paused.
        /// </summary>
        /// <param name="durationOverride">Above zero: take this many seconds regardless of charsPerSecond.</param>
        public void Reveal(float charsPerSecond, Action? onDone, float durationOverride = 0f)
        {
            DOTween.Kill(this);
            int total = this.label.text.Length;
            this.label.maxVisibleCharacters = 0;
            float duration = durationOverride > 0f ? durationOverride : total / Mathf.Max(1f, charsPerSecond);

            DOVirtual.Int(0, total, duration, v => this.label.maxVisibleCharacters = v)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    this.label.maxVisibleCharacters = int.MaxValue;
                    onDone?.Invoke();
                });
        }
    }
}
