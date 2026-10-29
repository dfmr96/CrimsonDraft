#nullable enable

using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CrimsonDraft.Infrastructure.UI
{
    /// <summary>
    /// The military PDA that "turns on" when the inventory or the pause menu opens: it hangs in front of the
    /// game camera, unfolds from lying flat to facing the player, and only then does the UI appear. Closing
    /// plays it backwards. The model stays switched off the rest of the time.
    ///
    /// The PDA is a child of the active game camera while it is shown, so it sits in front of whichever
    /// camera the current zone uses. Its hinge is the bottom edge: at 0 degrees it faces the camera,
    /// at <see cref="foldAngle"/> it lies flat pointing away from it.
    ///
    /// It is drawn by its own overlay camera stacked on the game camera, on its own layer: the world
    /// camera never renders that layer, and the overlay clears only depth, so a wall right in front of the
    /// lens can never cover the PDA.
    /// </summary>
    public sealed class PdaDevice : MonoBehaviour
    {
        [SerializeField] private GameObject pdaPrefab = null!;
        [Tooltip("Layer exclusiva del PDA: la camara del juego no la dibuja, solo la camara overlay del PDA.")]
        [SerializeField] private string pdaLayerName = "PDA";

        [Header("Open pose (in camera space)")]
        [Tooltip("Donde queda el centro del borde inferior del PDA (la bisagra), relativo a la camara.")]
        [SerializeField] private Vector3 hingePosition = new Vector3(0f, -0.42f, 0.95f);
        [Tooltip("Rotacion del modelo para que su pantalla mire a la camara (el prefab esta acostado, X=90).")]
        [SerializeField] private Vector3 modelEuler = new Vector3(0f, 180f, 0f);
        [SerializeField] private float   modelScale = 1f;

        [Header("Motion")]
        [Tooltip("Grados que gira desde plegado hasta abierto.")]
        [SerializeField] private float foldAngle = 90f;
        [SerializeField] private float openDuration  = 0.45f;
        [SerializeField] private float closeDuration = 0.35f;

        private Transform? hinge;
        private Transform? model;
        private Camera?    overlayCamera;
        private UniversalAdditionalCameraData? mainData;
        private int        pdaLayer = -1;
        private Tween?     tween;
        private float      angle;      // current fold angle, foldAngle = closed
        private int        generation; // a newer Show/Hide invalidates the callback of an older one

        public bool IsShown => this.hinge != null && this.hinge.gameObject.activeSelf;

        /// <summary>Switches the PDA on and unfolds it; <paramref name="onOpened"/> runs once it is open.</summary>
        public void Show(Action? onOpened = null)
        {
            if (!Prepare())
            {
                onOpened?.Invoke();
                return;
            }

            this.tween?.Kill();
            int g = ++this.generation;

            // Starting from where it is: a Show during a Hide just reverses it.
            if (!this.hinge!.gameObject.activeSelf) SetAngle(this.foldAngle);
            this.hinge.gameObject.SetActive(true);

            float duration = this.openDuration * Mathf.Clamp01(this.angle / Mathf.Max(0.01f, this.foldAngle));
            this.tween = DOVirtual.Float(this.angle, 0f, Mathf.Max(0.0001f, duration), SetAngle)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(this)
                .OnComplete(() => { if (g == this.generation) onOpened?.Invoke(); });
        }

        /// <summary>Folds the PDA away and switches it off; <paramref name="onClosed"/> runs once it is gone.</summary>
        public void Hide(Action? onClosed = null)
        {
            if (this.hinge == null || !this.hinge.gameObject.activeSelf)
            {
                onClosed?.Invoke();
                return;
            }

            this.tween?.Kill();
            int g = ++this.generation;

            float duration = this.closeDuration * Mathf.Clamp01(1f - this.angle / Mathf.Max(0.01f, this.foldAngle));
            this.tween = DOVirtual.Float(this.angle, this.foldAngle, Mathf.Max(0.0001f, duration), SetAngle)
                .SetEase(Ease.InCubic).SetUpdate(true).SetTarget(this)
                .OnComplete(() =>
                {
                    if (g != this.generation) return;
                    this.hinge!.gameObject.SetActive(false);
                    SetOverlayActive(false);
                    onClosed?.Invoke();
                });
        }

        /// <summary>
        /// Draws or hides the PDA model without folding it (e.g. the pause menu's Video tab, where the bare
        /// scene has to be visible to judge gamma). A no-op while the PDA is not shown.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (!IsShown) return;
            SetOverlayActive(visible);
        }

        void OnDestroy()
        {
            this.tween?.Kill();
            SetOverlayActive(false);
            if (this.overlayCamera != null) Destroy(this.overlayCamera.gameObject);
            if (this.hinge != null) Destroy(this.hinge.gameObject);
        }

        // ── Rig ──────────────────────────────────────────────────────────────

        void SetAngle(float value)
        {
            this.angle = value;
            if (this.hinge != null) this.hinge.localRotation = Quaternion.Euler(value, 0f, 0f);
        }

        // Builds the hinge/model once and (re)hangs it on the camera that is rendering the game right now.
        bool Prepare()
        {
            var cam = Camera.main;
            if (cam == null || this.pdaPrefab == null) return false;

            if (this.hinge == null)
            {
                this.hinge = new GameObject("PdaHinge").transform;
                this.model = Instantiate(this.pdaPrefab, this.hinge).transform;
                this.model.gameObject.SetActive(true);
                this.hinge.gameObject.SetActive(false);
            }

            this.pdaLayer = LayerMask.NameToLayer(this.pdaLayerName);
            if (this.pdaLayer >= 0) PrepareOverlay(cam);

            this.hinge.SetParent(cam.transform, false);
            this.hinge.localPosition = this.hingePosition;
            this.hinge.localScale    = Vector3.one * this.modelScale;

            // Face the camera, then slide the model so its bottom edge sits on the hinge. Done at angle 0
            // each time: the camera (and so the model's frame of reference) can differ between zones.
            this.hinge.localRotation   = Quaternion.identity;
            this.model!.localRotation  = Quaternion.Euler(this.modelEuler);
            this.model.localPosition   = Vector3.zero;
            this.model.localScale      = Vector3.one;
            AlignBottomEdge();
            SetAngle(this.hinge.gameObject.activeSelf ? this.angle : this.foldAngle);
            return true;
        }

        // Puts the PDA on its own layer and has an overlay camera draw just that layer on top of the game.
        void PrepareOverlay(Camera main)
        {
            // The game camera must not draw it too, or the wall would be in front of it again.
            main.cullingMask &= ~(1 << this.pdaLayer);

            SetLayerRecursively(this.hinge!, this.pdaLayer);

            if (this.overlayCamera == null)
            {
                var go = new GameObject("PdaCamera");
                this.overlayCamera = go.AddComponent<Camera>();
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderType = CameraRenderType.Overlay;
                this.overlayCamera.clearFlags = CameraClearFlags.Depth; // keep the game's picture, wipe only its depth
                this.overlayCamera.enabled    = false;
            }

            var t = this.overlayCamera.transform;
            t.SetParent(main.transform, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;

            this.overlayCamera.cullingMask   = 1 << this.pdaLayer;
            this.overlayCamera.fieldOfView   = main.fieldOfView;
            this.overlayCamera.nearClipPlane = 0.05f;
            this.overlayCamera.farClipPlane  = 10f;

            this.mainData = main.GetUniversalAdditionalCameraData();
            SetOverlayActive(true);
        }

        // In the main camera's stack ahead of the UI camera (it renders in list order): the PDA is under the UI.
        void SetOverlayActive(bool active)
        {
            if (this.overlayCamera == null) return;
            this.overlayCamera.enabled = active;
            if (this.mainData == null) return;

            var stack = this.mainData.cameraStack;
            if (stack == null) return;
            bool inStack = stack.Contains(this.overlayCamera);
            if (active && !inStack)       stack.Insert(0, this.overlayCamera);
            else if (!active && inStack)  stack.Remove(this.overlayCamera);
        }

        static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursively(t.GetChild(i), layer);
        }

        // The model's body (its biggest mesh) is what the player sees as the PDA; the thin frame pieces
        // poke out past its top and bottom. The body, not the whole hierarchy, sits on the hinge so that
        // scaling it to fill the screen leaves no gap.
        void AlignBottomEdge()
        {
            bool any = false;
            Bounds body = default;
            float bestArea = -1f;

            foreach (var filter in this.model!.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;

                bool first = true;
                Bounds bounds = default;
                var mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var inHinge = this.hinge!.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (first) { bounds = new Bounds(inHinge, Vector3.zero); first = false; }
                    else       bounds.Encapsulate(inHinge);
                }

                float area = bounds.size.x * bounds.size.y;
                if (area > bestArea) { bestArea = area; body = bounds; any = true; }
            }
            if (!any) return;

            // Bottom-centre of the body on the hinge origin (x and z centred, lowest y on the hinge).
            this.model.localPosition = new Vector3(-body.center.x, -body.min.y, -body.center.z);
        }
    }
}
