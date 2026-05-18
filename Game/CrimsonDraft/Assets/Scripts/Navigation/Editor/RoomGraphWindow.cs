#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    public sealed class RoomGraphWindow : EditorWindow
    {
        private const float NodeW    = 150f;
        private const float NodeH    = 44f;
        private const float ArrowSize = 14f;

        private readonly List<NodeData>                          nodes   = new();
        private readonly List<EdgeData>                          edges   = new();
        private readonly Dictionary<RoomController, int>         nodeIdx = new();

        private Vector2 pan      = Vector2.zero;
        private float   zoom     = 1f;
        private int     dragging = -1;
        private Vector2 dragOffset;
        private bool    wasDragged;

        // ── toolbar button ───────────────────────────────────────────────────

        [MainToolbarElement("CrimsonDraft/RoomGraph", defaultDockPosition = MainToolbarDockPosition.Right)]
        private static IEnumerable<MainToolbarElement> CreateButton()
        {
            var icon = EditorGUIUtility.IconContent("d_UnityEditor.SceneHierarchyWindow").image as Texture2D;
            yield return new MainToolbarButton(
                new MainToolbarContent(icon, "Room Connection Graph"),
                () => EditorApplication.delayCall += Open);
        }

        [MenuItem("Tools/Navigation Room Graph")]
        public static void Open()
        {
            var w = GetWindow<RoomGraphWindow>("Room Graph");
            w.minSize = new Vector2(400f, 300f);
            w.Refresh();
        }

        // ── lifecycle ────────────────────────────────────────────────────────

        private void OnEnable() => Refresh();

        private void Refresh()
        {
            nodes.Clear();
            edges.Clear();
            nodeIdx.Clear();

            var rooms = Object.FindObjectsOfType<RoomController>(includeInactive: true);
            System.Array.Sort(rooms, (a, b) => string.Compare(
                string.IsNullOrEmpty(a.RoomId) ? a.name : a.RoomId,
                string.IsNullOrEmpty(b.RoomId) ? b.name : b.RoomId,
                System.StringComparison.Ordinal));

            var doors  = Object.FindObjectsOfType<RoomDoorInteractable>(includeInactive: true);
            var spawns = Object.FindObjectsOfType<SpawnPoint>(includeInactive: true);

            for (var i = 0; i < rooms.Length; i++)
            {
                var room = rooms[i];
                var key  = PosKey(room);
                var pos  = EditorPrefs.HasKey(key + "_x")
                    ? new Vector2(EditorPrefs.GetFloat(key + "_x"), EditorPrefs.GetFloat(key + "_y"))
                    : new Vector2(80f + i * 200f, 120f);

                nodeIdx[room] = nodes.Count;
                nodes.Add(new NodeData
                {
                    Room     = room,
                    Pos      = pos,
                    HasIssue = string.IsNullOrEmpty(room.RoomId),
                });
            }

            // Build (destRoom, fromRoom) pairs that have a matching SpawnPoint.
            var spawnPairs = new HashSet<(RoomController, RoomController)>();
            foreach (var sp in spawns)
            {
                if (sp.FromRoom == null) continue;
                var spRoom = sp.GetComponentInParent<RoomController>(includeInactive: true);
                if (spRoom != null)
                    spawnPairs.Add((spRoom, sp.FromRoom));
            }

            foreach (var door in doors)
            {
                var fromRoom = door.GetComponentInParent<RoomController>(includeInactive: true);
                if (fromRoom == null) continue;

                var dest     = door.Destination;
                var hasSpawn = dest != null && spawnPairs.Contains((dest, fromRoom));

                edges.Add(new EdgeData
                {
                    FromRoom      = fromRoom,
                    ToRoom        = dest,
                    Door          = door,
                    HasIssue      = dest == null || !hasSpawn,
                    HasSpawnPoint = hasSpawn,
                });
            }

            Repaint();
        }

        // ── gui ──────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawToolbar();

            var canvas = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(canvas, new Color(0.13f, 0.13f, 0.13f));

            HandleInput(canvas);

            GUI.BeginClip(canvas);
            DrawEdges();
            DrawNodes();
            GUI.EndClip();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                Refresh();
            if (GUILayout.Button("Reset Layout", EditorStyles.toolbarButton, GUILayout.Width(85)))
                ResetLayout();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void ResetLayout()
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                node.Pos = new Vector2(80f + i * 200f, 120f);
                nodes[i] = node;
                SavePos(node);
            }
            Repaint();
        }

        // ── input ────────────────────────────────────────────────────────────

        private void HandleInput(Rect canvas)
        {
            var e = Event.current;

            if (e.type == EventType.ScrollWheel && canvas.Contains(e.mousePosition))
            {
                zoom = Mathf.Clamp(zoom - e.delta.y * 0.05f, 0.3f, 2.5f);
                e.Use(); Repaint();
            }

            if (e.type == EventType.MouseDrag &&
                (e.button == 2 || (e.button == 0 && e.alt)) &&
                canvas.Contains(e.mousePosition))
            {
                pan += e.delta;
                e.Use(); Repaint();
            }

            if (e.type == EventType.MouseDown && e.button == 0 && canvas.Contains(e.mousePosition))
            {
                var local = ToLocal(e.mousePosition - canvas.position);
                for (var i = 0; i < nodes.Count; i++)
                {
                    if (NodeRect(nodes[i].Pos).Contains(local))
                    {
                        dragging   = i;
                        dragOffset = local - nodes[i].Pos;
                        wasDragged = false;
                        e.Use();
                        break;
                    }
                }
            }

            if (e.type == EventType.MouseDrag && e.button == 0 && dragging >= 0)
            {
                var local     = ToLocal(e.mousePosition - canvas.position);
                var node      = nodes[dragging];
                node.Pos      = local - dragOffset;
                nodes[dragging] = node;
                wasDragged    = true;
                SavePos(node);
                e.Use(); Repaint();
            }

            if (e.type == EventType.MouseUp && e.button == 0 && dragging >= 0)
            {
                if (!wasDragged)
                    EditorGUIUtility.PingObject(nodes[dragging].Room);
                dragging = -1;
            }
        }

        // ── drawing ──────────────────────────────────────────────────────────

        private void DrawEdges()
        {
            Handles.BeginGUI();

            foreach (var edge in edges)
            {
                if (!nodeIdx.TryGetValue(edge.FromRoom, out var fi)) continue;

                var fromLocalCenter = NodeCenter(nodes[fi].Pos);

                if (edge.ToRoom == null || !nodeIdx.TryGetValue(edge.ToRoom, out var ti))
                {
                    Handles.color = Color.red;
                    var stub = ToCanvas(fromLocalCenter) + new Vector2(60f, 0f);
                    Handles.DrawLine(ToCanvas(fromLocalCenter), stub);
                    continue;
                }

                var toLocalCenter = NodeCenter(nodes[ti].Pos);
                var dir           = (toLocalCenter - fromLocalCenter).normalized;
                var normal        = new Vector2(-dir.y, dir.x) * 6f;

                var fp = ToCanvas(NodeBorderInDir(nodes[fi].Pos,  dir) + normal);
                var tp = ToCanvas(NodeBorderInDir(nodes[ti].Pos, -dir) + normal);

                Handles.color = edge.HasIssue ? Color.red : new Color(1f, 0.85f, 0.4f, 0.9f);
                Handles.DrawLine(fp, tp);
                DrawArrow(fp, tp);
            }

            Handles.EndGUI();
        }

        private void DrawArrow(Vector2 from, Vector2 to)
        {
            var dir   = (to - from).normalized;
            var right = new Vector2(-dir.y, dir.x);
            var size  = ArrowSize * zoom;
            var a     = to - dir * size + right * (size * 0.45f);
            var b     = to - dir * size - right * (size * 0.45f);
            Handles.DrawAAConvexPolygon(to, a, b);
        }

        private static Vector2 NodeBorderInDir(Vector2 nodePos, Vector2 dir)
        {
            var center = NodeCenter(nodePos);
            if (Mathf.Abs(dir.x) < 0.0001f)
                return center + new Vector2(0f, Mathf.Sign(dir.y) * NodeH * 0.5f);
            if (Mathf.Abs(dir.y) < 0.0001f)
                return center + new Vector2(Mathf.Sign(dir.x) * NodeW * 0.5f, 0f);

            var t = Mathf.Min(NodeW * 0.5f / Mathf.Abs(dir.x), NodeH * 0.5f / Mathf.Abs(dir.y));
            return center + dir * t;
        }

        private void DrawNodes()
        {
            var labelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize  = Mathf.RoundToInt(11f * zoom),
                wordWrap  = true,
                normal    = { textColor = Color.white },
            };

            foreach (var node in nodes)
            {
                var rect  = ToCanvasRect(NodeRect(node.Pos));
                var color = node.HasIssue ? new Color(0.65f, 0.15f, 0.15f) : new Color(0.18f, 0.42f, 0.18f);

                EditorGUI.DrawRect(new Rect(rect.x + 3, rect.y + 3, rect.width, rect.height), new Color(0, 0, 0, 0.35f));
                EditorGUI.DrawRect(rect, color);

                var label = node.HasIssue
                    ? $"{node.Room.name}\n[no id]"
                    : node.Room.RoomId;

                GUI.Label(rect, label, labelStyle);
            }
        }

        // ── coordinate helpers ───────────────────────────────────────────────

        private static Rect   NodeRect(Vector2 pos)   => new(pos.x, pos.y, NodeW, NodeH);
        private static Vector2 NodeCenter(Vector2 pos) => pos + new Vector2(NodeW * 0.5f, NodeH * 0.5f);

        private Vector2 ToCanvas(Vector2 local)          => local * zoom + pan;
        private Vector2 ToLocal(Vector2 canvas)          => (canvas - pan) / zoom;
        private Rect    ToCanvasRect(Rect r)             => new(r.x * zoom + pan.x, r.y * zoom + pan.y, r.width * zoom, r.height * zoom);

        // ── persistence ──────────────────────────────────────────────────────

        private static string PosKey(RoomController room)
        {
            var id = string.IsNullOrEmpty(room.RoomId) ? room.name : room.RoomId;
            return $"RoomGraph_{id}";
        }

        private static void SavePos(NodeData node)
        {
            var key = PosKey(node.Room);
            EditorPrefs.SetFloat(key + "_x", node.Pos.x);
            EditorPrefs.SetFloat(key + "_y", node.Pos.y);
        }

        // ── data types ───────────────────────────────────────────────────────

        private struct NodeData
        {
            public RoomController Room;
            public Vector2        Pos;
            public bool           HasIssue;
        }

        private struct EdgeData
        {
            public RoomController       FromRoom;
            public RoomController?      ToRoom;
            public RoomDoorInteractable Door;
            public bool                 HasIssue;
            public bool                 HasSpawnPoint;
        }
    }
}
