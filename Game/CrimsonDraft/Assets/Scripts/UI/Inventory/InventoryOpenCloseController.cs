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
using CrimsonDraft.Navigation;
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

        [Inject] private ISubscriber<StorageOpenRequestedEvent> storageOpenSubscriber = null!;

        private IDisposable? storageSubscription;
        private bool         storageMode;
        private bool         isClosing;
        private Vector3      openScale;

        [Inject] private IInputService     inputService  = null!;
        [Inject] private GridCursor        cursor        = null!;
        [Inject] private PartyPanelView    partyPanel    = null!;
        [Inject] private TabManager        tabManager    = null!;
        [Inject] private InventorySfxData  sfxData       = null!;
        [Inject] private IGraphicsSettingsService graphicsSettings = null!;

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

            Open();
            this.tabManager.EnsureInitialized();
            this.tabManager.ActivateTabByName(MapTabName);
        }

        public void Open()
        {
            DOTween.Kill(this);
            this.isClosing = false;
            Time.timeScale = 0f;
            this.inputService.SwitchToInventory();
            this.transform.localScale = new Vector3(this.openScale.x, 0f, this.openScale.z);
            this.canvasRoot.SetActive(true);

            // Activates the starting tab's root so GridCursor's lifecycle runs before it is used
            // the first time Open() runs.
            this.tabManager.EnsureInitialized();

            this.partyPanel.Refresh();
            this.sfxData.PlayDecide(this.gameObject);
            FadeVolume(1f);
            this.ditherFeature?.SetActive(false);
            this.graphicsSettings.PushGammaSuppression();

            // Unscaled: the inventory pauses the game (timeScale 0).
            this.transform.DOScaleY(this.openScale.y, this.scaleDuration)
                .SetTarget(this)
                .SetUpdate(true)
                .SetEase(Ease.OutCubic);
        }

        public void OpenStorage()
        {
            if (this.canvasRoot.activeSelf || this.storageWindow == null || !this.cursor.CanEnterTransferMode) return;

            Open();
            this.tabManager.ActivateTab(0);
            if (this.tabBarRoot != null) this.tabBarRoot.SetActive(false);
            this.storageWindow.Show();
            this.cursor.EnterTransferMode();
            this.storageMode = true;
        }

        public void Close()
        {
            if (this.isClosing) return;
            this.isClosing = true;

            this.cursor.CancelAll();
            if (this.storageMode)
            {
                this.storageMode = false;
                this.cursor.ExitTransferMode();
                if (this.storageWindow != null) this.storageWindow.Hide();
                if (this.tabBarRoot != null) this.tabBarRoot.SetActive(true);
            }
            this.sfxData.PlayCancel(this.gameObject);
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

        private void FinishClose()
        {
            this.canvasRoot.SetActive(false);
            this.transform.localScale = this.openScale;
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
            this.ditherFeature?.SetActive(true);
            this.graphicsSettings.PopGammaSuppression();
            this.isClosing = false;
        }

        private void FadeVolume(float target) => VolumeFader.Fade(this.inventoryVolume, target > 0f, this.volumeFadeDuration);

        // Exiting Play Mode with the inventory still open never reaches Close(), so restore the
        // shared renderer feature here instead of leaving the asset modified.
        private void OnDestroy()
        {
            DOTween.Kill(this);
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
