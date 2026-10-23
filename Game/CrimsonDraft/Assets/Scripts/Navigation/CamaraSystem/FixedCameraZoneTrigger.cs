#nullable enable

using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace CrimsonDraft.Navigation.CamaraSystem
{
    /// <summary>Camera zone shaped as a polygon on the XZ floor plane (local space of this
    /// transform) extruded between <c>minHeight</c> and <c>maxHeight</c>. There is no collider:
    /// <see cref="FixedCameraZoneBootstrap"/> feeds the player position to <see cref="Evaluate"/>
    /// every frame. With the two-sided switch, the divider segment splits the zone between
    /// <c>zoneCamera</c> (negative side) and <c>positiveSideCamera</c> (positive side).</summary>
    public sealed class FixedCameraZoneTrigger : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera zoneCamera = null!;
        [Tooltip("Use Zone Camera on the negative side of the divider and Positive Side Camera on the positive side.")]
        [SerializeField] private bool useTwoSidedSwitch;
        [SerializeField] private CinemachineCamera? positiveSideCamera;
        [Tooltip("Distance in world units from the divider to each switching threshold. Total neutral width is twice this value.")]
        [Min(0f)] [SerializeField] private float sideMargin = 0.25f;

        [Tooltip("Zone outline as (x, z) points in local space. At least three.")]
        [SerializeField] private Vector2[] polygon =
        {
            new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f),
        };
        [Tooltip("Local Y range the player must be in, so a zone does not fire from another floor.")]
        [SerializeField] private float minHeight;
        [SerializeField] private float maxHeight = 3f;
        [Tooltip("Divider endpoints as (x, z) in local space. The positive side is to the left of start -> end seen from above (+Z for a divider running along +X).")]
        [SerializeField] private Vector2 dividerStart = new Vector2(-1f, 0f);
        [SerializeField] private Vector2 dividerEnd = new Vector2(1f, 0f);

        private IFixedCameraZoneService? zoneService;
        private bool playerInside;

        public CinemachineCamera ZoneCamera => this.zoneCamera;
        public IReadOnlyList<Vector2> Polygon => this.polygon;
        public float MinHeight => this.minHeight;
        public float MaxHeight => this.maxHeight;
        public bool UseTwoSidedSwitch => this.useTwoSidedSwitch;
        public float SideMargin => this.sideMargin;
        public Vector2 Centroid
        {
            get
            {
                if (this.polygon == null || this.polygon.Length == 0) return Vector2.zero;
                var sum = Vector2.zero;
                foreach (var point in this.polygon) sum += point;
                return sum / this.polygon.Length;
            }
        }
        public Vector2 DividerStart => this.dividerStart;
        public Vector2 DividerEnd => this.dividerEnd;

        /// <summary>Every camera this trigger can switch to (the positive side only when the two-sided switch is on).</summary>
        public IEnumerable<CinemachineCamera> ReferencedCameras
        {
            get
            {
                if (this.zoneCamera != null) yield return this.zoneCamera;
                if (this.useTwoSidedSwitch && this.positiveSideCamera != null) yield return this.positiveSideCamera;
            }
        }

        public void Construct(IFixedCameraZoneService zoneService)
        {
            this.zoneService = zoneService;
        }

        /// <summary>True when the world position is inside the polygon and within the height range.</summary>
        public bool Contains(Vector3 worldPosition)
        {
            var local = this.transform.InverseTransformPoint(worldPosition);
            if (local.y < this.minHeight || local.y > this.maxHeight) return false;
            return CameraZonePolygon.Contains(this.polygon, new Vector2(local.x, local.z));
        }

        public CinemachineCamera? ResolveCameraAt(Vector3 position, CinemachineCamera? current)
        {
            if (!this.useTwoSidedSwitch) return this.zoneCamera;
            // A partially configured switch must not turn off the current camera.
            if (this.zoneCamera == null || this.positiveSideCamera == null) return null;

            float distance = this.DistanceFromDivider(position);
            float margin = Mathf.Max(0f, this.sideMargin);
            if (distance > margin) return this.positiveSideCamera;
            if (distance < -margin) return this.zoneCamera;
            if (current == this.zoneCamera || current == this.positiveSideCamera) return current;

            // Spawning in the neutral band still needs a deterministic initial shot.
            return distance >= 0f ? this.positiveSideCamera : this.zoneCamera;
        }

        /// <summary>Called every frame with the player position. Fires on entering the polygon; with the
        /// two-sided switch it keeps re-resolving while the player stays inside.</summary>
        public void Evaluate(Vector3 playerPosition)
        {
            bool inside = this.Contains(playerPosition);
            bool entered = inside && !this.playerInside;
            this.playerInside = inside;

            if (inside && (entered || this.useTwoSidedSwitch)) this.TryActivate(playerPosition);
        }

        private void OnDisable()
        {
            // A room being hidden must re-fire its zone when the player lands in it again.
            this.playerInside = false;
        }

        private void TryActivate(Vector3 playerPosition)
        {
            if (this.zoneService == null) return;

            var camera = this.ResolveCameraAt(playerPosition, this.zoneService.CurrentZoneCamera);
            if (camera == null) return;
            if (camera == this.zoneService.CurrentZoneCamera && camera.enabled) return;

            this.zoneService.ActivateZone(camera);
        }

        /// <summary>Signed world-space distance on the floor plane from the divider; positive on the
        /// camera-B side. Zero when the divider is degenerate.</summary>
        private float DistanceFromDivider(Vector3 worldPosition)
        {
            var start = this.transform.TransformPoint(new Vector3(this.dividerStart.x, 0f, this.dividerStart.y));
            var end = this.transform.TransformPoint(new Vector3(this.dividerEnd.x, 0f, this.dividerEnd.y));
            var direction = new Vector2(end.x - start.x, end.z - start.z);
            if (direction.sqrMagnitude < 1e-8f) return 0f;

            var normal = new Vector2(-direction.y, direction.x).normalized;
            return Vector2.Dot(new Vector2(worldPosition.x - start.x, worldPosition.z - start.z), normal);
        }

