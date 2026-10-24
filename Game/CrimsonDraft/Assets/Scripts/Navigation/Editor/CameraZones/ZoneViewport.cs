#nullable enable

using System;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>A hidden camera rendering into a texture for an editor window. Render from
    /// EditorApplication.update, never inside OnGUI (rendering a URP camera from a GUI event corrupts
    /// the GUIClip stack).</summary>
    internal sealed class ZoneViewport : IDisposable
    {
        private readonly string name;
        private Camera? camera;
        private RenderTexture? texture;

        public ZoneViewport(string name) => this.name = name;

        public bool HasCamera => this.camera != null;
        public Camera Camera => this.EnsureCamera();
        public Texture? Texture => this.texture;

        /// <param name="setup">Places and configures the camera before it renders.</param>
        /// <param name="unlit">URP material debug mode "Albedo": surfaces without lighting.</param>
        public void Render(Vector2Int pixels, Action<Camera> setup, bool unlit)
        {
            if (this.texture == null || this.texture.width != pixels.x || this.texture.height != pixels.y)
            {
                this.ReleaseTexture();
                this.texture = new RenderTexture(pixels.x, pixels.y, 24, RenderTextureFormat.ARGB32)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            var view = this.EnsureCamera();
            setup(view);
            view.aspect = (float)pixels.x / pixels.y;
            view.targetTexture = this.texture;

            // The debug setting is global, so it is switched on only for this synchronous Render call.
            var material = UniversalRenderPipelineDebugDisplaySettings.Instance.materialSettings;
            var previousMode = material.materialDebugMode;
            if (unlit) material.materialDebugMode = DebugMaterialMode.Albedo;
            try { view.Render(); }
            finally { material.materialDebugMode = previousMode; }
        }

        /// <summary>Largest rect with the given aspect ratio that fits in <paramref name="area"/>, centred.</summary>
        public static Rect FitToAspect(Rect area, float aspect)
        {
            if (aspect <= 0f || area.width <= 0f || area.height <= 0f) return area;
            float width = Mathf.Min(area.width, area.height * aspect);
            float height = width / aspect;
            return new Rect(area.x + (area.width - width) * 0.5f, area.y + (area.height - height) * 0.5f, width, height);
        }

        public static Rect FitToGameAspect(Rect area)
        {
            var size = Handles.GetMainGameViewSize();
            return size.x <= 0f || size.y <= 0f ? area : FitToAspect(area, size.x / size.y);
        }

        public static void CopyLens(CinemachineCamera source, Camera target)
        {
            var lens = source.Lens;
            target.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            target.orthographic = lens.ModeOverride == LensSettings.OverrideModes.Orthographic
                || (lens.ModeOverride == LensSettings.OverrideModes.None && lens.Orthographic);
            target.fieldOfView = lens.FieldOfView;
            target.orthographicSize = lens.OrthographicSize;
            target.nearClipPlane = lens.NearClipPlane;
            target.farClipPlane = lens.FarClipPlane;
            target.clearFlags = CameraClearFlags.Skybox;
        }

        public void Dispose()
        {
            if (this.camera != null) UnityEngine.Object.DestroyImmediate(this.camera.gameObject);
            this.camera = null;
            this.ReleaseTexture();
        }

        private Camera EnsureCamera()
        {
            if (this.camera != null) return this.camera;
            var go = new GameObject(this.name) { hideFlags = HideFlags.HideAndDontSave };
            this.camera = go.AddComponent<Camera>();
            this.camera.enabled = false;
            this.camera.allowHDR = false;
            this.camera.allowMSAA = false;
            var urpData = go.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderPostProcessing = false;
            urpData.antialiasing = AntialiasingMode.None;
            urpData.renderShadows = false;
            return this.camera;
        }

        private void ReleaseTexture()
        {
            if (this.texture != null)
            {
                this.texture.Release();
                UnityEngine.Object.DestroyImmediate(this.texture);
            }
            this.texture = null;
        }
    }
}
