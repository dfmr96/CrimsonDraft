#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
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

        [Inject] private IInputService     inputService  = null!;
        [Inject] private GridCursor        cursor        = null!;
        [Inject] private PartyPanelView    partyPanel    = null!;
        [Inject] private InventorySceneInit sceneInit    = null!;

        public void Initialize()
        {
            this.inputService.OpenInventory.performed += Open;
            this.cursor.OnCloseRequested              += Close;
        }

        private void Open(InputAction.CallbackContext _) => Open();

        public void Open()
        {
            Time.timeScale = 0f;
            this.inputService.SwitchToInventory();
            this.canvasRoot.SetActive(true);
            this.sceneInit.EnsureSynced();
            this.partyPanel.Refresh();
            FadeVolume(1f);
        }

        public void Close()
        {
            this.cursor.CancelAll();
            this.canvasRoot.SetActive(false);
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
            FadeVolume(0f);
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
            this.cursor.OnCloseRequested              -= Close;
        }
    }
}
