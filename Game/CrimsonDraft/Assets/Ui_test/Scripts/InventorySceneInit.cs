#nullable enable

using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.UI
{
    public class InventorySceneInit : MonoBehaviour
    {
        [Inject] private IInputService inputService = null!;

        void Start() => this.inputService.SwitchToInventory();
    }
}
