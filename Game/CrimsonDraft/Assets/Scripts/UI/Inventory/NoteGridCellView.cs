#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.UI
{
    public sealed class NoteGridCellView : MonoBehaviour
    {
        [SerializeField] private Image     iconImage  = null!;
        [SerializeField] private TMP_Text? titleLabel;

        /// <summary>
        /// A found note shows its own icon and title; one that has not been found yet keeps its place
        /// in the grid with <paramref name="missingIcon"/> and no title.
        /// </summary>
        public void Bind(DocumentData doc, bool collected, Sprite? missingIcon)
        {
            Sprite? icon = collected ? doc.Icon : missingIcon;

            this.iconImage.enabled = icon != null;
            if (icon != null) this.iconImage.sprite = icon;
            if (this.titleLabel != null) this.titleLabel.text = collected ? doc.Title : string.Empty;
        }
    }
}
