#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI
{
    // Added at runtime to the selector's Animator GameObject so its clip's Animation Event
    // can reach CombatInventoryPanelController.
    public sealed class SelectorAnimationRelay : MonoBehaviour
    {
        private CombatInventoryPanelController controller = null!;

        internal void Init(CombatInventoryPanelController controller)
            => this.controller = controller;

        public void OnUseAnimationComplete() => this.controller.OnSelectorAnimationComplete();
    }
}
