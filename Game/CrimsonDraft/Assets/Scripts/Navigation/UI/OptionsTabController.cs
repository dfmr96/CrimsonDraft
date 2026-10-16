#nullable enable

using CrimsonDraft.Navigation.UI;
using CrimsonDraft.Infrastructure.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// Owns navigation for the whole Options screen (all UI, no physical knobs). Three index
    /// buttons (Video / Control / Sound) sit on top: Left/Right switches the active tab right away
    /// (one shared bar glides under the active tab; it is white while the cursor is on the tab row and
    /// goes back to the UI green once the cursor enters that tab's content), Down enters that tab's content
    /// list, and Up from the first item returns to the tab row. Inside the content list
    /// Up/Down moves between channels and Left/Right/Confirm go to the active
    /// <see cref="IOptionsChannelPanel"/>.
    /// </summary>
    public sealed class OptionsTabController : MonoBehaviour
    {
        [System.Serializable]
        private sealed class Tab
        {
            [SerializeField] public GameObject buttonImage = null!;
            [SerializeField] public GameObject group       = null!;
            [SerializeField] public MonoBehaviour panel    = null!;
        }

        [Header("Tabs (0 = Video, 1 = Control, 2 = Sound)")]
        [SerializeField] private Tab[] tabs = null!;

        [Header("Selection bar")]
        [Tooltip("Barra unica que se desliza bajo el tab activo y se pone blanca al entrar a su contenido. Las imagenes de cada tab quedan solo como anclas invisibles.")]
        [SerializeField] private MenuSelectionBar? tabBar;

        [Header("Hold-to-repeat (Left/Right on a channel)")]
        [SerializeField] private float initialRepeatDelay = 0.35f;
        [SerializeField] private float repeatInterval     = 0.08f;

        private IInputService   inputService = null!;
        private InventorySfxData sfx         = null!;

        private bool isOpen;
        private int  activeTab;

        /// <summary>Raised whenever a tab becomes the active one (0 = Video, 1 = Control, 2 = Sound), including on Open().</summary>
        public event System.Action<int>? ActiveTabChanged;
        private bool onTabRow;
        private int  contentIndex;

        private int   heldHorizontalDirection;
        private float horizontalRepeatTimer;

        private IOptionsChannelPanel Panel(int tab) => (IOptionsChannelPanel)this.tabs[tab].panel;

        [Inject]
        public void Construct(IInputService inputService, InventorySfxData sfx)
        {
            this.inputService = inputService;
            this.sfx          = sfx;
            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIConfirm.performed  += OnConfirm;
        }

        private void OnDestroy()
        {
            if (this.inputService == null) return;
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIConfirm.performed  -= OnConfirm;
        }

        private void Awake()
        {
            // Each tab's underline Image stays active only as an invisible anchor the bar follows.
            foreach (var tab in this.tabs)
            {
                tab.buttonImage.SetActive(true);
                var image = tab.buttonImage.GetComponent<Image>();
                if (image != null) image.enabled = false;
            }
        }

        private void RefreshTabBar()
        {
            if (this.tabBar == null) return;
            this.tabBar.Follow((RectTransform)this.tabs[this.activeTab].buttonImage.transform);
            this.tabBar.SetConfirmed(this.isOpen && this.onTabRow);
        }

        public void Open()
        {
            this.isOpen                  = true;
            this.onTabRow                = true;
            this.heldHorizontalDirection = 0;
            ShowTab(0);
            this.tabBar?.Snap();
        }

        public void Close()
        {
            this.isOpen                  = false;
            this.heldHorizontalDirection = 0;
            for (int i = 0; i < this.tabs.Length; i++)
                Panel(i).HideOutlines();
            RefreshTabBar();
        }

        private void Update()
        {
            if (!this.isOpen || this.onTabRow || this.heldHorizontalDirection == 0) return;
            if (!Panel(this.activeTab).RepeatsOnHold(this.contentIndex))
            {
                this.heldHorizontalDirection = 0;
                return;
            }

            float x = this.inputService.UINavigate.ReadValue<Vector2>().x;
            if (Mathf.Abs(x) < 0.5f || (int)Mathf.Sign(x) != this.heldHorizontalDirection)
            {
                this.heldHorizontalDirection = 0;
                return;
            }

            this.horizontalRepeatTimer -= Time.unscaledDeltaTime;
            if (this.horizontalRepeatTimer > 0f) return;

            Panel(this.activeTab).Adjust(this.contentIndex, this.heldHorizontalDirection);
            this.horizontalRepeatTimer = this.repeatInterval;
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isOpen) return;

            var direction = ctx.ReadValue<Vector2>();

            int verticalDelta = direction.y > 0.5f ? -1 : direction.y < -0.5f ? 1 : 0;
            if (verticalDelta != 0)
            {
                HandleVertical(verticalDelta);
                return;
            }

            int horizontalDelta = direction.x > 0.5f ? 1 : direction.x < -0.5f ? -1 : 0;
            if (horizontalDelta != 0)
                HandleHorizontal(horizontalDelta);
            else
                this.heldHorizontalDirection = 0;
        }

        // delta: +1 = down, -1 = up.
        private void HandleVertical(int delta)
        {
            this.heldHorizontalDirection = 0;
            var panel = Panel(this.activeTab);

            if (this.onTabRow)
            {
                if (delta < 0) return;
                int first = NextSelectable(panel, -1, 1);
                if (first < 0) return;
                this.onTabRow     = false;
                this.contentIndex = first;
                this.sfx.PlayCursor(gameObject);
                panel.ShowOutline(this.contentIndex);
                RefreshTabBar();
                return;
            }

            int next = NextSelectable(panel, this.contentIndex, delta);
            if (next >= 0)
            {
                this.contentIndex = next;
                this.sfx.PlayCursor(gameObject);
                panel.ShowOutline(this.contentIndex);
            }
            else if (delta < 0)
            {
                panel.HideOutlines();
                this.onTabRow = true;
                this.sfx.PlayCursor(gameObject);
                RefreshTabBar();
            }
        }

        // Next selectable channel from `from` stepping by `delta`, without wrapping; -1 if none.
        private static int NextSelectable(IOptionsChannelPanel panel, int from, int delta)
        {
            for (int i = from + delta; i >= 0 && i < panel.ChannelCount; i += delta)
                if (panel.IsSelectable(i)) return i;
            return -1;
        }

        private void HandleHorizontal(int delta)
        {
            if (this.onTabRow)
            {
                int next = Mathf.Clamp(this.activeTab + delta, 0, this.tabs.Length - 1);
                if (next == this.activeTab) return;
                this.sfx.PlayCursor(gameObject);
                ShowTab(next);
                return;
            }

            Panel(this.activeTab).Adjust(this.contentIndex, delta);
            this.heldHorizontalDirection = delta;
            this.horizontalRepeatTimer   = this.initialRepeatDelay;
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (!this.isOpen || this.onTabRow) return;
            Panel(this.activeTab).Confirm(this.contentIndex);
        }

        private void ShowTab(int tab)
        {
            this.activeTab = tab;
            for (int i = 0; i < this.tabs.Length; i++)
            {
                bool active = i == tab;
                this.tabs[i].group.SetActive(active);
                if (!active) Panel(i).HideOutlines();
            }
            RefreshTabBar();
            ActiveTabChanged?.Invoke(tab);
        }
    }
}
