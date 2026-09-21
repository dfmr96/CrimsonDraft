#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Yarn.Unity;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public sealed class PickupPreviewView : MonoBehaviour
    {
        [SerializeField] private GameObject  root            = null!;
        [SerializeField] private Transform   mountPoint       = null!;
        [SerializeField] private float       rotationSpeed    = 60f;
        [SerializeField] private bool        autoRotate       = true;
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
        private Camera?     previewCamera;
        private Quaternion  initialMountRotation;

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

            // mountPoint's own parent is the preview camera rig (see manage_gameobject hierarchy:
            // MountPoint is a direct child of PreviewCamera) -- used to rotate relative to what
            // the player actually sees instead of world axes.
            this.previewCamera        = this.mountPoint.parent.GetComponent<Camera>();
            this.initialMountRotation = this.mountPoint.rotation;
        }

        void Update()
        {
            // Unscaled time -- inventory/inspect UI pauses gameplay via Time.timeScale = 0,
            // but this preview should keep spinning/pulsing while that's shown.
            if (this.currentInstance != null && this.autoRotate)
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
            this.mountPoint.rotation = this.initialMountRotation;

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
                // Parent-only overload -- preserves the prefab's own authored local
                // position/rotation/scale under mountPoint, instead of forcing world
                // position/rotation to mountPoint's and discarding the prefab's offset.
                this.currentInstance = Instantiate(modelPrefab, this.mountPoint);
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

        // Used instead of the auto-rotate (autoRotate = false) by callers that let the player
        // spin the model themselves, e.g. InspectPanel feeding its InventoryNavigate axis.
        // Rotates mountPoint (not the model instance) around the preview camera's own up/right
        // so left/right and up/down always match what the player sees on screen, regardless of
        // how the camera rig itself is oriented.
        public void SetRotationInput(Vector2 axis)
        {
            if (this.currentInstance == null || axis == Vector2.zero) return;

            float   delta    = this.rotationSpeed * Time.unscaledDeltaTime;
            Vector3 camUp    = this.previewCamera != null ? this.previewCamera.transform.up    : Vector3.up;
            Vector3 camRight = this.previewCamera != null ? this.previewCamera.transform.right : Vector3.right;

            if (axis.x != 0f) this.mountPoint.Rotate(camUp,     axis.x  * delta, Space.World);
            if (axis.y != 0f) this.mountPoint.Rotate(camRight, -axis.y * delta, Space.World);
        }

        // Returns null when the currently-shown item has no ItemExamineHotspots at all, or
        // when nothing usable resolves -- callers should fall back to that item's own
        // default examine text in that case. A non-null result is either text to type or a
        // prompt to run -- see ExamineResolution.
        public ExamineResolution? TryGetExamineDialogue(IInventoryService inventory)
        {
            if (this.currentInstance == null) return null;

            var hotspots = this.currentInstance.GetComponentInChildren<ItemExamineHotspots>();
            if (hotspots == null) return null;

            Collider? hitCollider = null;
            if (this.previewCamera != null)
            {
                // Player rotates the model via mountPoint.Rotate (SetRotationInput), a plain
                // Transform op -- with autoSyncTransforms disabled project-wide and Time.timeScale
                // at 0 while inspect is open (no physics step to pick it up naturally), PhysX
                // would otherwise see a stale collider pose here.
                Physics.SyncTransforms();

                int mask = this.previewLayer >= 0 ? 1 << this.previewLayer : ~0;
                if (Physics.Raycast(this.previewCamera.transform.position, this.previewCamera.transform.forward,
                        out var hit, Mathf.Infinity, mask, QueryTriggerInteraction.Collide))
                {
                    hitCollider = hit.collider;
                }
            }

            return hotspots.Resolve(hitCollider, inventory);
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

        // Debug aid: draws the exact ray TryGetExamineDialogue() would cast right now
        // (green if it hits something on the preview layer, red if not), plus the
        // currently-shown instance's hotspot colliders, so both can be inspected together
        // without separately selecting the spawned model in the hierarchy.
        void OnDrawGizmosSelected()
        {
            if (this.previewCamera == null) return;

            // Same reasoning as TryGetExamineDialogue(): without this, a script-driven
            // rotation change may not be visible to Physics.Raycast yet.
            Physics.SyncTransforms();

            var origin  = this.previewCamera.transform.position;
            var forward = this.previewCamera.transform.forward;
            int mask    = this.previewLayer >= 0 ? 1 << this.previewLayer : ~0;

            bool hasHit = Physics.Raycast(origin, forward, out var hit, Mathf.Infinity, mask, QueryTriggerInteraction.Collide);

            Gizmos.color = hasHit ? Color.green : Color.red;
            Gizmos.DrawLine(origin, origin + forward * (hasHit ? hit.distance : 10f));
            if (hasHit) Gizmos.DrawWireSphere(hit.point, 0.03f);

            if (this.currentInstance != null)
                this.currentInstance.GetComponentInChildren<ItemExamineHotspots>()?.DrawGizmos();
        }
    }
}
