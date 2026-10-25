#nullable enable

using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.UI;
using CrimsonDraft.Navigation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// Navigation for the main menu's Options screen, in two levels. Level 1 is a vertical list of
    /// three buttons (Video / Control / Sound) moved with the EventSystem selection. Confirming one
    /// hides the buttons and shows the Options panel with only that tab's content; there Up/Down
    /// moves between channels and Left/Right/Confirm go to the tab's <see cref="IOptionsChannelPanel"/>.
    /// Cancel from the content returns to the buttons (see <see cref="TryGoBack"/>), Cancel from the
    /// buttons is left to <see cref="MainMenuPanelNavigator"/>, which leaves Options.
    ///
    /// The pause menu keeps using <see cref="OptionsTabController"/> (tab row + content together).
    /// </summary>
    public sealed class OptionsMenuNavigator : MonoBehaviour
    {
        [System.Serializable]
        private sealed class Tab
        {
            [SerializeField] public Button        button = null!;
            [SerializeField] public GameObject    group  = null!;
            [SerializeField] public MonoBehaviour panel  = null!;
        }

        [Header("Tabs (0 = Video, 1 = Control, 2 = Sound)")]
        [SerializeField] private Tab[] tabs = null!;

        [Header("Levels")]
        [Tooltip("Contenedor de los 3 botones (SETTINGS/Buttons).")]
        [SerializeField] private GameObject buttonsRoot = null!;
        [Tooltip("Options_panel: se prende solo mientras hay una pestaña abierta.")]
        [SerializeField] private GameObject contentRoot = null!;

        [Tooltip("Glitch de CRT (el mismo del Bio del inventario) que refresca la vista al pasar de los botones al contenido de una pestaña y al volver. Opcional.")]
        [SerializeField] private CardSwitchGlitch? glitch;
        [Tooltip("Tira de seleccion del contenido de las pestañas (Options_panel). Opcional.")]
        [SerializeField] private SelectionCursor? selector;

        [Header("Hold-to-repeat (Left/Right on a channel)")]
        [SerializeField] private float initialRepeatDelay = 0.35f;
        [SerializeField] private float repeatInterval     = 0.08f;

        private IInputService    inputService = null!;
        private InventorySfxData sfx          = null!;

        private bool isOpen;
        private bool inContent;
        private int  activeTab;
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

        private void Awake()
        {
            for (int i = 0; i < this.tabs.Length; i++)
            {
                int tab = i;
                this.tabs[i].button.onClick.AddListener(() => OnTabButtonPressed(tab));
            }
        }

        private void OnDestroy()
        {
            if (this.inputService == null) return;
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIConfirm.performed  -= OnConfirm;
        }

        /// <summary>Shows level 1 (the three buttons) with the first one selected.</summary>
        public void Open()
        {
            this.isOpen    = true;
            this.inContent = false;
            HideAllOutlines();
            this.selector?.SetVisible(false);
            ShowButtonsNow(0);
        }

        // The view is being refreshed (glitch): the old content is still on screen until the peak.
        private bool Switching => this.glitch != null && this.glitch.IsPlaying;

        public void Close()
        {
            this.isOpen                  = false;
            this.inContent               = false;
            this.heldHorizontalDirection = 0;
            HideAllOutlines();
            this.selector?.SetVisible(false);
        }

        /// <summary>Cancel pressed while Options is open. Returns true if it only went back from a
        /// tab's content to the buttons, false if the caller should leave Options.</summary>
        public bool TryGoBack()
        {
            if (!this.isOpen || !this.inContent) return false;
            if (this.Switching) return true; // already going back / forth: swallow this Cancel

            this.sfx.PlayCancel(gameObject);
            this.heldHorizontalDirection = 0;
            ShowButtons(this.activeTab);
            return true;
        }

        private void ShowButtons(int selectedTab)
        {
            this.inContent = false;
            HideAllOutlines();
            this.selector?.SetVisible(false);

            // Coming back from a tab's content: the view refreshes (glitch) and swaps at its peak.
            if (this.glitch != null && this.contentRoot.activeSelf)
            {
                this.glitch.Play(() =>
                {
                    if (this.isOpen && !this.inContent) ShowButtonsNow(selectedTab);
                });
                return;
            }
            ShowButtonsNow(selectedTab);
        }

        private void ShowButtonsNow(int selectedTab)
        {
            this.contentRoot.SetActive(false);
            this.buttonsRoot.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(this.tabs[selectedTab].button.gameObject);
        }

        private void OnTabButtonPressed(int tab)
        {
            if (!this.isOpen || this.inContent || this.Switching) return;
            this.sfx.PlayDecide(gameObject);

            // Same reason as MainMenuPanelNavigator: don't let this Confirm also drive the content.
            DeferredInputAction.Run(() =>
            {
                if (this.isOpen && !this.inContent) ShowContent(tab);
            });
        }

        private void ShowContent(int tab)
        {
            this.activeTab = tab;
            this.inContent = true;

            EventSystem.current?.SetSelectedGameObject(null);
            this.selector?.SetVisible(false);

            // The buttons view refreshes (glitch) and the tab's content takes over at its peak.
            if (this.glitch != null)
            {
                this.glitch.Play(() =>
                {
                    if (this.isOpen && this.inContent) ShowContentNow(tab);
                });
                return;
            }
            ShowContentNow(tab);
        }

        private void ShowContentNow(int tab)
        {
            this.buttonsRoot.SetActive(false);
            this.contentRoot.SetActive(true);
            for (int i = 0; i < this.tabs.Length; i++)
                this.tabs[i].group.SetActive(i == tab);

            var panel = Panel(tab);
            this.contentIndex = NextSelectable(panel, -1, 1);
            if (this.contentIndex >= 0)
            {
                panel.ShowOutline(this.contentIndex);
                this.selector?.FollowOrHide(panel.GetSelectorAnchor(this.contentIndex), snap: true);
            }
        }

        // The anchor can change without the channel changing (a Left/Right inside a Control row).
        private void RefreshSelector() =>
            this.selector?.FollowOrHide(Panel(this.activeTab).GetSelectorAnchor(this.contentIndex));

        private void HideAllOutlines()
        {
            // A tab that was never shown hasn't run Awake yet, so there is nothing to hide.
            for (int i = 0; i < this.tabs.Length; i++)
                if (this.tabs[i].group.activeInHierarchy) Panel(i).HideOutlines();
        }

        private void Update()
        {
            if (!this.isOpen || !this.inContent || this.heldHorizontalDirection == 0) return;
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

        // Level 1 needs no handling here: the buttons are moved by the EventSystem selection.
        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isOpen || !this.inContent || this.Switching || this.contentIndex < 0) return;

            var direction = ctx.ReadValue<Vector2>();
            var panel     = Panel(this.activeTab);

            int verticalDelta = direction.y > 0.5f ? -1 : direction.y < -0.5f ? 1 : 0;
            if (verticalDelta != 0)
            {
                this.heldHorizontalDirection = 0;
                int next = NextSelectable(panel, this.contentIndex, verticalDelta);
                if (next < 0) return;
                this.contentIndex = next;
                this.sfx.PlayCursor(gameObject);
                panel.ShowOutline(this.contentIndex);
                RefreshSelector();
                return;
            }

            int horizontalDelta = direction.x > 0.5f ? 1 : direction.x < -0.5f ? -1 : 0;
            if (horizontalDelta == 0)
            {
                this.heldHorizontalDirection = 0;
                return;
            }

            panel.Adjust(this.contentIndex, horizontalDelta);
            RefreshSelector();
            this.heldHorizontalDirection = horizontalDelta;
            this.horizontalRepeatTimer   = this.initialRepeatDelay;
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (!this.isOpen || !this.inContent || this.Switching || this.contentIndex < 0) return;
            Panel(this.activeTab).Confirm(this.contentIndex);
            this.selector?.Flash();
        }

        // Next selectable channel from `from` stepping by `delta`, without wrapping; -1 if none.
        private static int NextSelectable(IOptionsChannelPanel panel, int from, int delta)
        {
            for (int i = from + delta; i >= 0 && i < panel.ChannelCount; i += delta)
                if (panel.IsSelectable(i)) return i;
            return -1;
        }
    }
}
