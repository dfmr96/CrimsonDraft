#nullable enable

using UnityEngine;

namespace CrimsonDraft.Infrastructure.UI
{
    /// <summary>
    /// The single highlight strip that glides between the items of a menu. Lives in Infrastructure
    /// so views here (the Load Game slot list) can drive it without referencing the Navigation
    /// assembly, where the implementation (MenuSelectionBar) is.
    /// </summary>
    public abstract class SelectionCursor : MonoBehaviour
    {
        /// <summary>Glides to <paramref name="anchor"/>, or hides when it is null.</summary>
        public abstract void FollowOrHide(RectTransform? anchor, bool snap = false);

        public abstract void SetVisible(bool visible);

        /// <summary>Brief white pulse that fades back to the normal color.</summary>
        public abstract void Flash();
    }
}
