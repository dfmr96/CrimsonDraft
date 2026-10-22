#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CrimsonDraft.Infrastructure.UI
{
    /// <summary>
    /// Keeps a UI camera rendering only while some UI it draws is actually visible. The gameplay
    /// "UI CRT Camera" is an Overlay stacked on the world camera and its renderer runs the CRT pass
    /// over the whole shared frame, so leaving it on with nothing to draw costs a full-screen pass
    /// and re-applies the CRT on top of the world camera's own.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(10000)] // after every UI script has shown/hidden its panels this frame
    public sealed class UiCameraAutoToggle : MonoBehaviour
    {
        // Canvases come and go with scene loads and runtime instantiation; a periodic full rescan
        // catches anything the scene events and Track() don't.
        private const float RescanInterval = 1f;

        private static readonly List<UiCameraAutoToggle> instances = new();

        private readonly List<Canvas> canvases = new();
        private Camera uiCamera = null!;
        private float  nextRescan;

        /// <summary>Every managed UI camera, rendering or not -- Camera.allCameras skips disabled ones,
        /// so code that looks up "the camera that draws the UI layer" must include these.</summary>
        public static IReadOnlyList<UiCameraAutoToggle> Instances => instances;

        /// <summary>Master switch for rendering UI/preview cameras only while needed. False restores
        /// the original always-on behaviour, for A/B performance comparisons (debug key F5).</summary>
        public static bool GatingEnabled
        {
            get => gatingEnabled;
            set
            {
                if (gatingEnabled == value) return;
                gatingEnabled = value;
                GatingChanged?.Invoke();
            }
        }
        private static bool gatingEnabled = true;

        /// <summary>Raised when <see cref="GatingEnabled"/> flips, for cameras gated outside this
        /// component (e.g. the pickup preview) that may be sitting on an inactive object.</summary>
        public static event System.Action? GatingChanged;

        public Camera Camera => this.uiCamera;

        // Statics outlive Play sessions when domain reload is disabled; start each one optimized.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instances.Clear();
            gatingEnabled = true;
            GatingChanged = null;
        }

        void Awake()
        {
            this.uiCamera = GetComponent<Camera>();
        }

        void OnEnable()
        {
            instances.Add(this);
            SceneManager.sceneLoaded   += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Rescan();
        }

        void OnDisable()
        {
            instances.Remove(this);
            SceneManager.sceneLoaded   -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;

            // With the gate off, the camera goes back to always rendering, as authored.
            if (this.uiCamera != null) this.uiCamera.enabled = true;
        }

        /// <summary>Tracks a canvas bound to this camera at runtime (e.g. a persistent view moved onto
        /// the UI layer) so it shows on the same frame instead of after the next rescan.</summary>
        public void Track(Canvas canvas)
        {
            if (!this.canvases.Contains(canvas)) this.canvases.Add(canvas);
        }

        void LateUpdate()
        {
            if (!GatingEnabled)
            {
                if (!this.uiCamera.enabled) this.uiCamera.enabled = true;
                return;
            }

            if (Time.unscaledTime >= this.nextRescan) Rescan();

            bool visible = HasVisibleContent(this.uiCamera, this.canvases);
            if (this.uiCamera.enabled != visible) this.uiCamera.enabled = visible;
        }

        /// <summary>True when any active, non-transparent Graphic sits on one of
        /// <paramref name="canvases"/> whose root canvas is drawn by <paramref name="camera"/>.</summary>
        public static bool HasVisibleContent(Camera camera, IReadOnlyList<Canvas> canvases)
        {
            for (int i = 0; i < canvases.Count; i++)
            {
                var canvas = canvases[i];
                if (canvas == null || !canvas.isActiveAndEnabled) continue;

                var root = canvas.rootCanvas;
                if (root.renderMode == RenderMode.ScreenSpaceOverlay || root.worldCamera != camera) continue;

                var graphics = GraphicRegistry.GetGraphicsForCanvas(canvas);
                for (int g = 0; g < graphics.Count; g++)
                    if (IsVisible(graphics[g])) return true;
            }
            return false;
        }

        // GetInheritedAlpha folds in every parent CanvasGroup, so panels faded out with a group
        // (rather than deactivated) count as hidden too.
        private static bool IsVisible(Graphic graphic) =>
            graphic != null
            && graphic.isActiveAndEnabled
            && graphic.color.a > 0f
            && !graphic.canvasRenderer.cull
            && graphic.canvasRenderer.GetInheritedAlpha() > 0f;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Rescan();

        private void OnSceneUnloaded(Scene scene) => Rescan();

        private void Rescan()
        {
            this.nextRescan = Time.unscaledTime + RescanInterval;
            this.canvases.Clear();
            this.canvases.AddRange(FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }
    }
}
