#nullable enable

using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Looks through a room's fixed cameras without entering Play Mode. The room picker
    /// lists the RoomControllers of the open scene; the camera list holds the cameras that room's
    /// FixedCameraZoneTriggers activate. The view shows every trigger volume of the room, so it is
    /// easy to see whether the player can reach another camera's trigger while still in frame.
    /// By default it only looks; the Move Camera toggle turns the view into a free-fly control
    /// for the selected camera (see HandleCameraControls).
    /// The camera is rendered to a texture from EditorApplication.update, never inside OnGUI
    /// (rendering a URP camera from a GUI event corrupts the GUIClip stack), and the trigger
    /// volumes are projected on top in GUI space.</summary>
    public sealed class CameraZoneViewerWindow : EditorWindow
    {
        private const float ListWidth = 190f;
        private const double RenderInterval = 0.1;
        private const float PlayerMarkerRadius = 0.1f;
        private const float PlayerDotRadius = 0.03f;
        private const int PlayerMarkerSegments = 24;

        private RoomController[] rooms = System.Array.Empty<RoomController>();
        private CinemachineCamera[] cameras = System.Array.Empty<CinemachineCamera>();
        private int roomIndex;
        private int cameraIndex;
        private Vector2 listScroll;
        private Camera? viewCamera;
        private RenderTexture? viewTexture;
        private Vector2Int viewPixels;
        private double lastRenderTime;
        private bool unlit = true;
        private bool? legacySpawnsLeft;
        private bool cameraMode;
        private int cameraDragButton = -1;
        private bool fastFly;
        private double lastFlightTime;
        private readonly HashSet<KeyCode> heldKeys = new HashSet<KeyCode>();
        private PlayerController? player;
        private float planeY;

        [MenuItem("CrimsonDraft/Camera Zone Viewer")]
        private static void Open()
        {
            var window = GetWindow<CameraZoneViewerWindow>("Camera Zone Viewer");
            window.minSize = new Vector2(480f, 300f);
            window.RefreshRooms();
        }

        private void OnEnable()
        {
            this.RefreshRooms();
            EditorApplication.hierarchyChanged += this.OnHierarchyChanged;
            EditorApplication.update += this.OnEditorUpdate;
            EditorApplication.projectChanged += this.OnProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= this.OnHierarchyChanged;
            EditorApplication.update -= this.OnEditorUpdate;
            EditorApplication.projectChanged -= this.OnProjectChanged;
            FixedCameraZoneTrigger.GizmoHighlightCamera = null;
            this.DestroyViewCamera();
            this.ReleaseTexture();
            SceneView.RepaintAll();
        }

        private void OnProjectChanged() => this.legacySpawnsLeft = null;

        private void OnHierarchyChanged()
        {
            this.RefreshRooms();
            this.Repaint();
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;

            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min((float)(now - this.lastFlightTime), 0.1f);
            this.lastFlightTime = now;
            this.ApplyFlight(dt);

            if (now - this.lastRenderTime < RenderInterval) return;
            this.lastRenderTime = EditorApplication.timeSinceStartup;

            var camera = this.CurrentCamera;
            if (camera == null || this.viewPixels.x < 16 || this.viewPixels.y < 16) return;

            this.RenderView(camera);
            this.Repaint();
        }

        private void RefreshRooms()
        {
            var previousRoom = this.roomIndex < this.rooms.Length ? this.rooms[this.roomIndex] : null;
            var previousCamera = this.cameraIndex < this.cameras.Length ? this.cameras[this.cameraIndex] : null;

            this.rooms = FindObjectsByType<RoomController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(r => r.name).ToArray();
            this.roomIndex = previousRoom != null ? System.Array.IndexOf(this.rooms, previousRoom) : 0;
            if (this.roomIndex < 0) this.roomIndex = 0;

            this.player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

            this.RefreshCameras();
            if (previousCamera != null)
            {
                int index = System.Array.IndexOf(this.cameras, previousCamera);
                if (index >= 0) this.cameraIndex = index;
            }
        }

        private void RefreshCameras()
        {
            var list = new List<CinemachineCamera>();
            var room = this.CurrentRoom;
            if (room != null)
            {
                foreach (var trigger in room.GetComponentsInChildren<FixedCameraZoneTrigger>(includeInactive: true))
                {
                    foreach (var camera in trigger.ReferencedCameras)
                        if (camera != null && !list.Contains(camera)) list.Add(camera);
                }
            }
            this.cameras = list.OrderBy(c => c.name).ToArray();
            this.cameraIndex = Mathf.Clamp(this.cameraIndex, 0, Mathf.Max(0, this.cameras.Length - 1));
        }

        // Unity null checks: a room or camera destroyed under the window (scene reload, tests) reads as null.
        private RoomController? CurrentRoom =>
            this.roomIndex >= 0 && this.roomIndex < this.rooms.Length && this.rooms[this.roomIndex] != null
                ? this.rooms[this.roomIndex] : null;

        private CinemachineCamera? CurrentCamera =>
            this.cameraIndex >= 0 && this.cameraIndex < this.cameras.Length && this.cameras[this.cameraIndex] != null
                ? this.cameras[this.cameraIndex] : null;

        private bool HasDestroyedReferences =>
            this.rooms.Any(r => r == null) || this.cameras.Any(c => c == null)
            || (!ReferenceEquals(this.player, null) && this.player == null);

        private void OnGUI()
        {
            if (this.HasDestroyedReferences) this.RefreshRooms();
            this.TrackFlightKeys();
            this.DrawToolbar();

            var room = this.CurrentRoom;
            if (room == null)
            {
                EditorGUILayout.HelpBox("No RoomController found in the open scene.", MessageType.Info);
                return;
            }
            if (this.cameras.Length == 0)
            {
                EditorGUILayout.HelpBox("This room has no FixedCameraZoneTrigger with a camera assigned.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                this.DrawCameraList();
                this.DrawView(room);
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Room", GUILayout.Width(38f));
                var names = this.rooms.Select(r => r.name).ToArray();
                int newRoom = EditorGUILayout.Popup(this.roomIndex, names, EditorStyles.toolbarPopup, GUILayout.Width(180f));
                if (newRoom != this.roomIndex)
                {
                    this.roomIndex = newRoom;
                    this.cameraIndex = 0;
                    this.RefreshCameras();
                }

                this.unlit = GUILayout.Toggle(this.unlit, "Unlit", EditorStyles.toolbarButton, GUILayout.Width(48f));
                this.DrawArrivalControls();
                this.cameraMode = GUILayout.Toggle(this.cameraMode, "Move Camera", EditorStyles.toolbarButton, GUILayout.Width(88f));
                if (!this.cameraMode) this.StopFlying();
                this.DrawFovField();

                // Zones that still use their BoxCollider work, but can not be edited until converted.
                var room = this.CurrentRoom;
                var legacy = room == null ? System.Array.Empty<FixedCameraZoneTrigger>()
                    : room.GetComponentsInChildren<FixedCameraZoneTrigger>(includeInactive: true).Where(t => t.IsLegacyBox).ToArray();
                if (legacy.Length > 0 && GUILayout.Button($"Convert {legacy.Length} box zone(s)", EditorStyles.toolbarButton))
                {
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName("Convert Camera Zones");
                    foreach (var trigger in legacy) CameraZoneBoxMigration.Convert(trigger, useUndo: true);
                    SceneView.RepaintAll();
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(this.cameras.Length == 0))
                {
                    if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(28f))) this.Step(-1);
                    GUILayout.Label(this.CurrentCamera != null
                        ? $"{this.CurrentCamera.name}  ({this.cameraIndex + 1}/{this.cameras.Length})" : "-",
                        GUILayout.MinWidth(120f));
                    if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(28f))) this.Step(1);
                }
            }
        }

        private void Step(int delta)
        {
            if (this.cameras.Length == 0) return;
            this.cameraIndex = (this.cameraIndex + delta + this.cameras.Length) % this.cameras.Length;
            this.lastRenderTime = 0;
        }

        private void DrawCameraList()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(this.listScroll, GUILayout.Width(ListWidth)))
            {
                this.listScroll = scroll.scrollPosition;
                for (int i = 0; i < this.cameras.Length; i++)
                {
                    var rect = EditorGUILayout.GetControlRect(false, 20f);
                    bool selected = i == this.cameraIndex;
                    if (selected) EditorGUI.DrawRect(rect, new Color(0.17f, 0.36f, 0.54f, 0.9f));
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y + 3f, 6f, rect.height - 6f),
                        FixedCameraZoneTrigger.GizmoColorFor(this.cameras[i]));
                    GUI.Label(new Rect(rect.x + 10f, rect.y, rect.width - 10f, rect.height), this.cameras[i].name);
                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        this.cameraIndex = i;
                        this.lastRenderTime = 0;
                        Event.current.Use();
                    }
                }
            }
        }

        private void DrawView(RoomController room)
        {
            var camera = this.CurrentCamera;
            var area = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            // The Layout pass returns a dummy rect; every other pass has the real one.
            if (camera == null || Event.current.type == EventType.Layout) return;

            FixedCameraZoneTrigger.GizmoHighlightCamera = camera;
            this.planeY = this.ResolvePlaneY(room);
            var view = FitToGameAspect(area);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(area, Color.black);

                float scale = EditorGUIUtility.pixelsPerPoint;
                this.viewPixels = new Vector2Int(
                    Mathf.Clamp(Mathf.RoundToInt(view.width * scale), 0, 2048),
                    Mathf.Clamp(Mathf.RoundToInt(view.height * scale), 0, 2048));

                if (this.viewTexture != null)
                    GUI.DrawTexture(view, this.viewTexture, ScaleMode.StretchToFill, false);

                if (this.viewCamera != null)
                    this.DrawTriggerOverlay(room, camera, this.viewCamera, view);

                if (!room.gameObject.activeInHierarchy)
                    EditorGUI.HelpBox(new Rect(area.x, area.y, area.width, 32f),
                        "The room is inactive, so its geometry will not render.", MessageType.Warning);
            }

            if (this.cameraMode)
                this.HandleCameraControls(camera, view, controlId);
            else if (this.viewCamera != null)
                this.EditHandles(room, this.viewCamera, view, controlId);
        }

        // ---- Zone editing handles --------------------------------------------------------

        private enum HandleKind { Vertex, Add, DividerStart, DividerEnd, Move }

        private struct ZoneHandle
        {
            public FixedCameraZoneTrigger Trigger;
            public HandleKind Kind;
            public int Index;
            public Vector3 World;
            public Vector2 Gui;
        }

        // ---- Camera fly mode -------------------------------------------------------------

        private const float LookSensitivity = 0.2f;
        private const float PanSensitivity = 0.005f;
        private const float DollyPerScroll = 0.2f;
        private const float FlySpeed = 3f;
        private const float FastMultiplier = 3f;

        // Puts every door arrival of the room back in front of its door, back to it.
        private void DrawArrivalControls()
        {
            var room = this.CurrentRoom;
            using (new EditorGUI.DisabledScope(room == null))
            {
                if (GUILayout.Button(new GUIContent("Reset Arrivals", "Put every door arrival of this room back in front of its door at the distance on the right."),
                        EditorStyles.toolbarButton, GUILayout.Width(90f)) && room != null)
                {
                    int count = DoorArrivalSettings.ResetRoom(room);
                    Debug.Log($"[CameraZoneViewer] Reset {count} door arrival(s) in '{room.name}' at {DoorArrivalSettings.Distance:0.##} m.");
                    SceneView.RepaintAll();
                }
            }
            DoorArrivalSettings.Distance = EditorGUILayout.FloatField(DoorArrivalSettings.Distance, EditorStyles.toolbarTextField, GUILayout.Width(34f));

            // One-shot conversion of the old spawn points; hidden once none is left, so a second click
            // can never reset hand-tuned arrivals.
            this.legacySpawnsLeft ??= SpawnPointMigration.HasLegacySpawns();
            if (this.legacySpawnsLeft == true
                && GUILayout.Button(new GUIContent("Migrate Doors", SpawnPointMigration.Explanation), EditorStyles.toolbarButton, GUILayout.Width(88f)))
                EditorApplication.delayCall += SpawnPointMigration.MigrateWithConfirmation;
        }

        private void DrawFovField()
        {
            var camera = this.CurrentCamera;
            if (!this.cameraMode || camera == null) return;

            GUILayout.Label("FOV", GUILayout.Width(28f));
            var lens = camera.Lens;
            EditorGUI.BeginChangeCheck();
            float fov = EditorGUILayout.FloatField(lens.FieldOfView, EditorStyles.toolbarTextField, GUILayout.Width(44f));
            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(camera, "Change Camera FOV");
            lens.FieldOfView = Mathf.Clamp(fov, 1f, 170f);
            camera.Lens = lens;
            EditorUtility.SetDirty(camera);
            this.lastRenderTime = 0;
        }

        /// <summary>Right mouse drag looks around, middle drag pans, the wheel dollies, and while the right
        /// button is held WASD / Q E fly (Shift is faster). Everything edits the selected camera transform
        /// with undo.</summary>
        private void HandleCameraControls(CinemachineCamera camera, Rect rect, int controlId)
        {
            var e = Event.current;
            switch (e.GetTypeForControl(controlId))
            {
                case EventType.Repaint:
                    EditorGUI.LabelField(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 18f),
                        "Camera mode: RMB look + WASD/QE fly (Shift faster)  |  MMB pan  |  wheel dolly",
                        EditorStyles.whiteMiniLabel);
                    break;

                case EventType.MouseDown:
                    if ((e.button != 1 && e.button != 2) || !rect.Contains(e.mousePosition)) break;
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName("Move Camera");
                    this.cameraDragButton = e.button;
                    this.heldKeys.Clear();
                    this.lastFlightTime = EditorApplication.timeSinceStartup;
                    GUIUtility.hotControl = controlId;
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId) break;
                    this.DragCamera(camera, e);
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId) break;
                    GUIUtility.hotControl = 0;
                    this.StopFlying();
                    e.Use();
                    break;

                case EventType.ScrollWheel:
                    if (!rect.Contains(e.mousePosition)) break;
                    Undo.RecordObject(camera.transform, "Move Camera");
                    camera.transform.position += camera.transform.forward * (-e.delta.y * DollyPerScroll);
                    this.CameraMoved();
                    e.Use();
                    break;
            }
        }

        private void DragCamera(CinemachineCamera camera, Event e)
        {
            var transform = camera.transform;
            Undo.RecordObject(transform, "Move Camera");

            if (this.cameraDragButton == 1)
            {
                var euler = transform.eulerAngles;
                float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
                pitch = Mathf.Clamp(pitch + e.delta.y * LookSensitivity, -89f, 89f);
                transform.rotation = Quaternion.Euler(pitch, euler.y + e.delta.x * LookSensitivity, 0f);
            }
            else if (this.cameraDragButton == 2)
            {
                transform.position += (-transform.right * e.delta.x + transform.up * e.delta.y) * PanSensitivity;
            }
            this.CameraMoved();
        }

        // Keys only matter while flying (right button held). They are tracked from GUI events and
        // applied from EditorApplication.update, so movement is smooth and frame-rate independent.
        private void TrackFlightKeys()
        {
            var e = Event.current;
            if (!this.cameraMode || this.cameraDragButton != 1) return;
            if (e.type != EventType.KeyDown && e.type != EventType.KeyUp) return;

            this.fastFly = e.shift;
            if (!IsFlightKey(e.keyCode)) return;

            if (e.type == EventType.KeyDown) this.heldKeys.Add(e.keyCode);
            else this.heldKeys.Remove(e.keyCode);
            e.Use();
        }

        private static bool IsFlightKey(KeyCode key) =>
            key == KeyCode.W || key == KeyCode.A || key == KeyCode.S || key == KeyCode.D
            || key == KeyCode.Q || key == KeyCode.E;

        private void ApplyFlight(float dt)
        {
            var camera = this.CurrentCamera;
            if (!this.cameraMode || this.cameraDragButton != 1 || camera == null || this.heldKeys.Count == 0) return;

            var transform = camera.transform;
            var direction = Vector3.zero;
            if (this.heldKeys.Contains(KeyCode.W)) direction += transform.forward;
            if (this.heldKeys.Contains(KeyCode.S)) direction -= transform.forward;
            if (this.heldKeys.Contains(KeyCode.D)) direction += transform.right;
            if (this.heldKeys.Contains(KeyCode.A)) direction -= transform.right;
            if (this.heldKeys.Contains(KeyCode.E)) direction += Vector3.up;
            if (this.heldKeys.Contains(KeyCode.Q)) direction -= Vector3.up;
            if (direction == Vector3.zero) return;

            Undo.RecordObject(transform, "Move Camera");
            transform.position += direction.normalized * (FlySpeed * (this.fastFly ? FastMultiplier : 1f) * dt);
            this.CameraMoved();
            this.Repaint();
        }

        private void StopFlying()
        {
            this.cameraDragButton = -1;
            this.heldKeys.Clear();
        }

        private void CameraMoved()
        {
            this.lastRenderTime = 0;
            SceneView.RepaintAll();
            this.Repaint();
        }

        private ZoneHandle? dragging;
        private Vector2 lastDragPoint;

        /// <summary>Same handles as the Scene view, on top of the camera view: drag a white vertex or a
        /// yellow divider end, drag the orange square to move the whole zone, click a green dot to split
        /// an edge, Ctrl+click a vertex to delete it. Drags move the point on the zone's floor plane
        /// under the mouse.</summary>
        private void EditHandles(RoomController room, Camera view, Rect rect, int controlId)
        {
            view.aspect = rect.width / rect.height;
            var handles = this.CollectHandles(room, view, rect);
            var e = Event.current;

            switch (e.GetTypeForControl(controlId))
            {
                case EventType.Repaint:
                    foreach (var handle in handles) DrawHandle(handle);
                    break;

                case EventType.MouseDown:
                    if (e.button != 0 || !rect.Contains(e.mousePosition)) break;
                    var hit = Pick(handles, e.mousePosition);
                    if (hit == null) break;

                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName("Edit Camera Zone");
                    var picked = hit.Value;
                    if (picked.Kind == HandleKind.Vertex && e.control)
                    {
                        RemoveVertex(picked);
                    }
                    else
                    {
                        if (picked.Kind == HandleKind.Add) picked = InsertVertex(picked);
                        if (picked.Kind == HandleKind.Move)
                            TryGetFloorPoint(picked, e.mousePosition, view, rect, out this.lastDragPoint);
                        this.dragging = picked;
                        GUIUtility.hotControl = controlId;
                    }
                    e.Use();
                    this.Repaint();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId || this.dragging == null) break;
                    this.MoveHandle(this.dragging.Value, e.mousePosition, view, rect);
                    e.Use();
                    this.Repaint();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId) break;
                    GUIUtility.hotControl = 0;
                    this.dragging = null;
                    e.Use();
                    break;
            }
        }

        private List<ZoneHandle> CollectHandles(RoomController room, Camera view, Rect rect)
        {
            var handles = new List<ZoneHandle>();
            var moves = new List<ZoneHandle>();
            var adds = new List<ZoneHandle>();
            foreach (var trigger in room.GetComponentsInChildren<FixedCameraZoneTrigger>(includeInactive: false))
            {
                if (trigger.IsLegacyBox) continue; // edits would be ignored while the BoxCollider defines the zone
                var polygon = trigger.Polygon;
                float height = trigger.MinHeight;
                for (int i = 0; i < polygon.Count && polygon.Count >= 3; i++)
                {
                    TryAddHandle(handles, view, rect, trigger, HandleKind.Vertex, i, trigger.ToWorld(polygon[i], height));
                    var mid = (polygon[i] + polygon[(i + 1) % polygon.Count]) * 0.5f;
                    TryAddHandle(adds, view, rect, trigger, HandleKind.Add, i, trigger.ToWorld(mid, height));
                }
                if (polygon.Count >= 3)
                    TryAddHandle(moves, view, rect, trigger, HandleKind.Move, 0, trigger.ToWorld(trigger.Centroid, height));
                if (trigger.UseTwoSidedSwitch)
                {
                    TryAddHandle(handles, view, rect, trigger, HandleKind.DividerStart, 0, trigger.ToWorld(trigger.DividerStart, height));
                    TryAddHandle(handles, view, rect, trigger, HandleKind.DividerEnd, 0, trigger.ToWorld(trigger.DividerEnd, height));
                }
            }
            handles.AddRange(moves);
            handles.AddRange(adds);
            return handles;
        }

        private void TryAddHandle(List<ZoneHandle> list, Camera view, Rect rect,
            FixedCameraZoneTrigger trigger, HandleKind kind, int index, Vector3 world)
        {
            world = this.OnPlane(world);
            var viewPoint = view.worldToCameraMatrix.MultiplyPoint3x4(world);
            if (-viewPoint.z <= view.nearClipPlane) return;

            var local = ToGui(view.projectionMatrix, viewPoint, rect.size);
            list.Add(new ZoneHandle
            {
                Trigger = trigger, Kind = kind, Index = index, World = world,
                Gui = new Vector2(rect.x + local.x, rect.y + local.y),
            });
        }

        private static ZoneHandle? Pick(List<ZoneHandle> handles, Vector2 mouse)
        {
            // Vertices and divider ends come first in the list, then the move squares, then the "+" dots.
            foreach (var handle in handles)
            {
                float radius = handle.Kind == HandleKind.Add ? 6f : handle.Kind == HandleKind.Move ? 10f : 8f;
                if ((handle.Gui - mouse).sqrMagnitude <= radius * radius) return handle;
            }
            return null;
        }

        private static void DrawHandle(ZoneHandle handle)
        {
            float size = handle.Kind == HandleKind.Add ? 5f : handle.Kind == HandleKind.Move ? 14f : 9f;
            var rect = new Rect(handle.Gui.x - size * 0.5f, handle.Gui.y - size * 0.5f, size, size);
            var fill = handle.Kind switch
            {
                HandleKind.Add => new Color(0.4f, 1f, 0.4f),
                HandleKind.Move => new Color(1f, 0.55f, 0.1f),
                HandleKind.Vertex => Color.white,
                _ => Color.yellow,
            };
            EditorGUI.DrawRect(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), Color.black);
            EditorGUI.DrawRect(rect, fill);
        }

        /// <summary>The mouse position projected onto the handle's floor plane, as local (x, z) of its trigger.</summary>
        private static bool TryGetFloorPoint(ZoneHandle handle, Vector2 mouse, Camera view, Rect rect, out Vector2 point)
        {
            point = default;
            float u = (mouse.x - rect.x) / rect.width;
            float v = (mouse.y - rect.y) / rect.height;
            var inverse = (view.projectionMatrix * view.worldToCameraMatrix).inverse;
            var ndc = new Vector2(u * 2f - 1f, 1f - v * 2f);
            var nearPoint = inverse.MultiplyPoint(new Vector3(ndc.x, ndc.y, -1f));
            var farPoint = inverse.MultiplyPoint(new Vector3(ndc.x, ndc.y, 1f));
            var ray = new Ray(nearPoint, (farPoint - nearPoint).normalized);

            var plane = new Plane(Vector3.up, handle.World);
            if (!plane.Raycast(ray, out float distance)) return false;

            var local = handle.Trigger.transform.InverseTransformPoint(ray.GetPoint(distance));
            point = new Vector2(local.x, local.z);
            return true;
        }

        private void MoveHandle(ZoneHandle handle, Vector2 mouse, Camera view, Rect rect)
        {
            if (!TryGetFloorPoint(handle, mouse, view, rect, out var point)) return;

            if (handle.Kind == HandleKind.Move)
            {
                FixedCameraZoneTriggerEditor.Translate(new SerializedObject(handle.Trigger), point - this.lastDragPoint);
                this.lastDragPoint = point;
                SceneView.RepaintAll();
                return;
            }

            var serialized = new SerializedObject(handle.Trigger);
            switch (handle.Kind)
            {
                case HandleKind.Vertex:
                    serialized.FindProperty("polygon").GetArrayElementAtIndex(handle.Index).vector2Value = point;
                    break;
                case HandleKind.DividerStart:
                    serialized.FindProperty("dividerStart").vector2Value = point;
                    break;
                case HandleKind.DividerEnd:
                    serialized.FindProperty("dividerEnd").vector2Value = point;
                    break;
            }
            serialized.ApplyModifiedProperties();
            SceneView.RepaintAll();
        }

        /// <summary>Splits the edge after <c>Index</c> at its midpoint; returns the handle of the new vertex.</summary>
        private static ZoneHandle InsertVertex(ZoneHandle edge)
        {
            var serialized = new SerializedObject(edge.Trigger);
            var polygon = serialized.FindProperty("polygon");
            var a = polygon.GetArrayElementAtIndex(edge.Index).vector2Value;
            var b = polygon.GetArrayElementAtIndex((edge.Index + 1) % polygon.arraySize).vector2Value;

            polygon.InsertArrayElementAtIndex(edge.Index);
            polygon.GetArrayElementAtIndex(edge.Index + 1).vector2Value = (a + b) * 0.5f;
            serialized.ApplyModifiedProperties();
            SceneView.RepaintAll();

            edge.Kind = HandleKind.Vertex;
            edge.Index += 1;
            return edge;
        }

        private static void RemoveVertex(ZoneHandle vertex)
        {
            var serialized = new SerializedObject(vertex.Trigger);
            var polygon = serialized.FindProperty("polygon");
            if (polygon.arraySize <= 3) return;

            polygon.DeleteArrayElementAtIndex(vertex.Index);
            serialized.ApplyModifiedProperties();
            SceneView.RepaintAll();
        }

        /// <summary>Largest rect with the Game view's aspect ratio that fits in <paramref name="area"/>, centred.</summary>
        private static Rect FitToGameAspect(Rect area)
        {
            var size = Handles.GetMainGameViewSize();
            if (size.x <= 0f || size.y <= 0f || area.width <= 0f || area.height <= 0f) return area;

            float aspect = size.x / size.y;
            float width = Mathf.Min(area.width, area.height * aspect);
            float height = width / aspect;
            return new Rect(area.x + (area.width - width) * 0.5f, area.y + (area.height - height) * 0.5f, width, height);
        }

        private void RenderView(CinemachineCamera source)
        {
            if (this.viewTexture == null || this.viewTexture.width != this.viewPixels.x || this.viewTexture.height != this.viewPixels.y)
            {
                this.ReleaseTexture();
                this.viewTexture = new RenderTexture(this.viewPixels.x, this.viewPixels.y, 24, RenderTextureFormat.ARGB32)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            var view = this.EnsureViewCamera();
            this.CopyLens(source, view);
            view.aspect = (float)this.viewPixels.x / this.viewPixels.y;
            view.targetTexture = this.viewTexture;

            // Unlit look: URP's material debug mode "Albedo" draws surfaces without lighting.
            // The setting is global, so it is switched on only for this synchronous Render call.
            var material = UniversalRenderPipelineDebugDisplaySettings.Instance.materialSettings;
            var previousMode = material.materialDebugMode;
            if (this.unlit) material.materialDebugMode = DebugMaterialMode.Albedo;
            try { view.Render(); }
            finally { material.materialDebugMode = previousMode; }
        }

        private Camera EnsureViewCamera()
        {
            if (this.viewCamera != null) return this.viewCamera;
            var go = new GameObject("CameraZoneViewer_Camera") { hideFlags = HideFlags.HideAndDontSave };
            this.viewCamera = go.AddComponent<Camera>();
            this.viewCamera.enabled = false;
            this.viewCamera.allowHDR = false;
            this.viewCamera.allowMSAA = false;
            var urpData = go.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderPostProcessing = false;
            urpData.antialiasing = AntialiasingMode.None;
            urpData.renderShadows = false;
            return this.viewCamera;
        }

        private void DestroyViewCamera()
        {
            if (this.viewCamera != null) DestroyImmediate(this.viewCamera.gameObject);
            this.viewCamera = null;
        }

        private void ReleaseTexture()
        {
            if (this.viewTexture != null)
            {
                this.viewTexture.Release();
                DestroyImmediate(this.viewTexture);
            }
            this.viewTexture = null;
        }

        private void CopyLens(CinemachineCamera source, Camera target)
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

        // ---- Trigger overlay -------------------------------------------------------------

        private void DrawTriggerOverlay(RoomController room, CinemachineCamera current, Camera view, Rect rect)
        {
            // The texture was rendered with this aspect; keep the projection in step with the rect.
            view.aspect = rect.width / rect.height;
            var worldToView = view.worldToCameraMatrix;
            var projection = view.projectionMatrix;
            float near = view.nearClipPlane;
            var overlay = new OverlayProjection(worldToView, projection, near, rect.size);

            // Same look as the runtime DebugWorldOverlay: flat zones and the player marker, all on the
            // one plane the player's feet stand on. No walls or ceiling.
            var playerPosition = this.player != null ? this.player.transform.position : (Vector3?)null;

            GUI.BeginClip(rect);
            foreach (var trigger in room.GetComponentsInChildren<FixedCameraZoneTrigger>(includeInactive: false))
            {
                var polygon = trigger.Polygon;
                if (polygon.Count < 3) continue;

                bool emphasized = trigger.ReferencedCameras.Contains(current);
                bool playerInside = playerPosition.HasValue && trigger.Contains(playerPosition.Value);
                var color = FixedCameraZoneTrigger.GizmoColorFor(trigger.ZoneCamera);
                var lineColor = playerInside ? Color.white : color;

                var floor = new Vector3[polygon.Count];
                for (int i = 0; i < floor.Length; i++) floor[i] = this.OnPlane(trigger.ToWorld(polygon[i], trigger.MinHeight));

                var triangles = CameraZonePolygon.Triangulate(polygon);
                for (int i = 0; i + 2 < triangles.Count; i += 3)
                    overlay.Draw(new[] { floor[triangles[i]], floor[triangles[i + 1]], floor[triangles[i + 2]] },
                        color, emphasized ? 0.28f : 0.1f, 0f);

                for (int i = 0; i < floor.Length; i++)
                    overlay.DrawLine(floor[i], floor[(i + 1) % floor.Length], lineColor, emphasized ? 3f : 1.5f);

                if (trigger.UseTwoSidedSwitch)
                {
                    float margin = Mathf.Max(0f, trigger.SideMargin);
                    foreach (float offset in new[] { -margin, margin })
                    {
                        var edge = trigger.GetDividerQuad(offset);
                        overlay.DrawLine(this.OnPlane(edge[0]), this.OnPlane(edge[1]), lineColor, 2f);
                    }
                    var divider = trigger.GetDividerQuad(0f);
                    overlay.DrawLine(this.OnPlane(divider[0]), this.OnPlane(divider[1]), Color.white, 2f);
                }

                var anchor = Vector3.zero;
                foreach (var point in floor) anchor += point;
                anchor /= floor.Length;

                var anchorView = worldToView.MultiplyPoint3x4(anchor);
                if (-anchorView.z > near)
                {
                    var p = ToGui(projection, anchorView, rect.size);
                    var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = color } };
                    GUI.Label(new Rect(p.x - 80f, p.y - 8f, 160f, 16f), LabelFor(trigger), style);
                }
            }

            if (this.player != null)
            {
                var feet = this.OnPlane(this.player.FootPosition);
                overlay.Draw(Circle(feet, PlayerMarkerRadius), new Color(1f, 0.85f, 0.2f), 1f, 0f);
                overlay.Draw(Circle(feet, PlayerDotRadius), new Color(0.9f, 0.1f, 0.1f), 1f, 0f);
            }
            GUI.EndClip();
        }

        private Vector3 OnPlane(Vector3 world)
        {
            world.y = this.planeY;
            return world;
        }

        private static Vector3[] Circle(Vector3 center, float radius)
        {
            var points = new Vector3[PlayerMarkerSegments];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / points.Length;
                points[i] = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        /// <summary>Height of the plane every zone and the player marker are flattened onto: the player's
        /// feet, as in the runtime overlay. Without a player in the scene, the room's first zone floor.</summary>
        private float ResolvePlaneY(RoomController room)
        {
            if (this.player != null) return this.player.FootPosition.y;

            var trigger = room.GetComponentInChildren<FixedCameraZoneTrigger>(includeInactive: false);
            return trigger != null ? trigger.ToWorld(Vector2.zero, trigger.MinHeight).y : 0f;
        }

        private static string LabelFor(FixedCameraZoneTrigger trigger) =>
            string.Join(" | ", trigger.ReferencedCameras.Select(c => c.name));

        /// <summary>Projects world-space convex polygons to the view rect, clipped to the near plane.</summary>
        private readonly struct OverlayProjection
        {
            private readonly Matrix4x4 worldToView;
            private readonly Matrix4x4 projection;
            private readonly float near;
            private readonly Vector2 size;

            public OverlayProjection(Matrix4x4 worldToView, Matrix4x4 projection, float near, Vector2 size)
            {
                this.worldToView = worldToView;
                this.projection = projection;
                this.near = near;
                this.size = size;
            }

            public void DrawLine(Vector3 a, Vector3 b, Color color, float width)
            {
                var va = this.worldToView.MultiplyPoint3x4(a);
                var vb = this.worldToView.MultiplyPoint3x4(b);
                bool aIn = -va.z > this.near;
                bool bIn = -vb.z > this.near;
                if (!aIn && !bIn) return;

                if (!aIn) va = Vector3.Lerp(va, vb, (-this.near - va.z) / (vb.z - va.z));
                else if (!bIn) vb = Vector3.Lerp(va, vb, (-this.near - va.z) / (vb.z - va.z));

                Handles.color = color;
                Handles.DrawAAPolyLine(width, ToGui(this.projection, va, this.size), ToGui(this.projection, vb, this.size));
            }

            /// <param name="fillAlpha">0 skips the fill.</param>
            /// <param name="lineWidth">0 skips the outline.</param>
            public void Draw(Vector3[] worldPolygon, Color color, float fillAlpha, float lineWidth)
            {
                var polygon = new List<Vector3>(worldPolygon.Length);
                foreach (var point in worldPolygon) polygon.Add(this.worldToView.MultiplyPoint3x4(point));
                polygon = ClipToNear(polygon, this.near);
                if (polygon.Count < 3) return;

                var points = new Vector3[polygon.Count];
                for (int i = 0; i < points.Length; i++) points[i] = ToGui(this.projection, polygon[i], this.size);
                if (fillAlpha > 0f)
                {
                    Handles.color = new Color(color.r, color.g, color.b, fillAlpha);
                    Handles.DrawAAConvexPolygon(points);
                }
                if (lineWidth > 0f)
                {
                    Handles.color = color;
                    Handles.DrawAAPolyLine(lineWidth, points.Concat(new[] { points[0] }).ToArray());
                }
            }
        }

        /// <summary>Sutherland-Hodgman against the near plane, in view space (camera looks down -Z).</summary>
        private static List<Vector3> ClipToNear(List<Vector3> polygon, float near)
        {
            var result = new List<Vector3>(polygon.Count + 1);
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                bool aIn = -a.z > near;
                bool bIn = -b.z > near;
                if (aIn) result.Add(a);
                if (aIn != bIn)
                {
                    float t = (-near - a.z) / (b.z - a.z);
                    result.Add(Vector3.Lerp(a, b, t));
                }
            }
            return result;
        }

        private static Vector3 ToGui(Matrix4x4 projection, Vector3 viewPoint, Vector2 size)
        {
            var clip = projection * new Vector4(viewPoint.x, viewPoint.y, viewPoint.z, 1f);
            float x = clip.x / clip.w;
            float y = clip.y / clip.w;
            return new Vector3((x * 0.5f + 0.5f) * size.x, (1f - (y * 0.5f + 0.5f)) * size.y, 0f);
        }
    }
}
