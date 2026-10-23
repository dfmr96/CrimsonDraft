#nullable enable

using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Looks through a room's fixed cameras without entering Play Mode. The room picker
    /// lists the RoomControllers of the open scene; the camera list holds the cameras that room's
    /// FixedCameraZoneTriggers activate. The view shows every trigger volume of the room, so it is
    /// easy to see whether the player can reach another camera's trigger while still in frame.
    /// Read-only: it never moves or enables anything in the scene.
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
        private Camera? viewCamera;
        private RenderTexture? viewTexture;
        private Vector2Int viewPixels;
        private double lastRenderTime;
        private bool unlit = true;

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
            this.DestroyViewCamera();
            this.ReleaseTexture();
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
            if (EditorApplication.timeSinceStartup - this.lastRenderTime < RenderInterval) return;
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

        private RoomController? CurrentRoom =>
            this.roomIndex >= 0 && this.roomIndex < this.rooms.Length ? this.rooms[this.roomIndex] : null;

        private CinemachineCamera? CurrentCamera =>
            this.cameraIndex >= 0 && this.cameraIndex < this.cameras.Length ? this.cameras[this.cameraIndex] : null;

        private void OnGUI()
        {
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

            if (this.viewCamera != null) this.EditHandles(room, this.viewCamera, view, controlId);
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

        private static void TryAddHandle(List<ZoneHandle> list, Camera view, Rect rect,
            FixedCameraZoneTrigger trigger, HandleKind kind, int index, Vector3 world)
        {
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

            var plane = new Plane(handle.Trigger.transform.up, handle.World);
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

            var caps = new List<Vector3[]>();
            var walls = new List<Vector3[]>();

            GUI.BeginClip(rect);
            foreach (var trigger in room.GetComponentsInChildren<FixedCameraZoneTrigger>(includeInactive: false))
            {
                var color = FixedCameraZoneTrigger.GizmoColorFor(trigger.ZoneCamera);
                bool emphasized = trigger.ReferencedCameras.Contains(current);

                caps.Clear();
                walls.Clear();
                trigger.GetCapTriangles(caps);
                trigger.GetWallQuads(walls);

                foreach (var cap in caps) overlay.Draw(cap, color, emphasized ? 0.22f : 0.08f, 0f);
                foreach (var wall in walls) overlay.Draw(wall, color, emphasized ? 0.22f : 0.08f, emphasized ? 3f : 1.5f);

                if (trigger.UseTwoSidedSwitch)
                {
                    float margin = Mathf.Max(0f, trigger.SideMargin);
                    overlay.Draw(trigger.GetDividerQuad(-margin), FixedCameraZoneTrigger.GizmoColorFor(trigger.ZoneCamera), 0f, 2f);
                    overlay.Draw(trigger.GetDividerQuad(margin), FixedCameraZoneTrigger.GizmoColorFor(
                        trigger.ReferencedCameras.LastOrDefault()), 0f, 2f);
                    overlay.Draw(trigger.GetDividerQuad(0f), Color.white, 0f, 2f);
                }

                var polygon = trigger.Polygon;
                if (polygon.Count < 3) continue;
                var anchor = Vector3.zero;
                for (int i = 0; i < polygon.Count; i++)
                    anchor += trigger.ToWorld(polygon[i], (trigger.MinHeight + trigger.MaxHeight) * 0.5f);
                anchor /= polygon.Count;

                var anchorView = worldToView.MultiplyPoint3x4(anchor);
                if (-anchorView.z > near)
                {
                    var p = ToGui(projection, anchorView, rect.size);
                    var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = color } };
                    GUI.Label(new Rect(p.x - 80f, p.y - 8f, 160f, 16f), LabelFor(trigger), style);
                }
            }
            GUI.EndClip();
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
