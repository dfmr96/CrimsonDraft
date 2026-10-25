#nullable enable

using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.CamaraSystem
{
    /// <summary>Camera zone shaped as a polygon on the XZ floor plane of the parent (the room), extruded
    /// between <c>minHeight</c> and <c>maxHeight</c>. Measuring in the parent means moving or turning the
    /// camera this lives on never moves where it activates. There is no collider:
    /// <see cref="FixedCameraZoneBootstrap"/> feeds the player position to <see cref="Evaluate"/> every frame.</summary>
    public sealed class FixedCameraZoneTrigger : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera zoneCamera = null!;

        [Tooltip("Zone outline as (x, z) points in the parent's space. At least three.")]
        [SerializeField] private Vector2[] polygon =
        {
            new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f),
        };
        [Tooltip("Y range in the parent's space the player must be in, so a zone does not fire from another level.")]
        [SerializeField] private float minHeight;
        [SerializeField] private float maxHeight = 3f;

        [Header("Completion gate (optional)")]
        [Tooltip("Once every socket / puzzle referenced here is complete, this zone stops cutting to its camera. A camera already showing stays until the player leaves the zone.")]
        [SerializeField] private ItemSocketInteractable? completeWhenSocket;
        [Tooltip("Component implementing IPuzzle (PuzzleInteractable, GeneratorSwitchPanel, MusicBoxPanel...). Complete once it is solved.")]
        [SerializeField] private MonoBehaviour? completeWhenPuzzle;

        private IFixedCameraZoneService? zoneService;
        private PuzzleStateRegistry? puzzleStates;
        private bool playerInside;

        public CinemachineCamera ZoneCamera => this.zoneCamera;
        public IReadOnlyList<Vector2> Polygon => this.polygon;
        public float MinHeight => this.minHeight;
        public float MaxHeight => this.maxHeight;
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

        /// <summary>Every camera this trigger can switch to.</summary>
        public IEnumerable<CinemachineCamera> ReferencedCameras
        {
            get
            {
                if (this.zoneCamera != null) yield return this.zoneCamera;
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
            var local = this.ToLocal(worldPosition);
            if (local.y < this.minHeight || local.y > this.maxHeight) return false;
            return CameraZonePolygon.Contains(this.polygon, new Vector2(local.x, local.z));
        }

        /// <summary>Called every frame with the player position. Fires on entering the polygon.</summary>
        public void Evaluate(Vector3 playerPosition)
        {
            // The player point, not the body: a body overlapping two zones near their border keeps
            // the one it came from "inside", so walking back would never cut back to it.
            bool inside = this.Contains(playerPosition);
            bool entered = inside && !this.playerInside;
            this.playerInside = inside;

            if (entered && !this.IsCompleted) this.TryActivate();
        }

        private void OnDisable()
        {
            // A room being hidden must re-fire its zone when the player lands in it again.
            this.playerInside = false;
        }

        private void TryActivate()
        {
            if (this.zoneService == null || this.zoneCamera == null) return;
            if (this.zoneCamera == this.zoneService.CurrentZoneCamera && this.zoneCamera.enabled) return;

            this.zoneService.ActivateZone(this.zoneCamera);
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

        /// <summary>A world position in the space the polygon is measured in (the parent).</summary>
        public Vector3 ToLocal(Vector3 world)
        {
            var frame = this.Frame;
            return frame != null ? frame.InverseTransformPoint(world) : world;
        }

        /// <summary>The space the polygon lives in: the parent (world when there is none).</summary>
        public Transform? Frame => this.transform.parent;

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
        }

        /// <summary>Zone gizmos only show for the room being worked on: when its RoomController is selected,
        /// or while the zone itself is (to keep seeing it while editing it).</summary>
        public static bool ShouldDrawGizmo(FixedCameraZoneTrigger zone, IEnumerable<GameObject> selection)
        {
            var room = zone.GetComponentInParent<CrimsonDraft.Navigation.Rooms.RoomController>(true);
            foreach (var selected in selection)
            {
                if (selected == null) continue;
                if (selected == zone.gameObject) return true;
                if (room != null && selected == room.gameObject) return true;
            }
            return false;
        }

        // The floor plane of the zone: filled polygon and its outline, no walls, ceiling or name.
        private void OnDrawGizmos()
        {
            if (this.polygon == null || this.polygon.Length < 3) return;
            if (!ShouldDrawGizmo(this, UnityEditor.Selection.gameObjects)) return;

            var color = GizmoColorFor(this.zoneCamera);
            bool emphasized = UnityEditor.Selection.Contains(this.gameObject)
                || (GizmoHighlightCamera != null && this.zoneCamera == GizmoHighlightCamera);

            var floor = new Vector3[this.polygon.Length];
            for (int i = 0; i < floor.Length; i++) floor[i] = this.ToWorld(this.polygon[i], this.minHeight);

            UnityEditor.Handles.color = new Color(color.r, color.g, color.b, emphasized ? 0.3f : 0.15f);
            var triangles = CameraZonePolygon.Triangulate(this.polygon);
            for (int i = 0; i + 2 < triangles.Count; i += 3)
                UnityEditor.Handles.DrawAAConvexPolygon(floor[triangles[i]], floor[triangles[i + 1]], floor[triangles[i + 2]]);

            Gizmos.color = color;
            for (int i = 0; i < floor.Length; i++) Gizmos.DrawLine(floor[i], floor[(i + 1) % floor.Length]);
        }
#endif
    }
}
