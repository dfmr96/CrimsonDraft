#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI
{
    public sealed class StorageWindow : MonoBehaviour
    {
        [SerializeField] private InventoryGrid grid = null!;

        public InventoryGrid Grid    => this.grid;
        public bool          IsShown => this.gameObject.activeSelf;

        public void Show() => this.gameObject.SetActive(true);
        public void Hide() => this.gameObject.SetActive(false);
    }
}
