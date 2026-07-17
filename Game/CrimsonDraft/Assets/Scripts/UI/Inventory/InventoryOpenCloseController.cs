#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.UI
{
    public sealed class InventoryOpenCloseController : MonoBehaviour, IInitializable, System.IDisposable
    {
        [SerializeField] private GameObject canvasRoot      = null!;
        [SerializeField] private Volume?    inventoryVolume;
        [SerializeField] private float      volumeFadeDuration = 0.3f;
        [SerializeField] private ScriptableRendererFeature? pixelationFeature;
        [SerializeField] private ScriptableRendererFeature? ditherFeature;
        [SerializeField] private ScriptableRendererFeature? crtFeature;

        [Inject] private IInputService     inputService  = null!;
        [Inject] private GridCursor        cursor        = null!;
        [Inject] private PartyPanelView    partyPanel    = null!;
        [Inject] private InventorySceneInit sceneInit    = null!;
        [Inject] private TabManager        tabManager    = null!;
        [Inject] private InventorySfxData  sfxData       = null!;

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
            this.sceneInit.EnsureSynced();
            this.partyPanel.Refresh();
            this.sfxData.PlayDecide(this.gameObject);
            FadeVolume(1f);
            this.pixelationFeature?.SetActive(false);
            this.ditherFeature?.SetActive(false);
            this.crtFeature?.SetActive(true);
        }

        public void Close()
        {
            this.cursor.CancelAll();
            this.canvasRoot.SetActive(false);
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
            this.sfxData.PlayCancel(this.gameObject);
            FadeVolume(0f);
            this.pixelationFeature?.SetActive(true);
            this.ditherFeature?.SetActive(true);
            this.crtFeature?.SetActive(false);
        }

        private void FadeVolume(float target)
        {
            if (this.inventoryVolume == null) return;
            if (target > 0f) this.inventoryVolume.gameObject.SetActive(true);
            DOTween.Kill(this.inventoryVolume);
            DOTween.To(
                    () => this.inventoryVolume.weight,
                    x  => this.inventoryVolume.weight = x,
                    target,
                    this.volumeFadeDuration)
                .SetTarget(this.inventoryVolume)
                .SetUpdate(true)
                .SetEase(Ease.InOutSine)
                .OnComplete(() =>
                {
                    if (target <= 0f) this.inventoryVolume.gameObject.SetActive(false);
                });
        }

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
