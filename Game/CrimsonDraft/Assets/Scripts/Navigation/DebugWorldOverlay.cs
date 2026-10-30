#nullable enable

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.DebugMenu;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation
{
    // Debug-only world overlay that shows up in gameplay (Gizmos only exist in the editor), a single
    // toggle of the debug menu that draws both:
    //  - Camera triggers: the floor outline of every active FixedCameraZoneTrigger in its camera's
    //    colour, white while the player is inside, plus the divider/thresholds of two-sided switches.
    //    No walls or ceiling.
    //  - Player marker: a small filled circle at the player's feet with a red dot at its centre.
    // Everything is flattened onto one horizontal plane, the one the player's feet stand on, so the
    // triggers and the marker are coplanar whatever each trigger's own minHeight is.
    // Drawn with GL lines on the camera that carries the CinemachineBrain, after it has rendered.
    public sealed class DebugWorldOverlay : IInitializable, IDisposable
    {
        private const string Group      = "Overlays";
        private const float  MarkerRadius = 0.1f;
        private const float  DotRadius    = 0.03f;
        private const int    MarkerSegments = 24;

        private readonly FixedCameraZoneTrigger[] triggers;
        private readonly PlayerController         player;
        private readonly IDebugMenu               menu;
        private readonly List<Vector3[]>          wallBuffer = new List<Vector3[]>();

        private float        planeY;
        private IDisposable[] registrations = Array.Empty<IDisposable>();
        private Material?     material;
        private bool      missingShaderReported;

        public bool Visible { get; set; }

        [Preserve]
        public DebugWorldOverlay(IDebugMenu menu, FixedCameraZoneTrigger[] triggers, PlayerController player)
        {
            this.menu     = menu;
            this.triggers = triggers;
            this.player   = player;
        }

        void IInitializable.Initialize()
        {
            this.registrations = new[]
            {
                this.menu.Register(DebugMenuEntry.Toggle(Group, "Camera triggers + player marker",
                    () => this.Visible, on => this.Visible = on)),
            };

            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!this.Visible) return;
            if (camera.cameraType != CameraType.Game || !camera.TryGetComponent<CinemachineBrain>(out _)) return;
            if (!EnsureMaterial()) return;

            GL.PushMatrix();
            GL.LoadProjectionMatrix(GL.GetGPUProjectionMatrix(camera.projectionMatrix, camera.targetTexture != null));
            GL.modelview = camera.worldToCameraMatrix;
            this.planeY = this.player.FootPosition.y;
            this.material!.SetPass(0);
            GL.Begin(GL.LINES);

            DrawTriggers();
            GL.End();

            GL.Begin(GL.TRIANGLES);
            DrawPlayerMarker();
            GL.End();

            GL.PopMatrix();
        }

        private void DrawTriggers()
        {
            // Same position FixedCameraZoneBootstrap feeds to the zones, so "inside" matches what fires.
            var playerPosition = this.player.transform.position;
            foreach (var trigger in this.triggers)
            {
                if (trigger == null || !trigger.isActiveAndEnabled || trigger.Polygon.Count < 3) continue;

                var color = trigger.Contains(playerPosition) ? Color.white : FixedCameraZoneTrigger.GizmoColorFor(trigger.ZoneCamera);
                GL.Color(color);

                this.wallBuffer.Clear();
                trigger.GetWallQuads(this.wallBuffer);
                foreach (var wall in this.wallBuffer) Line(wall[0], wall[1]); // bottom edge of each wall = floor outline
            }
        }

        // Two stacked triangle fans around the player's feet (transform.position is the Rigidbody
        // pivot, footOffset above the ground): the disc, then the dot over it. ZTest is Always, so
        // the dot wins by being drawn last.
        private void DrawPlayerMarker()
        {
            var feet = this.player.FootPosition;
            feet.y = this.planeY;

            FillCircle(feet, MarkerRadius, new Color(1f, 0.85f, 0.2f));
            FillCircle(feet, DotRadius, new Color(0.9f, 0.1f, 0.1f));
        }

        private static void FillCircle(Vector3 center, float radius, Color color)
        {
            GL.Color(color);
            for (int i = 0; i < MarkerSegments; i++)
            {
                float a0 = i       * Mathf.PI * 2f / MarkerSegments;
                float a1 = (i + 1) * Mathf.PI * 2f / MarkerSegments;
                GL.Vertex(center);
                GL.Vertex(center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius);
                GL.Vertex(center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius);
            }
        }

        private void Line(Vector3 a, Vector3 b)
        {
            a.y = b.y = this.planeY;
            GL.Vertex(a);
            GL.Vertex(b);
        }

        private bool EnsureMaterial()
        {
            if (this.material != null) return true;

            // Always shipped with the engine, but can be stripped from a build that never references it.
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                if (!this.missingShaderReported)
                    Debug.LogWarning("[DebugWorldOverlay] Shader 'Hidden/Internal-Colored' not found; overlays can't draw. " +
                                     "Add it to Project Settings > Graphics > Always Included Shaders.");
                this.missingShaderReported = true;
                return false;
            }

            this.material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            this.material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            this.material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            this.material.SetInt("_Cull",     (int)CullMode.Off);
            this.material.SetInt("_ZWrite",   0);
            this.material.SetInt("_ZTest",    (int)CompareFunction.Always); // always on top of the level
            return true;
        }

        void IDisposable.Dispose()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            foreach (var registration in this.registrations) registration.Dispose();
            if (this.material == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(this.material);
            else UnityEngine.Object.DestroyImmediate(this.material);
        }
    }
}

#endif
