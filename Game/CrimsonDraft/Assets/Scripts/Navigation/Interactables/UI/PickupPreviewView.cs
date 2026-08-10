#nullable enable

using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public sealed class PickupPreviewView : MonoBehaviour
    {
        [SerializeField] private GameObject  root            = null!;
        [SerializeField] private Transform   mountPoint       = null!;
        [SerializeField] private float       rotationSpeed    = 60f;
        [SerializeField] private string      previewLayerName = "ItemPreview";

        [Header("Inventory Footprint")]
        [SerializeField] private RectTransform? gridRect;
        [SerializeField] private RectTransform? highlightRect;
        [SerializeField] private float          gridCellSize = 40f;
        [SerializeField] private int            gridColumns  = 4;
        [SerializeField] private int            gridRows     = 4;

        private GameObject? currentInstance;
        private int         previewLayer = -1;

        void Awake()
        {
            this.previewLayer = LayerMask.NameToLayer(this.previewLayerName);
            this.root.SetActive(false);

            if (this.gridRect != null)
                this.gridRect.sizeDelta = new Vector2(
                    this.gridColumns * this.gridCellSize,
                    this.gridRows    * this.gridCellSize);

            // Some root Canvases in this project keep a zero Transform scale (harmless for
            // Overlay UI rendering, which ignores it) -- but it collapses any non-RectTransform
            // descendant, like this preview camera, to zero size/offset. Detach the camera rig
            // to the scene root so it always renders at its authored world-space position.
            this.mountPoint.parent.SetParent(null, worldPositionStays: false);
        }

        void Update()
        {
            if (this.currentInstance == null) return;
            this.currentInstance.transform.Rotate(Vector3.up, this.rotationSpeed * Time.deltaTime, Space.World);
        }

        public void Show(ItemData item)
        {
            ClearInstance();

            // Highlight the item's footprint from the grid's top-left cell (0,0),
            // same convention as InventoryGrid.CellToLocal for a center-pivoted grid.
            if (this.highlightRect != null)
            {
                this.highlightRect.sizeDelta = new Vector2(
                    item.GridSize.x * this.gridCellSize,
                    item.GridSize.y * this.gridCellSize);
                this.highlightRect.anchoredPosition = new Vector2(
                    -this.gridColumns * this.gridCellSize * 0.5f,
                     this.gridRows    * this.gridCellSize * 0.5f);
            }

            var modelPrefab = item.PreviewModel;

            // No model assigned on the ItemData yet -- stay hidden instead of showing an empty panel.
            if (modelPrefab == null)
            {
                this.root.SetActive(false);
                return;
            }

            this.currentInstance = Instantiate(modelPrefab, this.mountPoint.position, this.mountPoint.rotation, this.mountPoint);
            if (this.previewLayer >= 0)
                SetLayerRecursively(this.currentInstance.transform, this.previewLayer);

            this.root.SetActive(true);
        }

        public void Hide()
        {
            ClearInstance();
            this.root.SetActive(false);
        }

        private void ClearInstance()
        {
            if (this.currentInstance == null) return;
            Destroy(this.currentInstance);
            this.currentInstance = null;
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i), layer);
        }
    }
}
