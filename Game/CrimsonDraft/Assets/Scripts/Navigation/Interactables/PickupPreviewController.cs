#nullable enable

using UnityEngine.Scripting;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PickupPreviewController
    {
        private readonly PickupPreviewView view;

        [Preserve]
        public PickupPreviewController(PickupPreviewView view)
        {
            this.view = view;
        }

        public void Show(ItemData item) => this.view.Show(item);

        public void Hide() => this.view.Hide();
    }
}
