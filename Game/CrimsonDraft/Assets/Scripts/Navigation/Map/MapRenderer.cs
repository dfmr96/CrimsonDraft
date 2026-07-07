#nullable enable

using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Navigation.Map
{
    /// <summary>Builds map meshes on a hidden layer and films them with an ortho camera
    /// into a RenderTexture. Reads MapData + registries only.</summary>
    public sealed class MapRenderer : MonoBehaviour
    {
        [SerializeField] private Camera mapCamera = null!;
        [SerializeField] private CinemachineCamera mapVirtualCamera = null!;
        [SerializeField] private Transform contentRoot = null!;
        [SerializeField] private int renderLayer = 30;

        [Header("Room materials")]
        [SerializeField] private Material roomVisitedMaterial = null!;
        [SerializeField] private Material roomNotVisitedMaterial = null!;
        [SerializeField] private Material roomCompletedMaterial = null!;
        [SerializeField] private Material currentRoomMaterial = null!;
        [SerializeField] private Material wallMaterial = null!;
        [SerializeField] private float wallWidth = 0.12f;

        [Header("Door materials")]
        [SerializeField] private Material doorUnknownMaterial = null!;
        [SerializeField] private Material doorLockedMaterial = null!;
        [SerializeField] private Material doorUnlockedMaterial = null!;
        [Tooltip("Scales every door's authored Size when drawing it on the map, so doors read " +
                 "clearly without having to touch each MapDoorMarker's Size individually.")]
        [SerializeField] private float doorSizeMultiplier = 2.5f;

        [Header("Current room pulse")]
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseMin = 0.55f;

        [Header("Zoom")]
        [Tooltip("Multipliers applied to the map's base orthographic size. CycleZoom() steps " +
                 "through these in order and wraps around; index 0 is the default on deck open.")]
        [SerializeField] private float[] zoomLevels = { 1f, 1.6f, 0.6f };

        private RoomStateRegistry rooms = null!;
        private KnownMapsRegistry knownMaps = null!;
        private PickupRegistry pickups = null!;
        private DoorStateRegistry doorStates = null!;

        private RenderTexture? texture;
        private MapData? currentMap;
        private Renderer? currentRoomRenderer;
        private bool initialized;
        private int zoomIndex;

        private const float RoomHeight = 0f;
        private const float WallHeight = 0.2f;
        private const float DoorHeight = 0.3f;
        private const float CameraHeight = 10f;

        public RenderTexture? Texture => this.texture;

        [Inject]
        public void Construct(
            RoomStateRegistry rooms,
            KnownMapsRegistry knownMaps,
            PickupRegistry pickups,
            DoorStateRegistry doorStates)
        {
            this.rooms = rooms;
            this.knownMaps = knownMaps;
            this.pickups = pickups;
            this.doorStates = doorStates;
        }

        private void Awake()
        {
            EnsureInitialized();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (this.texture != null)
                Destroy(this.texture);
        }

        private void Update()
        {
            if (this.currentRoomRenderer == null)
                return;

            float t = Mathf.Lerp(
                this.pulseMin,
                1f,
                (Mathf.Sin(Time.unscaledTime * this.pulseSpeed) + 1f) * 0.5f);

            var material = this.currentRoomRenderer.material;
            var c = material.color;
            material.color = new Color(c.r, c.g, c.b, t);
        }

        // Map camera has its own CinemachineBrain isolated on OutputChannels.Channel01, so it
        // never competes with the per-room follow cameras on the Default channel (and vice
        // versa). Priority is set on top of that isolation for when multiple vcams eventually
        // share this channel (e.g. one per deck).
        private static readonly PrioritySettings ActivePriority   = new() { Enabled = true, Value = 100 };
        private static readonly PrioritySettings InactivePriority = new() { Enabled = true, Value = 0 };

        public void SetVisible(bool visible)
        {
            EnsureInitialized();
            if (this.mapCamera != null)
                this.mapCamera.gameObject.SetActive(visible);

            if (this.mapVirtualCamera != null)
            {
                this.mapVirtualCamera.gameObject.SetActive(visible);
                this.mapVirtualCamera.Priority = visible ? ActivePriority : InactivePriority;
            }
        }

        public void Generate(MapData map, string? currentRoomId)
        {
            EnsureInitialized();

            this.currentMap = map;
            this.currentRoomRenderer = null;
            ClearContent();

            bool deckKnown = MapStateResolver.IsDeckKnown(map, this.rooms, this.knownMaps);
            var drawnDoorIds = new HashSet<string>();

            foreach (var room in map.Rooms)
            {
                var roomState = this.rooms.GetState(room.RoomId);
                bool isCurrentRoom = currentRoomId != null && room.RoomId == currentRoomId;
                bool isCompleted = roomState == RoomMapState.Visited && AreAllPickupsCollected(room.PickupIds);

                var visualState = MapStateResolver.ResolveRoom(
                    hasMap: deckKnown,
                    roomState: roomState,
                    isCurrentRoom: isCurrentRoom,
                    isCompleted: isCompleted);

                if (visualState == MapRoomVisualState.Hidden)
                    continue;

                var roomRenderer = BuildRoomMesh(room, ResolveRoomMaterial(visualState));
                if (isCurrentRoom)
                    this.currentRoomRenderer = roomRenderer;

                BuildOutline(room);

                foreach (var id in room.DoorIds)
                    drawnDoorIds.Add(id);
            }

            foreach (var door in map.Doors)
            {
                if (!drawnDoorIds.Contains(door.DoorId))
                    continue;

                BuildDoorMesh(door);
            }

            CenterCamera(map);
        }

        public void CycleZoom()
        {
            if (this.currentMap == null || this.zoomLevels.Length == 0)
                return;

            this.zoomIndex = (this.zoomIndex + 1) % this.zoomLevels.Length;
            ApplyZoom();
        }

        public void Pan(Vector2 delta)
        {
            EnsureInitialized();
            if (this.currentMap == null)
                return;

            var pos = this.mapVirtualCamera.transform.position + new Vector3(delta.x, 0f, delta.y);
            var center = this.currentMap.Center;
            float halfW = this.currentMap.GridSize.x * this.currentMap.CellSize * 0.5f;
            float halfH = this.currentMap.GridSize.y * this.currentMap.CellSize * 0.5f;
            pos.x = Mathf.Clamp(pos.x, center.x - halfW, center.x + halfW);
            pos.z = Mathf.Clamp(pos.z, center.y - halfH, center.y + halfH);
            this.mapVirtualCamera.transform.position = pos;
        }

        private void EnsureInitialized()
        {
            if (this.initialized)
                return;

            if (this.mapCamera == null || this.contentRoot == null)
                return;

            this.texture = new RenderTexture(Screen.width, Screen.height, 16)
            {
                name = $"{nameof(MapRenderer)}_RT"
            };

            this.mapCamera.targetTexture = this.texture;
            this.mapCamera.orthographic = true;
            this.mapCamera.cullingMask = 1 << this.renderLayer;
            this.initialized = true;
        }

        private void ClearContent()
        {
            for (int i = this.contentRoot.childCount - 1; i >= 0; i--)
                DestroyImmediate(this.contentRoot.GetChild(i).gameObject);
        }

        private bool AreAllPickupsCollected(string[] pickupIds)
        {
            if (pickupIds.Length == 0)
                return true;

            foreach (var pickupId in pickupIds)
            {
                if (!this.pickups.IsCollected(pickupId))
                    return false;
            }

            return true;
        }

        private Material ResolveRoomMaterial(MapRoomVisualState state)
            => state switch
            {
                MapRoomVisualState.NotVisited => this.roomNotVisitedMaterial,
                MapRoomVisualState.Visited => this.roomVisitedMaterial,
                MapRoomVisualState.Completed => this.roomCompletedMaterial,
                MapRoomVisualState.Current => this.currentRoomMaterial,
                _ => this.roomVisitedMaterial,
            };

        // Rotation sign matches MapEditorWindow's on-screen convention (which is what level
        // design actually looks at while authoring): rotating a shape by MapRotation must
        // look the same way in-game as it does in the Map Editor Window.
        private static Matrix4x4 TRS(MapElementTransform t, float height)
            => Matrix4x4.TRS(
                new Vector3(t.Offset.x, height + t.ZOrder * 0.01f, t.Offset.y),
                Quaternion.Euler(0f, t.Rotation, 0f),
                new Vector3(t.Scale.x, 1f, t.Scale.y));

        private Renderer BuildRoomMesh(MapRoomData room, Material material)
        {
            var tris = PolygonTriangulator.Triangulate(room.Polygon);
            var verts = new Vector3[room.Polygon.Length];
            var trs = TRS(room.Transform, RoomHeight);

            for (int i = 0; i < verts.Length; i++)
                verts[i] = trs.MultiplyPoint3x4(new Vector3(room.Polygon[i].x, 0f, room.Polygon[i].y));

            return CreateMeshObject($"Room_{room.RoomId}", verts, tris, material);
        }

        private void BuildOutline(MapRoomData room)
        {
            var poly = room.Polygon;
            var trs = TRS(room.Transform, WallHeight);
            var verts = new List<Vector3>();
            var tris = new List<int>();

            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Length];
                Vector2 dir = (b - a).normalized;
                Vector2 normal = new(-dir.y, dir.x);
                Vector2 half = normal * (this.wallWidth * 0.5f);

                int baseIdx = verts.Count;
                verts.Add(trs.MultiplyPoint3x4(new Vector3(a.x - half.x, 0f, a.y - half.y)));
                verts.Add(trs.MultiplyPoint3x4(new Vector3(a.x + half.x, 0f, a.y + half.y)));
                verts.Add(trs.MultiplyPoint3x4(new Vector3(b.x + half.x, 0f, b.y + half.y)));
                verts.Add(trs.MultiplyPoint3x4(new Vector3(b.x - half.x, 0f, b.y - half.y)));
                tris.AddRange(new[] { baseIdx, baseIdx + 2, baseIdx + 1, baseIdx, baseIdx + 3, baseIdx + 2 });
            }

            CreateMeshObject($"Walls_{room.RoomId}", verts.ToArray(), tris.ToArray(), this.wallMaterial);
        }

        private void BuildDoorMesh(MapDoorData door)
        {
            var state = this.doorStates.GetMapState(door.DoorId);
            var material = state switch
            {
                DoorMapState.Locked => this.doorLockedMaterial,
                DoorMapState.Unlocked => this.doorUnlockedMaterial,
                _ => this.doorUnknownMaterial,
            };

            var trs = TRS(door.Transform, DoorHeight);
            Vector2 half = door.Size * this.doorSizeMultiplier * 0.5f;
            var verts = new[]
            {
                trs.MultiplyPoint3x4(new Vector3(-half.x, 0f, -half.y)),
                trs.MultiplyPoint3x4(new Vector3( half.x, 0f, -half.y)),
                trs.MultiplyPoint3x4(new Vector3( half.x, 0f,  half.y)),
                trs.MultiplyPoint3x4(new Vector3(-half.x, 0f,  half.y)),
            };

            CreateMeshObject($"Door_{door.DoorId}", verts, new[] { 0, 2, 1, 0, 3, 2 }, material);
        }

        private Renderer CreateMeshObject(string name, Vector3[] verts, int[] tris, Material material)
        {
            var go = new GameObject(name) { layer = this.renderLayer };
            go.transform.SetParent(this.contentRoot, false);

            var mesh = new Mesh
            {
                vertices = verts,
                triangles = tris,
                name = name + "_Mesh",
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return meshRenderer;
        }

        private void CenterCamera(MapData map)
        {
            this.mapVirtualCamera.transform.position = new Vector3(map.Center.x, CameraHeight, map.Center.y);
            this.mapVirtualCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            this.zoomIndex = 0;
            ApplyZoom();
        }

        private void ApplyZoom()
        {
            if (this.currentMap == null)
                return;

            float zoom = this.zoomLevels.Length > 0 ? this.zoomLevels[this.zoomIndex] : 1f;

            var lens = this.mapVirtualCamera.Lens;
            lens.ModeOverride     = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = this.currentMap.GridSize.y * this.currentMap.CellSize * 0.5f * zoom;
            this.mapVirtualCamera.Lens = lens;
        }
    }
}
