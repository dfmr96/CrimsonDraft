#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public sealed class PickupPreviewView : MonoBehaviour
    {
        [SerializeField] private GameObject  root            = null!;
        [SerializeField] private Transform   mountPoint       = null!;
        [SerializeField] private float       rotationSpeed    = 60f;
        [SerializeField] private string      previewLayerName = "ItemPreview";

        // Optional -- only the standalone world pickup-preview panel wires this (InspectPanel's
        // own PickupPreviewView instance leaves it unassigned, since that one only ever shows
        // while the full inventory is already open and InventoryOpenCloseController has already
        // faded the same Volume in). See InventoryOpenCloseController.FadeVolume for the
        // matching implementation this mirrors.
        [SerializeField] private Volume? inventoryVolume;
        [SerializeField] private float   volumeFadeDuration = 0.3f;

        [Header("Inventory Footprint")]
        [SerializeField] private RectTransform? gridRect;
        [SerializeField] private RectTransform? highlightRect;
        [SerializeField] private float          gridCellSize = 40f;
        [SerializeField] private int            gridColumns  = 4;
        [SerializeField] private int            gridRows     = 4;

        [Header("Highlight Animation")]
        [SerializeField] private float pulseMinAlpha = 0.25f;
        [SerializeField] private float pulseMaxAlpha = 0.6f;
        [SerializeField] private float pulseSpeed    = 2f;
        [SerializeField] private float autoRotateInterval = 5f;

        private GameObject? currentInstance;
        private int         previewLayer  = -1;
        private Vector2Int  highlightSize;
        private bool        highlightRotated;
        private float       rotateTimer;
        private Image?      highlightImage;

        void Awake()
        {
            this.previewLayer = LayerMask.NameToLayer(this.previewLayerName);
            this.root.SetActive(false);

            if (this.highlightRect != null)
                this.highlightImage = this.highlightRect.GetComponent<Image>();

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
            // Unscaled time -- inventory/inspect UI pauses gameplay via Time.timeScale = 0,
            // but this preview should keep spinning/pulsing while that's shown.
            if (this.currentInstance != null)
                this.currentInstance.transform.Rotate(Vector3.up, this.rotationSpeed * Time.unscaledDeltaTime, Space.World);

            if (this.highlightRect == null) return;

            if (this.highlightImage != null)
            {
                float alpha = Mathf.Lerp(this.pulseMinAlpha, this.pulseMaxAlpha,
                    Mathf.Sin(Time.unscaledTime * this.pulseSpeed) * 0.5f + 0.5f);
                var color = this.highlightImage.color;
                color.a = alpha;
                this.highlightImage.color = color;
            }

            // Only a non-square footprint has a meaningfully different "other" orientation.
            if (this.highlightSize.x == this.highlightSize.y) return;

            this.rotateTimer += Time.unscaledDeltaTime;
            if (this.rotateTimer < this.autoRotateInterval) return;

            this.rotateTimer = 0f;
            this.highlightRotated = !this.highlightRotated;
            ApplyHighlightSize();
        }

        public void Show(ItemData item)
        {
            ClearInstance();

            // Highlight the item's footprint from the grid's top-left cell (0,0),
            // same convention as InventoryGrid.CellToLocal for a center-pivoted grid.
            if (this.highlightRect != null)
            {
                this.highlightSize     = item.GridSize;
                this.highlightRotated  = false;
                this.rotateTimer       = 0f;
                ApplyHighlightSize();
                this.highlightRect.anchoredPosition = new Vector2(
                    -this.gridColumns * this.gridCellSize * 0.5f,
                     this.gridRows    * this.gridCellSize * 0.5f);
            }

            var modelPrefab = item.PreviewModel;

            // No model assigned on the ItemData yet -- still show the panel (grid/highlight),
            // just skip instantiating a 3D preview.
            if (modelPrefab != null)
            {
                this.currentInstance = Instantiate(modelPrefab, this.mountPoint.position, this.mountPoint.rotation, this.mountPoint);
                if (this.previewLayer >= 0)
                    SetLayerRecursively(this.currentInstance.transform, this.previewLayer);
            }

            this.root.SetActive(true);
            FadeVolume(1f);
        }

        public void Hide()
        {
            ClearInstance();
            this.root.SetActive(false);
            FadeVolume(0f);
        }

        private void FadeVolume(float target) => VolumeFader.Fade(this.inventoryVolume, target > 0f, this.volumeFadeDuration);

        private void ApplyHighlightSize()
        {
            if (this.highlightRect == null) return;
            var size = this.highlightRotated
                ? new Vector2(this.highlightSize.y, this.highlightSize.x)
                : new Vector2(this.highlightSize.x, this.highlightSize.y);
            this.highlightRect.sizeDelta = size * this.gridCellSize;
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
