#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class ContextMenuItemRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label       = null!;
        [SerializeField] private Image           cursorImage = null!;
        [SerializeField] private CanvasGroup     group       = null!;

        public ContextMenuAction Action { get; private set; }

        public void Setup(ContextMenuAction action, bool isCursor, bool isEnabled)
        {
            this.Action              = action;
            this.label.text          = action.ToString();
            this.cursorImage.enabled = isCursor;
            this.group.alpha         = isEnabled ? 1f : 0.4f;
            this.group.interactable  = isEnabled;
        }
    }
}
