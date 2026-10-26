#nullable enable

using System;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Graphics;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.UI;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Radio;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.UI
{
    public sealed class InventoryOpenCloseController : MonoBehaviour, IInitializable, System.IDisposable
    {
        [SerializeField] private GameObject canvasRoot      = null!;
        [SerializeField] private Volume?    inventoryVolume;
        [SerializeField] private float      volumeFadeDuration = 0.3f;
        [Tooltip("Seconds for the inventory to scale vertically (Y: 0 -> its scene scale) on open and back on close.")]
        [SerializeField] private float      scaleDuration      = 0.25f;
        [SerializeField] private ScriptableRendererFeature? ditherFeature;
        [SerializeField] private GameObject?    tabBarRoot;
        [SerializeField] private StorageWindow? storageWindow;
        [Tooltip("El PDA que se despliega antes de que aparezca la UI (y se pliega despues de que se va). Opcional.")]
        [SerializeField] private PdaDevice? pda;

        [Inject] private ISubscriber<StorageOpenRequestedEvent> storageOpenSubscriber = null!;

        private IDisposable? storageSubscription;
        private bool         storageMode;
        private bool         pickupMode;
        private bool         isClosing;
        private bool         pdaOpening; // the PDA is unfolding; the UI itself is not up yet

        // Raised once the close animation has finished and input is back on Gameplay.
        public event Action? Closed;
        private Vector3      openScale;

        [Inject] private IInputService     inputService  = null!;
        [Inject] private GridCursor        cursor        = null!;
        [Inject] private PartyPanelView    partyPanel    = null!;
        [Inject] private TabManager        tabManager    = null!;
        [Inject] private InventorySfxData  sfxData       = null!;
        [Inject] private IGraphicsSettingsService graphicsSettings = null!;
        [Inject] private IObjectResolver          resolver         = null!;

        // Radio calls are answered from here. Resolved optionally: not every scope has the radio.
        private IRadioConversationService? radio;

        private const string MapTabName = "Map";

        // The scale authored in the scene (e.g. 0.33) is the 'open' scale; Open/Close animate to and
        // from zero, so remember it before anything touches the transform.
        private void Awake() => this.openScale = this.transform.localScale;

        public void Initialize()
        {
            // The renderer feature is a ScriptableObject asset: toggling it at runtime also changes
            // the asset in the Editor and survives exiting Play Mode. Always start from the closed
            // state (feature on, same as Close()) so a previous session left open can't leak in.
            this.ditherFeature?.SetActive(true);
            this.resolver.TryResolve(out this.radio);
            this.inputService.OpenInventory.performed += Open;
            this.inputService.InventoryClose.performed += OnCloseKey;
            this.inputService.OpenMap.performed        += OnToggleMap;
            this.inputService.InventoryCloseMap.performed += OnToggleMap;
            this.cursor.OnCloseRequested                += Close;
            this.storageSubscription = this.storageOpenSubscriber.Subscribe(_ => OpenStorage());
        }

        private void Open(InputAction.CallbackContext _) => Open();

        // OpenInventory lives in the Gameplay action map (fires when closed) and InventoryClose
        // in the Inventory map (fires when open) — same physical key (Z), different map
        // depending on current mode, so both route through the same close path as tab-bar back.
        private void OnCloseKey(InputAction.CallbackContext _) => this.cursor.RequestClose();

        // OpenMap lives in the Gameplay action map (fires when closed) and InventoryCloseMap
        // in the Inventory map (fires when open) — same physical key (A), different map
        // depending on current mode, so both route to the same toggle.
        private void OnToggleMap(InputAction.CallbackContext _)
        {
            if (this.isClosing) return;

            if (this.canvasRoot.activeSelf)
            {
                Close();
                return;
            }

            Open(() =>
            {
                this.tabManager.EnsureInitialized();
                this.tabManager.ActivateTabByName(MapTabName);
            });
        }

        public bool IsOpen => this.canvasRoot.activeSelf;

        /// <summary>
        /// Opens the inventory as the backdrop of a world pickup's "take it?" prompt: the item grids
        /// only, no tab bar and no cursor, and input stays with the prompt (which owns it) instead
        /// of moving to the inventory. Close it with <see cref="ClosePickup"/>.
        /// </summary>
        public void OpenForPickup()
        {
            if (this.canvasRoot.activeSelf) return;

            this.pickupMode = true;
            Open();
            this.tabManager.ActivateTab(0);
            if (this.tabBarRoot != null) this.tabBarRoot.SetActive(false);
            this.cursor.HideSelectorForTabBar();
        }

        public void ClosePickup()
        {
            if (this.pickupMode) Close();
        }

        public void Open() => Open(null);

        /// <summary>
        /// Opens the inventory. With a PDA the UI only comes up once the device has unfolded, so anything
        /// that has to happen on the open UI (switching tab, opening a note...) goes in
        /// <paramref name="afterShown"/>, which runs right after the UI appears (immediately without a PDA).
        /// </summary>
        public void Open(Action? afterShown)
        {
            if (this.pdaOpening) return;

            // Already up (and not on its way out): nothing to open again -- just run what the caller wanted
            // on the open UI. This is how a note found through a pickup takes over the inventory that was only
            // opened as that pickup's backdrop: it becomes a normal, fully usable inventory instead of closing
            // behind the note.
            if (this.canvasRoot.activeSelf && !this.isClosing)
            {
                if (this.pickupMode)
                {
                    this.pickupMode = false;
                    if (this.tabBarRoot != null) this.tabBarRoot.SetActive(true);
                    this.inputService.SwitchToInventory();
                }
                afterShown?.Invoke();
                return;
            }

            DOTween.Kill(this);
            this.isClosing = false;
            ExitStorageMode(); // reopened while a storage close was still animating
            Time.timeScale = 0f;
            if (!this.pickupMode) this.inputService.SwitchToInventory();

            // The pickup prompt is not the player turning the device on: no PDA for it.
            if (this.pda != null && !this.pickupMode)
            {
                this.pdaOpening = true;
                this.pda.Show(() =>
                {
                    this.pdaOpening = false;
                    ShowCanvas();
                    afterShown?.Invoke();
                });
                return;
            }

            ShowCanvas();
            afterShown?.Invoke();
        }

        private void ShowCanvas()
        {
            this.transform.localScale = new Vector3(this.openScale.x, 0f, this.openScale.z);
            UiCameraGate.Request(this);
            this.canvasRoot.SetActive(true);

            // Activates the starting tab's root so GridCursor's lifecycle runs before it is used
            // the first time Open() runs.
            this.tabManager.EnsureInitialized();

            this.partyPanel.Refresh();
            if (!this.pickupMode) this.sfxData.PlayDecide(this.gameObject); // the prompt has its own sounds
            FadeVolume(1f);
            this.ditherFeature?.SetActive(false);
            this.graphicsSettings.PushGammaSuppression();

            // Unscaled: the inventory pauses the game (timeScale 0).
            this.transform.DOScaleY(this.openScale.y, this.scaleDuration)
                .SetTarget(this)
                .SetUpdate(true)
                .SetEase(Ease.OutCubic);

            if (!this.pickupMode) this.radio?.NotifyInventoryOpened();
        }

        public void OpenStorage()
        {
            if (this.canvasRoot.activeSelf || this.storageWindow == null || !this.cursor.CanEnterTransferMode) return;

            Open(() =>
            {
                this.tabManager.ActivateTab(0);
                if (this.tabBarRoot != null) this.tabBarRoot.SetActive(false);
                this.storageWindow.Show();
                this.cursor.EnterTransferMode();
                this.storageMode = true;
            });
        }

        public void Close()
        {
            if (this.isClosing || this.pdaOpening) return;
            this.isClosing = true;

            this.cursor.CancelAll();
            if (this.pickupMode && this.tabBarRoot != null) this.tabBarRoot.SetActive(true);
            if (!this.pickupMode) this.sfxData.PlayCancel(this.gameObject);
            FadeVolume(0f);

            // Scale back out first; the game stays paused (enemies can't move under the closing
            // UI) and everything else switches back once it has finished.
            DOTween.Kill(this);
            this.transform.DOScaleY(0f, this.scaleDuration)
                .SetTarget(this)
                .SetUpdate(true)
                .SetEase(Ease.InCubic)
                .OnComplete(FinishClose);
        }

        // The storage window covers the party cards: tearing it down before the scale-out has finished
        // shows the cards for the length of the close animation.
        private void ExitStorageMode()
        {
            if (!this.storageMode) return;
            this.storageMode = false;
            this.cursor.ExitTransferMode();
            if (this.storageWindow != null) this.storageWindow.Hide();
            if (this.tabBarRoot != null) this.tabBarRoot.SetActive(true);
        }

        private void FinishClose()
        {
            ExitStorageMode();
            // Still scaled to zero here, so the cursor coming back is never seen.
            if (this.pickupMode) this.cursor.ShowSelectorAfterTabBar();
            this.canvasRoot.SetActive(false);
            UiCameraGate.Release(this);
            this.transform.localScale = this.openScale;

            // The UI is gone; now the PDA folds away, and only then does the game carry on.
            if (this.pda != null && !this.pickupMode) this.pda.Hide(FinishCloseAfterPda);
            else                                       FinishCloseAfterPda();
        }

        private void FinishCloseAfterPda()
        {
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
            this.ditherFeature?.SetActive(true);
            this.graphicsSettings.PopGammaSuppression();
            this.isClosing = false;
            if (!this.pickupMode) this.radio?.NotifyInventoryClosed();
            this.pickupMode = false;
            Closed?.Invoke();
        }

        private void FadeVolume(float target) => VolumeFader.Fade(this.inventoryVolume, target > 0f, this.volumeFadeDuration);

        // Exiting Play Mode with the inventory still open never reaches Close(), so restore the
        // shared renderer feature here instead of leaving the asset modified.
        private void OnDestroy()
        {
            DOTween.Kill(this);
            UiCameraGate.Release(this);
            if (this.isClosing) Time.timeScale = 1f;
            this.ditherFeature?.SetActive(true);
        }

        public void Dispose()
        {
            this.storageSubscription?.Dispose();
            this.inputService.OpenInventory.performed -= Open;
            this.inputService.InventoryClose.performed -= OnCloseKey;
            this.inputService.OpenMap.performed        -= OnToggleMap;
            this.inputService.InventoryCloseMap.performed -= OnToggleMap;
            this.cursor.OnCloseRequested                -= Close;
        }
    }
}