#if UNITY_EDITOR
        /// <summary>Camera the Camera Zone Viewer is looking through; its triggers draw more opaque.</summary>
        public static CinemachineCamera? GizmoHighlightCamera;
#endif

        /// <summary>Stable colour per camera, shared by the scene gizmos and the Camera Zone Viewer.</summary>
        public static Color GizmoColorFor(CinemachineCamera? camera)
        {
            if (camera == null) return Color.gray;
            uint hash = 2166136261;
            foreach (char c in camera.name) hash = (hash ^ c) * 16777619;
            return Color.HSVToRGB((hash % 360u) / 360f, 0.75f, 1f);
        }

        public Vector3 ToWorld(Vector2 point, float height) =>
            this.transform.TransformPoint(new Vector3(point.x, height, point.y));

        /// <summary>The side walls of the extruded polygon, one world-space quad per edge.</summary>
        public void GetWallQuads(List<Vector3[]> quads)
        {
            int count = this.polygon.Length;
            for (int i = 0; i < count && count >= 3; i++)
            {
                var a = this.polygon[i];
                var b = this.polygon[(i + 1) % count];
                quads.Add(new[]
                {
                    this.ToWorld(a, this.minHeight), this.ToWorld(b, this.minHeight),
                    this.ToWorld(b, this.maxHeight), this.ToWorld(a, this.maxHeight),
                });
            }
        }

#if UNITY_EDITOR
        /// <summary>Floor and ceiling of the extruded polygon as world-space triangles.</summary>
        public void GetCapTriangles(List<Vector3[]> triangles)
        {
            var indices = CameraZonePolygon.Triangulate(this.polygon);
            foreach (float height in new[] { this.minHeight, this.maxHeight })
            {
                for (int i = 0; i + 2 < indices.Count; i += 3)
                {
                    triangles.Add(new[]
                    {
                        this.ToWorld(this.polygon[indices[i]], height),
                        this.ToWorld(this.polygon[indices[i + 1]], height),
                        this.ToWorld(this.polygon[indices[i + 2]], height),
                    });
                }
            }
        }

#endif

        /// <summary>A vertical world-space quad along the divider, pushed <paramref name="offset"/> world units
        /// along its positive normal. Offset 0 is the divider itself; ±sideMargin are the thresholds.</summary>
        public Vector3[] GetDividerQuad(float offset)
        {
            var start = this.ToWorld(this.dividerStart, this.minHeight);
            var end = this.ToWorld(this.dividerEnd, this.minHeight);
            var direction = new Vector2(end.x - start.x, end.z - start.z);
            var normal = direction.sqrMagnitude < 1e-8f ? Vector2.zero : new Vector2(-direction.y, direction.x).normalized;
            var push = new Vector3(normal.x, 0f, normal.y) * offset;
            var rise = this.transform.up * (this.maxHeight - this.minHeight);
            return new[] { start + push, end + push, end + push + rise, start + push + rise };
        }

#if UNITY_EDITOR
        private void Reset()
        {
            this.polygon = new[]
            {
                new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f),
            };
            this.dividerStart = new Vector2(-1f, 0f);
            this.dividerEnd = new Vector2(1f, 0f);
        }

        private void OnDrawGizmos()
        {
            if (this.polygon == null || this.polygon.Length < 3) return;

            var color = GizmoColorFor(this.zoneCamera);
            bool emphasized = UnityEditor.Selection.Contains(this.gameObject)
                || (GizmoHighlightCamera != null
                    && (this.zoneCamera == GizmoHighlightCamera || this.positiveSideCamera == GizmoHighlightCamera));

            var fills = new List<Vector3[]>();
            this.GetCapTriangles(fills);
            this.GetWallQuads(fills);
            UnityEditor.Handles.color = new Color(color.r, color.g, color.b, emphasized ? 0.22f : 0.08f);
            foreach (var face in fills) UnityEditor.Handles.DrawAAConvexPolygon(face);

            var walls = new List<Vector3[]>();
            this.GetWallQuads(walls);
            Gizmos.color = color;
            foreach (var wall in walls)
            {
                Gizmos.DrawLine(wall[0], wall[1]);
                Gizmos.DrawLine(wall[2], wall[3]);
                Gizmos.DrawLine(wall[0], wall[3]);
            }

            var center = Vector3.zero;
            foreach (var point in this.polygon) center += this.ToWorld(point, (this.minHeight + this.maxHeight) * 0.5f);
            center /= this.polygon.Length;

            var label = this.zoneCamera != null ? this.zoneCamera.name : "(no camera)";
            if (this.useTwoSidedSwitch && this.positiveSideCamera != null) label += " | " + this.positiveSideCamera.name;
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(center, label);

            if (this.useTwoSidedSwitch)
            {
                float margin = Mathf.Max(0f, this.sideMargin);
                DrawQuadOutline(this.GetDividerQuad(-margin), GizmoColorFor(this.zoneCamera));
                DrawQuadOutline(this.GetDividerQuad(margin), GizmoColorFor(this.positiveSideCamera));
                DrawQuadOutline(this.GetDividerQuad(0f), Color.white);
            }
        }

        private static void DrawQuadOutline(Vector3[] quad, Color color)
        {
            Gizmos.color = color;
            for (int i = 0; i < 4; i++) Gizmos.DrawLine(quad[i], quad[(i + 1) % 4]);
        }
#endif
    }
}
