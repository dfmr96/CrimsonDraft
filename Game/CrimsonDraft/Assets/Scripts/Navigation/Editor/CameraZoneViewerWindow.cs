#nullable enable

using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
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

        private RoomController[] rooms = System.Array.Empty<RoomController>();
        private CinemachineCamera[] cameras = System.Array.Empty<CinemachineCamera>();
        private int roomIndex;
        private int cameraIndex;
        private Vector2 listScroll;
        private ZoneViewport? viewport;
        private ZoneViewport? planViewport;
        private ZoneOverlay? overlay;

        private enum ViewMode { Camera, Plan, Split }
        private static readonly string[] ViewModeLabels = { "Camera", "Plan", "Split" };
        private const float MinPlanZoom = 0.25f;
        private const float MaxPlanZoom = 8f;
        private ViewMode viewMode;
        private float planZoom = 1f;
        private Vector2 planPan;
        private Vector2Int planPixels;
        private Vector2Int viewPixels;
        private double lastRenderTime;
        private bool unlit = true;
        private bool syncSceneView;
        private (CinemachineCamera? camera, Vector3 position, Quaternion rotation, float fov) lastSynced;
        private bool cameraMode;
        private int cameraDragButton = -1;
        private bool fastFly;
        private double lastFlightTime;
        private readonly HashSet<KeyCode> heldKeys = new HashSet<KeyCode>();
        private PlayerController? player;

        private ZoneViewport Viewport => this.viewport ??= new ZoneViewport("CameraZoneViewer_Camera");
        private ZoneViewport PlanViewport => this.planViewport ??= new ZoneViewport("CameraZoneViewer_Plan");
        private ZoneOverlay Overlay => this.overlay ??= new ZoneOverlay(this.Repaint);

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
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= this.OnHierarchyChanged;
            EditorApplication.update -= this.OnEditorUpdate;
            FixedCameraZoneTrigger.GizmoHighlightCamera = null;
            this.viewport?.Dispose();
            this.viewport = null;
            this.planViewport?.Dispose();
            this.planViewport = null;
            SceneView.RepaintAll();
        }

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
            var room = this.CurrentRoom;
            if (camera == null) return;
            if (this.syncSceneView) this.SyncSceneViewIfChanged(camera);

            if (this.viewMode != ViewMode.Plan && this.viewPixels.x >= 16 && this.viewPixels.y >= 16)
                this.Viewport.Render(this.viewPixels, view => ZoneViewport.CopyLens(camera, view), this.unlit);
            if (this.viewMode != ViewMode.Camera && room != null && this.planPixels.x >= 16 && this.planPixels.y >= 16)
                this.PlanViewport.Render(this.planPixels, view => this.SetupPlanCamera(view, room), this.unlit);
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
            // Zone cameras only: follow and inspection cameras do not get an activation zone.
            var room = this.CurrentRoom;
            var list = room != null ? ZoneCameras.Of(room) : System.Array.Empty<CinemachineCamera>();
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
                EditorGUILayout.HelpBox("This room has no CinemachineCamera.", MessageType.Info);
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
                    this.planPan = Vector2.zero;
                    this.planZoom = 1f;
                    this.RefreshCameras();
                }

                this.viewMode = (ViewMode)GUILayout.Toolbar((int)this.viewMode, ViewModeLabels, EditorStyles.toolbarButton, GUILayout.Width(150f));
                // Unlit swaps the level's materials for unlit stand-ins during the tool's own renders.
                // Look Through puts the Scene view on the camera instead, with the Scene view's own shading.
                bool unlitBefore = this.unlit;
                this.unlit = GUILayout.Toggle(this.unlit, new GUIContent("Unlit", "Show the level without lighting: unlit copies of its materials, only while this tool renders."),
                    EditorStyles.toolbarButton, GUILayout.Width(48f));
                if (this.unlit != unlitBefore) this.RequestRender();
                using (new EditorGUI.DisabledScope(this.CurrentCamera == null))
                {
                    if (GUILayout.Button(new GUIContent("Look Through", "Put the Scene view exactly where the selected camera is, to see it with the Scene view's own shading (lighting off)."),
                            EditorStyles.toolbarButton, GUILayout.Width(84f)))
                        this.LookThroughInSceneView();
                    this.syncSceneView = GUILayout.Toggle(this.syncSceneView, new GUIContent("Sync", "Keep the Scene view on the selected camera while you switch or move it."),
                        EditorStyles.toolbarButton, GUILayout.Width(40f));
                }
                this.DrawArrivalControls();
                this.cameraMode = GUILayout.Toggle(this.cameraMode, "Move Camera", EditorStyles.toolbarButton, GUILayout.Width(88f));
                if (!this.cameraMode) this.StopFlying();
                this.DrawFovField();

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

        private static readonly Color OkColor = new Color(0.3f, 0.85f, 0.35f);
        private static readonly Color NoneColor = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color LegacyColor = new Color(1f, 0.6f, 0.15f);

        // Camera list with each camera's activation status (green: one trigger, grey: none, orange:
        // several or two-sided), then the fix-up actions for the selected camera.
        private void DrawCameraList()
        {
            var room = this.CurrentRoom;
            var triggers = room != null
                ? room.GetComponentsInChildren<FixedCameraZoneTrigger>(includeInactive: true)
                : System.Array.Empty<FixedCameraZoneTrigger>();

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(this.listScroll))
                {
                    this.listScroll = scroll.scrollPosition;
                    for (int i = 0; i < this.cameras.Length; i++)
                    {
                        var camera = this.cameras[i];
                        if (camera == null) continue;
                        var rect = EditorGUILayout.GetControlRect(false, 20f);
                        if (i == this.cameraIndex) EditorGUI.DrawRect(rect, new Color(0.17f, 0.36f, 0.54f, 0.9f));
                        EditorGUI.DrawRect(new Rect(rect.x, rect.y + 3f, 6f, rect.height - 6f), FixedCameraZoneTrigger.GizmoColorFor(camera));
                        var state = CameraActivation.For(camera, triggers).State;
                        EditorGUI.DrawRect(new Rect(rect.xMax - 12f, rect.y + 6f, 8f, 8f), StateColor(state));
                        GUI.Label(new Rect(rect.x + 10f, rect.y, rect.width - 26f, rect.height), camera.name);
                        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                        {
                            this.cameraIndex = i;
                            this.lastRenderTime = 0;
                            Event.current.Use();
                        }
                    }
                }

                var current = this.CurrentCamera;
                if (room != null && current != null) this.DrawActivationPanel(room, current, triggers);
            }
        }

        private void DrawActivationPanel(RoomController room, CinemachineCamera camera, FixedCameraZoneTrigger[] triggers)
        {
            var status = CameraActivation.For(camera, triggers);
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var label = status.State switch
                {
                    CameraActivationState.Ok => "One activation trigger",
                    CameraActivationState.None => "No activation trigger",
                    _ => $"Legacy: {status.Triggers.Count} trigger(s), keep one",
                };
                var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = StateColor(status.State) } };
                GUILayout.Label(label, style);

                if (status.State == CameraActivationState.None)
                {
                    if (GUILayout.Button("Create Trigger"))
                        EditorApplication.delayCall += () => this.AfterEdit(CameraActivationEditing.CreateTrigger(camera, room));
                    return;
                }

                foreach (var trigger in status.Triggers)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(trigger.name, EditorStyles.label, GUILayout.MinWidth(60f))) EditorGUIUtility.PingObject(trigger);
                        if (status.State != CameraActivationState.Legacy) continue;
                        if (GUILayout.Button("Keep", EditorStyles.miniButtonLeft, GUILayout.Width(40f)))
                            EditorApplication.delayCall += () => { CameraActivationEditing.KeepOnly(trigger, camera); this.AfterEdit(trigger); };
                        if (GUILayout.Button("Delete", EditorStyles.miniButtonRight, GUILayout.Width(46f)))
                            EditorApplication.delayCall += () => { CameraActivationEditing.Delete(trigger); this.AfterEdit(null); };
                    }
                }
            }
        }

        private void AfterEdit(Object? changed)
        {
            if (changed != null) EditorUtility.SetDirty(changed);
            this.RefreshRooms();
            this.RequestRender();
            SceneView.RepaintAll();
        }

        private static Color StateColor(CameraActivationState state) => state switch
        {
            CameraActivationState.Ok => OkColor,
            CameraActivationState.None => NoneColor,
            _ => LegacyColor,
        };

        private void DrawView(RoomController room)
        {
            var camera = this.CurrentCamera;
            var area = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            // Both ids are taken every pass so they stay stable whatever the view mode.
            int cameraControlId = GUIUtility.GetControlID(FocusType.Passive);
            int planControlId = GUIUtility.GetControlID(FocusType.Passive);
            // The Layout pass returns a dummy rect; every other pass has the real one.
            if (camera == null || Event.current.type == EventType.Layout) return;

            FixedCameraZoneTrigger.GizmoHighlightCamera = camera;
            float planeY = ZoneOverlay.ResolvePlaneY(room, this.player);

            switch (this.viewMode)
            {
                case ViewMode.Camera:
                    this.DrawCameraView(room, camera, area, planeY, cameraControlId);
                    break;
                case ViewMode.Plan:
                    this.DrawPlanView(room, camera, area, planeY, planControlId);
                    break;
                default:
                    float half = Mathf.Floor(area.width * 0.5f);
                    this.DrawCameraView(room, camera, new Rect(area.x, area.y, half - 1f, area.height), planeY, cameraControlId);
                    this.DrawPlanView(room, camera, new Rect(area.x + half + 1f, area.y, area.width - half - 1f, area.height), planeY, planControlId);
                    break;
            }

            if (Event.current.type == EventType.Repaint && !room.gameObject.activeInHierarchy)
                EditorGUI.HelpBox(new Rect(area.x, area.y, area.width, 32f),
                    "The room is inactive, so its geometry will not render.", MessageType.Warning);
        }

        private void DrawCameraView(RoomController room, CinemachineCamera camera, Rect area, float planeY, int controlId)
        {
            var view = ZoneViewport.FitToGameAspect(area);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(area, Color.black);
                this.viewPixels = ToPixels(view);
                if (this.Viewport.Texture != null)
                    GUI.DrawTexture(view, this.Viewport.Texture, ScaleMode.StretchToFill, false);
                if (this.Viewport.HasCamera)
                    this.Overlay.Draw(room, this.Viewport.Camera, view, planeY, camera, this.player);
            }

            if (this.cameraMode)
                this.HandleCameraControls(camera, view, controlId);
            else if (this.Viewport.HasCamera)
                this.Overlay.Edit(room, this.Viewport.Camera, view, planeY, controlId);
        }

        // Top-down orthographic view of the room: every zone, the selected camera and its view on the
        // floor. Middle mouse pans, the wheel zooms, the left button edits zones like the camera view.
        private void DrawPlanView(RoomController room, CinemachineCamera camera, Rect area, float planeY, int controlId)
        {
            var e = Event.current;
            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(area, Color.black);
                this.planPixels = ToPixels(area);
                if (this.PlanViewport.Texture != null)
                    GUI.DrawTexture(area, this.PlanViewport.Texture, ScaleMode.StretchToFill, false);
                if (this.PlanViewport.HasCamera)
                {
                    var plan = this.PlanViewport.Camera;
                    this.Overlay.Draw(room, plan, area, planeY, camera, this.player);
                    var game = Handles.GetMainGameViewSize();
                    this.Overlay.DrawCameraFrustum(camera, game.y > 0f ? game.x / game.y : 16f / 9f, plan, area, planeY);
                }
                EditorGUI.LabelField(new Rect(area.x + 6f, area.y + area.height - 20f, area.width - 12f, 18f),
                    "Plan: MMB pan  |  wheel zoom  |  LMB edit zones", EditorStyles.whiteMiniLabel);
            }

            if (area.Contains(e.mousePosition) || GUIUtility.hotControl == controlId)
                this.HandlePlanNavigation(area, controlId);
            if (this.PlanViewport.HasCamera)
                this.Overlay.Edit(room, this.PlanViewport.Camera, area, planeY, GUIUtility.GetControlID(FocusType.Passive));
        }

        private void HandlePlanNavigation(Rect area, int controlId)
        {
            var e = Event.current;
            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown when e.button == 2:
                    GUIUtility.hotControl = controlId;
                    e.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                    float worldPerPixel = this.PlanViewport.HasCamera
                        ? this.PlanViewport.Camera.orthographicSize * 2f / Mathf.Max(1f, area.height) : 0.01f;
                    // Screen right = world +X, screen up = world +Z for a camera looking straight down.
                    this.planPan += new Vector2(-e.delta.x, e.delta.y) * worldPerPixel;
                    e.Use();
                    this.RequestRender();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == controlId:
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;
                case EventType.ScrollWheel:
                    this.planZoom = Mathf.Clamp(this.planZoom * (1f - e.delta.y * 0.05f), MinPlanZoom, MaxPlanZoom);
                    e.Use();
                    this.RequestRender();
                    break;
            }
        }

        private void SetupPlanCamera(Camera view, RoomController room)
        {
            var bounds = PlanViewFraming.RoomBounds(room);
            float aspect = this.planPixels.y > 0 ? (float)this.planPixels.x / this.planPixels.y : 1f;
            var (position, rotation, size) = PlanViewFraming.Fit(bounds, aspect, this.planZoom, this.planPan);
            view.transform.SetPositionAndRotation(position, rotation);
            view.orthographic = true;
            view.orthographicSize = size;
            // Section cut above head height, so ceilings do not hide the room.
            view.nearClipPlane = PlanViewFraming.NearClip(position.y, ZoneOverlay.ResolvePlaneY(room, this.player));
            view.farClipPlane = position.y - bounds.min.y + 20f;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Color.black;
        }

        private void LookThroughInSceneView()
        {
            var camera = this.CurrentCamera;
            var sceneView = SceneView.lastActiveSceneView;
            if (camera == null || sceneView == null) return;
            SceneViewSync.LookThrough(sceneView, camera);
            this.lastSynced = (camera, camera.transform.position, camera.transform.rotation, camera.Lens.FieldOfView);
        }

        // Re-aligns only when the selected camera, its pose or its FOV changed, so the Scene view can still
        // be orbited freely while Sync is on and nothing moves.
        private void SyncSceneViewIfChanged(CinemachineCamera camera)
        {
            var t = camera.transform;
            if (this.lastSynced.camera == camera && this.lastSynced.position == t.position
                && this.lastSynced.rotation == t.rotation && Mathf.Approximately(this.lastSynced.fov, camera.Lens.FieldOfView)) return;
            this.LookThroughInSceneView();
        }

        private void RequestRender()
        {
            this.lastRenderTime = 0;
            this.Repaint();
        }

        private static Vector2Int ToPixels(Rect rect)
        {
            float scale = EditorGUIUtility.pixelsPerPoint;
            return new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(rect.width * scale), 0, 2048),
                Mathf.Clamp(Mathf.RoundToInt(rect.height * scale), 0, 2048));
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

    }
}
