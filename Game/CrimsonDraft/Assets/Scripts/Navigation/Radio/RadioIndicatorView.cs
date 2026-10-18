#nullable enable

using UnityEngine;
using VContainer;

namespace CrimsonDraft.Navigation.Radio
{
    /// <summary>
    /// HUD icon (bottom-left) shown while a radio call is waiting. It hides while the inventory is
    /// open, because that is where the call is answered.
    /// </summary>
    public sealed class RadioIndicatorView : MonoBehaviour
    {
        [SerializeField] private GameObject icon = null!;

        private IRadioConversationService? service;
        private bool inventoryOpen;

        [Inject]
        public void Construct(IRadioConversationService service) => this.service = service;

        private void Start()
        {
            if (this.service == null) return;

            this.service.PendingChanged  += Refresh;
            this.service.InventoryOpened += OnInventoryOpened;
            this.service.InventoryClosed += OnInventoryClosed;
            Refresh();
        }

        private void OnDestroy()
        {
            if (this.service == null) return;

            this.service.PendingChanged  -= Refresh;
            this.service.InventoryOpened -= OnInventoryOpened;
            this.service.InventoryClosed -= OnInventoryClosed;
        }

        private void OnInventoryOpened() { this.inventoryOpen = true;  Refresh(); }
        private void OnInventoryClosed() { this.inventoryOpen = false; Refresh(); }

        private void Refresh()
            => this.icon.SetActive(this.service?.PendingNode != null && !this.inventoryOpen);
    }
}
