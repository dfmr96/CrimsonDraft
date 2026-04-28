#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.UI
{
    /// <summary>
    /// One button in the inventory context menu.
    /// Displays the action label and highlights when the cursor is on it.
    /// </summary>
    public sealed class InventoryActionButton : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label          = null!;
        [SerializeField] private Image           cursorHighlight = null!;

        public ContextMenuAction Action { get; private set; }

        public void Setup(ContextMenuAction action, bool isCursor)
        {
            this.Action                    = action;
            this.label.text                = GetDisplayName(action);
            this.cursorHighlight.enabled   = isCursor;
        }

        private static string GetDisplayName(ContextMenuAction action) => action switch
        {
            ContextMenuAction.Equip    => "Equip",
            ContextMenuAction.Unequip  => "Unequip",
            ContextMenuAction.Use      => "Use",
            ContextMenuAction.Combine  => "Combine",
            ContextMenuAction.Examine  => "Examine",
            _                          => action.ToString()
        };
    }
}
