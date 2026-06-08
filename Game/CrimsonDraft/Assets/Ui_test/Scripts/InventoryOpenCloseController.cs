#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.UI
{
    public sealed class InventoryOpenCloseController : MonoBehaviour, IInitializable, System.IDisposable
    {
        [SerializeField] private GameObject canvasRoot = null!;

        [Inject] private IInputService     inputService  = null!;
        [Inject] private GridCursor        cursor        = null!;
        [Inject] private PartyPanelView    partyPanel    = null!;
        [Inject] private InventorySceneInit sceneInit    = null!;

        public void Initialize()
        {
            this.inputService.OpenInventory.performed += Open;
            this.cursor.OnCloseRequested              += Close;
        }

        private void Open(InputAction.CallbackContext _)
        {
            Time.timeScale = 0f;
            this.inputService.SwitchToInventory();
            this.canvasRoot.SetActive(true);
            this.sceneInit.EnsureSynced();
            this.partyPanel.Refresh();
        }

        public void Close()
        {
            this.cursor.CancelAll();
            this.canvasRoot.SetActive(false);
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
        }

        public void Dispose()
        {
            this.inputService.OpenInventory.performed -= Open;
            this.cursor.OnCloseRequested              -= Close;
        }
    }
}
