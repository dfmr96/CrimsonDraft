#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI
{
    public class InventorySoundManager : MonoBehaviour
    {
        [Header("Cursor")]
        [SerializeField] private AK.Wwise.Event cursorMoveEvent    = new();
        [SerializeField] private AK.Wwise.Event cursorOnItemEvent  = new();

        [Header("Item Movement")]
        [SerializeField] private AK.Wwise.Event itemPickupEvent       = new();
        [SerializeField] private AK.Wwise.Event itemMoveEvent         = new();
        [SerializeField] private AK.Wwise.Event itemPlaceEvent        = new();
        [SerializeField] private AK.Wwise.Event itemPlaceInvalidEvent = new();
        [SerializeField] private AK.Wwise.Event itemRotateEvent       = new();
        [SerializeField] private AK.Wwise.Event itemSwapEvent         = new();

        [Header("Context Menu")]
        [SerializeField] private AK.Wwise.Event menuOpenEvent     = new();
        [SerializeField] private AK.Wwise.Event menuNavigateEvent = new();
        [SerializeField] private AK.Wwise.Event menuConfirmEvent  = new();
        [SerializeField] private AK.Wwise.Event menuCancelEvent   = new();

        [Header("Inspect")]
        [SerializeField] private AK.Wwise.Event inspectOpenEvent  = new();
        [SerializeField] private AK.Wwise.Event inspectCloseEvent = new();

        [Header("Tabs")]
        [SerializeField] private AK.Wwise.Event tabSwitchEvent = new();

        // ── Cursor ───────────────────────────────────────────────────────────
        public void PlayCursorMove()       => Post(this.cursorMoveEvent);
        public void PlayCursorOnItem()     => Post(this.cursorOnItemEvent);

        // ── Item Movement ────────────────────────────────────────────────────
        public void PlayItemPickup()       => Post(this.itemPickupEvent);
        public void PlayItemMove()         => Post(this.itemMoveEvent);
        public void PlayItemPlace()        => Post(this.itemPlaceEvent);
        public void PlayItemPlaceInvalid() => Post(this.itemPlaceInvalidEvent);
        public void PlayItemRotate()       => Post(this.itemRotateEvent);
        public void PlayItemSwap()         => Post(this.itemSwapEvent);

        // ── Context Menu ─────────────────────────────────────────────────────
        public void PlayMenuOpen()         => Post(this.menuOpenEvent);
        public void PlayMenuNavigate()     => Post(this.menuNavigateEvent);
        public void PlayMenuConfirm()      => Post(this.menuConfirmEvent);
        public void PlayMenuCancel()       => Post(this.menuCancelEvent);

        // ── Inspect ──────────────────────────────────────────────────────────
        public void PlayInspectOpen()      => Post(this.inspectOpenEvent);
        public void PlayInspectClose()     => Post(this.inspectCloseEvent);

        // ── Tabs ─────────────────────────────────────────────────────────────
        public void PlayTabSwitch()        => Post(this.tabSwitchEvent);

        // ── Core ─────────────────────────────────────────────────────────────
        private void Post(AK.Wwise.Event ev) => ev?.Post(gameObject);
    }
}
