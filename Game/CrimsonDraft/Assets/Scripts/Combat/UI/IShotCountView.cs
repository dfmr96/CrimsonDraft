#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface IShotCountView
    {
        int Value { get; }
        int MaxValue { get; }
        void Show(RectTransform commandPanelRect, int initial, int max);
        void Hide();
        void Increment();
        void Decrement();
    }
}
