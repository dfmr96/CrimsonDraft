#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Graphics;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.UI
{
    public sealed class InventoryOpenCloseController : MonoBehaviour, IInitializable, System.IDisposable
    {
        [SerializeField] private GameObject canvasRoot      = null!;
        [SerializeField] private Volume?    inventoryVolume;
        [SerializeField] private float      volumeFadeDuration = 0.3f;
        [SerializeField] private ScriptableRendererFeature? ditherFeature;

        [Inject] private IInputService     inputService  = null!;
        [Inject] private GridCursor        cursor        = null!;
        [Inject] private PartyPanelView    partyPanel    = null!;
        [Inject] private InventorySceneInit sceneInit    = null!;
        [Inject] private TabManager        tabManager    = null!;
        [Inject] private InventorySfxData  sfxData       = null!;
        [Inject] private IGraphicsSettingsService graphicsSettings = null!;

        private const string MapTabName = "Map";

        public void Initialize()
        {
            this.inputService.OpenInventory.performed += Open;
            this.inputService.InventoryClose.performed += OnCloseKey;
            this.inputService.OpenMap.performed        += OnToggleMap;
            this.inputService.InventoryCloseMap.performed += OnToggleMap;
            this.cursor.OnCloseRequested                += Close;
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
            Time.timeScale = 0f;
            this.inputService.SwitchToInventory();
            this.canvasRoot.SetActive(true);

            // Must run before EnsureSynced(): it activates the starting tab's root, which is
            // what makes GridCursor's Awake() fire (its GameObject can still be inactive right
            // after canvasRoot.SetActive() if the starting tab isn't a direct always-active
            // child). Without this, EnsureSynced()'s FindView() calls NRE on GridCursor's
            // not-yet-initialized gridGroup reference the first time Open() ever runs.
            this.tabManager.EnsureInitialized();

            this.sceneInit.EnsureSynced();
            this.partyPanel.Refresh();
            this.sfxData.PlayDecide(this.gameObject);
            FadeVolume(1f);
            this.ditherFeature?.SetActive(false);
            this.graphicsSettings.PushGammaSuppression();
        }

        public void Close()
        {
            this.cursor.CancelAll();
            this.canvasRoot.SetActive(false);
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
            this.sfxData.PlayCancel(this.gameObject);
            FadeVolume(0f);
            this.ditherFeature?.SetActive(true);
            this.graphicsSettings.PopGammaSuppression();
        }

        private void FadeVolume(float target) => VolumeFader.Fade(this.inventoryVolume, target > 0f, this.volumeFadeDuration);

        public void Dispose()
        {
            this.inputService.OpenInventory.performed -= Open;
            this.inputService.InventoryClose.performed -= OnCloseKey;
            this.inputService.OpenMap.performed        -= OnToggleMap;
            this.inputService.InventoryCloseMap.performed -= OnToggleMap;
            this.cursor.OnCloseRequested                -= Close;
        }
    }
}
