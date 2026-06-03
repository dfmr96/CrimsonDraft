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

        public void Bind(DocumentData doc)
        {
            this.iconImage.enabled = doc.Icon != null;
            if (doc.Icon != null) this.iconImage.sprite = doc.Icon;
            if (this.titleLabel != null) this.titleLabel.text = doc.Title;
        }
    }
}
