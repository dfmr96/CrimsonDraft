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
    /// <summary>Draws a room's camera zones flat on the player's floor plane (same look as the runtime
    /// DebugWorldOverlay) on top of any view camera, and edits them with handles: drag a white vertex or
    /// a yellow divider end, drag the orange square to move the whole zone, click a green dot to split an
    /// edge, Ctrl+click a vertex to delete it. Shared by the camera view and the plan view.</summary>
    internal sealed class ZoneOverlay
    {
        private const float PlayerMarkerRadius = 0.1f;
        private const float PlayerDotRadius = 0.03f;
        private const int PlayerMarkerSegments = 24;

        private readonly System.Action repaint;
        private float planeY;
        private PlayerController? player;

        public ZoneOverlay(System.Action repaint) => this.repaint = repaint;

        // ---- Zone editing handles --------------------------------------------------------

        private enum HandleKind { Vertex, Add, Move }

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
        public void Edit(RoomController room, Camera view, Rect rect, float planeY, int controlId)
        {
            this.planeY = planeY;
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
                    this.repaint();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId || this.dragging == null) break;
                    this.MoveHandle(this.dragging.Value, e.mousePosition, view, rect);
                    e.Use();
                    this.repaint();
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
                if (polygon == null) continue;
                float height = trigger.MinHeight;
                for (int i = 0; i < polygon.Count && polygon.Count >= 3; i++)
                {
                    TryAddHandle(handles, view, rect, trigger, HandleKind.Vertex, i, trigger.ToWorld(polygon[i], height));
                    var mid = (polygon[i] + polygon[(i + 1) % polygon.Count]) * 0.5f;
                    TryAddHandle(adds, view, rect, trigger, HandleKind.Add, i, trigger.ToWorld(mid, height));
                }
                if (polygon.Count >= 3)
                    TryAddHandle(moves, view, rect, trigger, HandleKind.Move, 0, trigger.ToWorld(trigger.Centroid, height));
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

            var local = handle.Trigger.ToLocal(ray.GetPoint(distance));
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
            if (handle.Kind == HandleKind.Vertex)
                serialized.FindProperty("polygon").GetArrayElementAtIndex(handle.Index).vector2Value = point;
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

        // ---- Trigger overlay -------------------------------------------------------------

        public void Draw(RoomController room, Camera view, Rect rect, float planeY, CinemachineCamera? current, PlayerController? player)
        {
            this.planeY = planeY;
            this.player = player;
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
                if (polygon == null || polygon.Count < 3) continue; // half-built trigger (tests, Reset)

                bool emphasized = current != null && trigger.ReferencedCameras.Contains(current);
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

        /// <summary>The camera's position and the footprint of its view on the floor plane, for the plan
        /// view: lines from the camera to where the four frustum corners meet the floor (rays that never
        /// reach it stop at the far plane, flattened).</summary>
        public void DrawCameraFrustum(CinemachineCamera camera, float aspect, Camera view, Rect rect, float planeY)
        {
            view.aspect = rect.width / rect.height;
            var overlay = new OverlayProjection(view.worldToCameraMatrix, view.projectionMatrix, view.nearClipPlane, rect.size);
            var t = camera.transform;
            var lens = camera.Lens;
            float tanV = Mathf.Tan(lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * aspect;
            var origin = t.position;
            var ground = new Vector3(origin.x, planeY, origin.z);

            var corners = new Vector3[4];
            int k = 0;
            foreach (var (sx, sy) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            {
                var direction = (t.forward + t.right * (sx * tanH) + t.up * (sy * tanV)).normalized;
                corners[k++] = FloorPoint(origin, direction, planeY, lens.FarClipPlane);
            }

            var color = FixedCameraZoneTrigger.GizmoColorFor(camera);
            GUI.BeginClip(rect);
            for (int i = 0; i < 4; i++)
            {
                overlay.DrawLine(ground, corners[i], new Color(color.r, color.g, color.b, 0.6f), 1.5f);
                overlay.DrawLine(corners[i], corners[(i + 1) % 4], color, 2f);
            }
            overlay.Draw(Circle(ground, 0.25f), color, 1f, 0f);
            GUI.EndClip();
        }

        private static Vector3 FloorPoint(Vector3 origin, Vector3 direction, float planeY, float far)
        {
            if (direction.y < -1e-4f)
            {
                float distance = (planeY - origin.y) / direction.y;
                if (distance > 0f && distance <= far) return origin + direction * distance;
            }
            var point = origin + direction * Mathf.Min(far, 30f);
            point.y = planeY;
            return point;
        }

        internal Vector3 OnPlane(Vector3 world)
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
        public static float ResolvePlaneY(RoomController room, PlayerController? player)
        {
            if (player != null) return player.FootPosition.y;

            var trigger = room.GetComponentInChildren<FixedCameraZoneTrigger>(includeInactive: false);
            return trigger != null ? trigger.ToWorld(Vector2.zero, trigger.MinHeight).y : 0f;
        }

        private static string LabelFor(FixedCameraZoneTrigger trigger) =>
            string.Join(" | ", trigger.ReferencedCameras.Select(c => c.name));

        /// <summary>Projects world-space convex polygons to the view rect, clipped to the near plane.</summary>
        internal readonly struct OverlayProjection
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

        internal static Vector3 ToGui(Matrix4x4 projection, Vector3 viewPoint, Vector2 size)
        {
            var clip = projection * new Vector4(viewPoint.x, viewPoint.y, viewPoint.z, 1f);
            float x = clip.x / clip.w;
            float y = clip.y / clip.w;
            return new Vector3((x * 0.5f + 0.5f) * size.x, (1f - (y * 0.5f + 0.5f)) * size.y, 0f);
        }
    }
}
