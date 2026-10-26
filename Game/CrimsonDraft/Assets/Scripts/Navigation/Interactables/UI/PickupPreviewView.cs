#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Infrastructure.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Rendering.Shine;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public sealed class PickupPreviewView : MonoBehaviour
    {
        [SerializeField] private GameObject  root            = null!;
        [SerializeField] private Transform   mountPoint       = null!;
        [SerializeField] private float       rotationSpeed    = 60f;
        [SerializeField] private float       activationRotationSpeed = 240f;
        [SerializeField] private bool        autoRotate       = true;
        [SerializeField] private string      previewLayerName = "ItemPreview";

        private GameObject? currentInstance;
        private int         previewLayer  = -1;
        private Camera?     previewCamera;
        private Quaternion  initialMountRotation;

        void Awake()
        {
            this.previewLayer = LayerMask.NameToLayer(this.previewLayerName);
            this.root.SetActive(false);

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

            // Detached above, the rig no longer switches off with the panel -- so only render the
            // preview while it is shown. Only the Camera component: mountPoint (the model) is its child.
            SyncPreviewCamera();
            UiCameraAutoToggle.GatingChanged += SyncPreviewCamera;
        }

        void OnDestroy()
        {
            UiCameraGate.Release(this);
            UiCameraAutoToggle.GatingChanged -= SyncPreviewCamera;
        }

        void Update()
        {
            // Unscaled time -- inventory/inspect UI pauses gameplay via Time.timeScale = 0,
            // but this preview should keep spinning while that's shown.
            if (this.currentInstance != null && this.autoRotate)
                this.currentInstance.transform.Rotate(Vector3.up, this.rotationSpeed * Time.unscaledDeltaTime, Space.World);
        }

        public void Show(ItemData item)
        {
            ClearInstance();
            this.mountPoint.rotation = this.initialMountRotation;

            var modelPrefab = item.PreviewModel;

            // No model assigned on the ItemData yet -- still show the panel, just skip instantiating a 3D preview.
            if (modelPrefab != null)
            {
                // Parent-only overload -- preserves the prefab's own authored local
                // position/rotation/scale under mountPoint, instead of forcing world
                // position/rotation to mountPoint's and discarding the prefab's offset.
                this.currentInstance = Instantiate(modelPrefab, this.mountPoint);
                if (this.previewLayer >= 0)
                    SetLayerRecursively(this.currentInstance.transform, this.previewLayer);
            }

            UiCameraGate.Request(this);
            this.root.SetActive(true);
            SyncPreviewCamera();
        }

        public void Hide()
        {
            ClearInstance();
            this.root.SetActive(false);
            UiCameraGate.Release(this);
            SyncPreviewCamera();
        }

        private void SyncPreviewCamera()
        {
            if (this.previewCamera == null) return;
            bool render = !UiCameraAutoToggle.GatingEnabled || this.root.activeSelf;
            if (this.previewCamera.enabled != render) this.previewCamera.enabled = render;
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

        // Smoothly rotates mountPoint from whatever the player left it at to targetRotation,
        // at activationRotationSpeed degrees/sec, using unscaled time (same reasoning as
        // everywhere else in this class -- the inventory pauses the game while this is shown).
        // Used to snap the model to a known, camera-friendly angle before playing a hotspot's
        // reveal animation, regardless of how the player was aiming when they confirmed.
        public async UniTask RotateMountPointTo(Quaternion targetRotation, CancellationToken cancellationToken = default)
        {
            const float doneThresholdDegrees = 0.5f;
            while (Quaternion.Angle(this.mountPoint.rotation, targetRotation) > doneThresholdDegrees)
            {
                this.mountPoint.rotation = Quaternion.RotateTowards(
                    this.mountPoint.rotation, targetRotation, this.activationRotationSpeed * Time.unscaledDeltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
            this.mountPoint.rotation = targetRotation;
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

        private void ClearInstance()
        {
            if (this.currentInstance == null) return;
            Destroy(this.currentInstance);
            this.currentInstance = null;
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            // World pickups and this preview instantiate the exact same prefab -- a
            // ShineOverlayRoot subtree (ItemShineOverlay's glint, see CrimsonDraft.Rendering.Shine)
            // must keep its own layer instead of being pulled onto ItemPreview here, or the
            // world-only shine would start rendering in the inspect/inventory preview too.
            if (t.GetComponent<ShineOverlayRoot>() != null) return;

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
