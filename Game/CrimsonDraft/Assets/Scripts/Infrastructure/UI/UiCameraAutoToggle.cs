#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CrimsonDraft.Infrastructure.UI
{
    /// <summary>
    /// Keeps a UI camera rendering only while some UI drawn by it is open. The gameplay
    /// "UI CRT Camera" is an Overlay stacked on the world camera and its renderer runs the CRT pass
    /// over the whole shared frame, so leaving it on with nothing to draw costs a full-screen pass
    /// and re-applies the CRT on top of the world camera's own.
    /// Who needs it is told explicitly through <see cref="UiCameraGate"/>: each UI requests the camera
    /// when it opens and releases it when it closes. In the editor and development builds a watchdog
    /// warns about UI visible on this camera that nobody requested.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class UiCameraAutoToggle : MonoBehaviour
    {
        private static readonly List<UiCameraAutoToggle> instances = new();

        private Camera uiCamera = null!;

        /// <summary>Every managed UI camera, rendering or not -- Camera.allCameras skips disabled ones,
        /// so code that looks up "the camera that draws the UI layer" must include these.</summary>
        public static IReadOnlyList<UiCameraAutoToggle> Instances => instances;

        /// <summary>Master switch for rendering UI/preview cameras only while needed. False restores
        /// the original always-on behaviour, for A/B performance comparisons (debug menu).</summary>
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
            UiCameraGate.Changed += Apply;
            GatingChanged        += Apply;
            Apply();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SceneManager.sceneLoaded   += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Rescan();
#endif
        }

        void OnDisable()
        {
            instances.Remove(this);
            UiCameraGate.Changed -= Apply;
            GatingChanged        -= Apply;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SceneManager.sceneLoaded   -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
#endif

            // With the gate off, the camera goes back to always rendering, as authored.
            if (this.uiCamera != null) this.uiCamera.enabled = true;
        }

        private void Apply()
        {
            if (this.uiCamera == null) return;
            bool render = !GatingEnabled || UiCameraGate.IsRequested;
            if (this.uiCamera.enabled != render) this.uiCamera.enabled = render;
        }

        /// <summary>True when any active, non-transparent Graphic sits on one of
        /// <paramref name="canvases"/> whose root canvas is drawn by <paramref name="camera"/>.</summary>
        public static bool HasVisibleContent(Camera camera, IReadOnlyList<Canvas> canvases)
            => FirstVisibleCanvas(camera, canvases) != null;

        /// <summary>The first canvas from <paramref name="canvases"/> drawn by <paramref name="camera"/>
        /// that shows something, or null.</summary>
        public static Canvas? FirstVisibleCanvas(Camera camera, IReadOnlyList<Canvas> canvases)
        {
            for (int i = 0; i < canvases.Count; i++)
            {
                var canvas = canvases[i];
                if (canvas == null || !canvas.isActiveAndEnabled) continue;

                var root = canvas.rootCanvas;
                if (root.renderMode == RenderMode.ScreenSpaceOverlay || root.worldCamera != camera) continue;

                var graphics = GraphicRegistry.GetGraphicsForCanvas(canvas);
                for (int g = 0; g < graphics.Count; g++)
                    if (IsVisible(graphics[g])) return canvas;
            }
            return null;
        }

        // GetInheritedAlpha folds in every parent CanvasGroup, so panels faded out with a group
        // (rather than deactivated) count as hidden too.
        private static bool IsVisible(Graphic graphic) =>
            graphic != null
            && graphic.isActiveAndEnabled
            && graphic.color.a > 0f
            && !graphic.canvasRenderer.cull
            && graphic.canvasRenderer.GetInheritedAlpha() > 0f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ---- Watchdog: UI on this camera that nobody requested would not show. ----

        private const float WatchInterval = 1f;

        private readonly List<Canvas> canvases = new();
        private readonly HashSet<string> warned = new();
        private float nextWatch;

        void Update()
        {
            if (!GatingEnabled || Time.unscaledTime < this.nextWatch) return;
            this.nextWatch = Time.unscaledTime + WatchInterval;
            Rescan();
            Apply(); // also catches an owner destroyed without releasing

            if (UiCameraGate.IsRequested) return;
            var canvas = FirstVisibleCanvas(this.uiCamera, this.canvases);
            if (canvas == null || !this.warned.Add(canvas.name)) return;
            Debug.LogWarning($"[UiCameraGate] '{canvas.name}' shows UI on '{this.name}' but nothing requested the camera, " +
                             "so it is not drawn. Call UiCameraGate.Request/Release where that UI opens and closes.", canvas);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Rescan();

        private void OnSceneUnloaded(Scene scene) => Rescan();

        private void Rescan()
        {
            this.canvases.Clear();
            this.canvases.AddRange(FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }
#endif
    }
}
