#nullable enable

using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Interactables;

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

        [Tooltip("Measure the polygon and heights in the parent's space (the room) instead of this object's. " +
                 "For a zone living on its camera: moving or turning the camera then never moves where it activates.")]
        [SerializeField] private bool polygonInParentSpace;
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

        [Header("Completion gate (optional)")]
        [Tooltip("Once every socket / puzzle referenced here is complete, this zone stops cutting to its camera. A camera already showing stays until the player leaves the zone.")]
        [SerializeField] private ItemSocketInteractable? completeWhenSocket;
        [Tooltip("Component implementing IPuzzle (PuzzleInteractable, GeneratorSwitchPanel, MusicBoxPanel...). Complete once it is solved.")]
        [SerializeField] private MonoBehaviour? completeWhenPuzzle;

        private IFixedCameraZoneService? zoneService;
        private PuzzleStateRegistry? puzzleStates;
        private bool playerInside;

        public CinemachineCamera ZoneCamera => this.zoneCamera;
        public IReadOnlyList<Vector2> Polygon => this.ActiveShape.Polygon;
        public float MinHeight => this.ActiveShape.MinHeight;
        public float MaxHeight => this.ActiveShape.MaxHeight;
        public bool UseTwoSidedSwitch => this.useTwoSidedSwitch;
        public float SideMargin => this.sideMargin;
        public Vector2 Centroid
        {
            get
            {
                var polygon = this.ActiveShape.Polygon;
                if (polygon == null || polygon.Length == 0) return Vector2.zero;
                var sum = Vector2.zero;
                foreach (var point in polygon) sum += point;
                return sum / polygon.Length;
            }
        }
        public Vector2 DividerStart => this.ActiveShape.DividerStart;
        public Vector2 DividerEnd => this.ActiveShape.DividerEnd;

        /// <summary>True while the zone still has the old BoxCollider. Such a zone is built from that box
        /// (same footprint, height and divider as the migration would produce) until it is converted, so
        /// scenes that were never migrated keep working.</summary>
        public bool IsLegacyBox => this.TryGetComponent<BoxCollider>(out _);

        /// <summary>Every camera this trigger can switch to (the positive side only when the two-sided switch is on).</summary>
        public IEnumerable<CinemachineCamera> ReferencedCameras
        {
            get
            {
                if (this.zoneCamera != null) yield return this.zoneCamera;
                if (this.useTwoSidedSwitch && this.positiveSideCamera != null) yield return this.positiveSideCamera;
            }
        }

        /// <summary>Everything that defines a zone footprint, from either the serialized polygon or a legacy box.</summary>
        public readonly struct ZoneShape
        {
            public readonly Vector2[] Polygon;
            public readonly float MinHeight;
            public readonly float MaxHeight;
            public readonly Vector2 DividerStart;
            public readonly Vector2 DividerEnd;

            public ZoneShape(Vector2[] polygon, float minHeight, float maxHeight, Vector2 dividerStart, Vector2 dividerEnd)
            {
                this.Polygon = polygon;
                this.MinHeight = minHeight;
                this.MaxHeight = maxHeight;
                this.DividerStart = dividerStart;
                this.DividerEnd = dividerEnd;
            }
        }

        /// <summary>The rectangle a box collider stands for: its footprint and height range, with the divider
        /// along the local X axis of the box through its centre (the plane the old two-sided switch used).</summary>
        public static ZoneShape ShapeFromBox(BoxCollider box)
        {
            var center = box.center;
            var half = box.size * 0.5f;
            return new ZoneShape(
                new[]
                {
                    new Vector2(center.x - half.x, center.z - half.z),
                    new Vector2(center.x - half.x, center.z + half.z),
                    new Vector2(center.x + half.x, center.z + half.z),
                    new Vector2(center.x + half.x, center.z - half.z),
                },
                center.y - half.y, center.y + half.y,
                new Vector2(center.x - half.x, center.z),
                new Vector2(center.x + half.x, center.z));
        }

        private BoxCollider? cachedBox;
        private Vector3 cachedBoxCenter;
        private Vector3 cachedBoxSize;
        private ZoneShape cachedBoxShape;

        private ZoneShape ActiveShape
        {
            get
            {
                if (!this.TryGetComponent<BoxCollider>(out var box))
                    return new ZoneShape(this.polygon, this.minHeight, this.maxHeight, this.dividerStart, this.dividerEnd);

                if (this.cachedBox != box || this.cachedBoxCenter != box.center || this.cachedBoxSize != box.size)
                {
                    this.cachedBox = box;
                    this.cachedBoxCenter = box.center;
                    this.cachedBoxSize = box.size;
                    this.cachedBoxShape = ShapeFromBox(box);
                }
                return this.cachedBoxShape;
            }
        }

        public void Construct(IFixedCameraZoneService zoneService, PuzzleStateRegistry? puzzleStates = null)
        {
            this.zoneService = zoneService;
            this.puzzleStates = puzzleStates;
        }

        /// <summary>True when the zone has a completion gate and everything it waits for is done. A completed
        /// zone no longer fires; without any gate this is always false.</summary>
        public bool IsCompleted
        {
            get
            {
                bool gated = false;
                if (this.completeWhenSocket != null)
                {
                    gated = true;
                    if (!this.completeWhenSocket.IsCompleted) return false;
                }
                if (this.completeWhenPuzzle is IPuzzle puzzle)
                {
                    gated = true;
                    if (this.puzzleStates == null || !this.puzzleStates.IsSolved(puzzle.PuzzleId)) return false;
                }
                return gated;
            }
        }

        /// <summary>True when the world position is inside the polygon and within the height range.</summary>
        public bool Contains(Vector3 worldPosition)
        {
            var shape = this.ActiveShape;
            var local = this.ToLocal(worldPosition);
            if (local.y < shape.MinHeight || local.y > shape.MaxHeight) return false;
            return CameraZonePolygon.Contains(shape.Polygon, new Vector2(local.x, local.z));
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
            // The player point, not the body: a body overlapping two zones near their border keeps
            // the one it came from "inside", so walking back would never cut back to it.
            bool inside = this.Contains(playerPosition);
            bool entered = inside && !this.playerInside;
            this.playerInside = inside;

            if (inside && (entered || this.useTwoSidedSwitch) && !this.IsCompleted) this.TryActivate(playerPosition);
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
            var shape = this.ActiveShape;
            var start = this.FrameToWorld(new Vector3(shape.DividerStart.x, 0f, shape.DividerStart.y));
            var end = this.FrameToWorld(new Vector3(shape.DividerEnd.x, 0f, shape.DividerEnd.y));
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
            this.FrameToWorld(new Vector3(point.x, height, point.y));

        /// <summary>A world position in the space the polygon is measured in (this object, or its parent).</summary>
        public Vector3 ToLocal(Vector3 world)
        {
            var frame = this.Frame;
            return frame != null ? frame.InverseTransformPoint(world) : world;
        }

        /// <summary>The space the polygon lives in: this object, or with polygonInParentSpace its parent (world
        /// when it has none).</summary>
        public Transform? Frame => this.polygonInParentSpace ? this.transform.parent : this.transform;

        public Vector3 FrameUp => this.Frame != null ? this.Frame.up : Vector3.up;
        public Vector3 FrameRight => this.Frame != null ? this.Frame.right : Vector3.right;
        public Vector3 FrameForward => this.Frame != null ? this.Frame.forward : Vector3.forward;

        private Vector3 FrameToWorld(Vector3 local)
        {
            var frame = this.Frame;
            return frame != null ? frame.TransformPoint(local) : local;
        }

        /// <summary>The side walls of the extruded polygon, one world-space quad per edge.</summary>
        public void GetWallQuads(List<Vector3[]> quads)
        {
            var shape = this.ActiveShape;
            int count = shape.Polygon.Length;
            for (int i = 0; i < count && count >= 3; i++)
            {
                var a = shape.Polygon[i];
                var b = shape.Polygon[(i + 1) % count];
                quads.Add(new[]
                {
                    this.ToWorld(a, shape.MinHeight), this.ToWorld(b, shape.MinHeight),
                    this.ToWorld(b, shape.MaxHeight), this.ToWorld(a, shape.MaxHeight),
                });
            }
        }

#if UNITY_EDITOR
        /// <summary>Floor and ceiling of the extruded polygon as world-space triangles.</summary>
        public void GetCapTriangles(List<Vector3[]> triangles)
        {
            var shape = this.ActiveShape;
            var indices = CameraZonePolygon.Triangulate(shape.Polygon);
            foreach (float height in new[] { shape.MinHeight, shape.MaxHeight })
            {
                for (int i = 0; i + 2 < indices.Count; i += 3)
                {
                    triangles.Add(new[]
                    {
                        this.ToWorld(shape.Polygon[indices[i]], height),
                        this.ToWorld(shape.Polygon[indices[i + 1]], height),
                        this.ToWorld(shape.Polygon[indices[i + 2]], height),
                    });
                }
            }
        }

#endif

        /// <summary>A vertical world-space quad along the divider, pushed <paramref name="offset"/> world units
        /// along its positive normal. Offset 0 is the divider itself; ±sideMargin are the thresholds.</summary>
        public Vector3[] GetDividerQuad(float offset)
        {
            var shape = this.ActiveShape;
            var start = this.ToWorld(shape.DividerStart, shape.MinHeight);
            var end = this.ToWorld(shape.DividerEnd, shape.MinHeight);
            var direction = new Vector2(end.x - start.x, end.z - start.z);
            var normal = direction.sqrMagnitude < 1e-8f ? Vector2.zero : new Vector2(-direction.y, direction.x).normalized;
            var push = new Vector3(normal.x, 0f, normal.y) * offset;
            var rise = this.FrameUp * (shape.MaxHeight - shape.MinHeight);
            return new[] { start + push, end + push, end + push + rise, start + push + rise };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (this.completeWhenPuzzle != null && this.completeWhenPuzzle is not IPuzzle)
            {
                Debug.LogWarning($"[FixedCameraZoneTrigger] '{this.completeWhenPuzzle.name}' does not implement IPuzzle; the completion gate is ignored.", this);
                this.completeWhenPuzzle = null;
            }
        }

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
            var shape = this.ActiveShape;
            if (shape.Polygon == null || shape.Polygon.Length < 3) return;

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
            foreach (var point in shape.Polygon) center += this.ToWorld(point, (shape.MinHeight + shape.MaxHeight) * 0.5f);
            center /= shape.Polygon.Length;

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
