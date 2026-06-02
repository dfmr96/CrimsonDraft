#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.UI
{
    /// <summary>
    /// Activa el mapa de input Inventory al iniciar la escena de test.
    /// En la escena de producción este trabajo lo hace el InventoryController via InputService.
    /// </summary>
    public class InventorySceneInit : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions = null!;

        [Inject] private IInputService? inputService;

        void Start()
        {
            if (this.inputService != null)
            {
                this.inputService.SwitchToInventory();
            }
            else if (this.inputActions != null)
            {
                // Fallback: activa el mapa directamente sin InputService (sin VContainer)
                foreach (var map in this.inputActions.actionMaps)
                    map.Disable();
                this.inputActions.FindActionMap("Inventory")?.Enable();
            }
        }

        void OnDestroy()
        {
            if (this.inputService == null)
                this.inputActions?.FindActionMap("Inventory")?.Disable();
        }
    }
}
