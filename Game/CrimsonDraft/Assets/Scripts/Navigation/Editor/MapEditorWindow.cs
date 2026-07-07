#nullable enable

using UnityEditor;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Navigation.Map;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>2D grid view of the current scene's map layout.</summary>
    public sealed class MapEditorWindow : EditorWindow
    {
        private const float PixelsPerUnit = 20f;

        private const float CenterHandleRadius = 8f;
        private const float CenterHandlePickRadius = 10f;

        private Vector2 pan;
        private float zoom = 1f;
        private MapRoomShape? draggingRoom;
        private MapDoorMarker? draggingDoor;
        private bool draggingCenter;

        private MapData? gridSettingsTarget;
        private SerializedObject? gridSettingsSerialized;
        private bool gridSettingsExpanded = true;
        private Rect canvasRect;

        [MenuItem("Tools/CrimsonDraft/Map Editor")]
        public static void Open()
        {
            var window = GetWindow<MapEditorWindow>("Map Editor");
            window.minSize = new Vector2(500f, 400f);
        }

        private void OnGUI()
        {
            var config = FindFirstObjectByType<MapSceneConfig>();
            if (config == null || config.Map == null)
            {
                EditorGUILayout.HelpBox(
                    "No MapSceneConfig with a MapData asset in the open scene.",
                    MessageType.Info);
                return;
            }

            GUILayout.BeginVertical();
            DrawToolbar(config);
            DrawGridSettings(config);
            GUILayout.EndVertical();

            // Reserve all remaining space below the header for the free-form canvas.
            this.canvasRect = GUILayoutUtility.GetRect(
                0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // BeginGroup makes mouse coordinates and draw calls relative to canvasRect AND
            // clips anything drawn outside it — without this, grid lines/labels are drawn in
            // absolute window coordinates with no clipping and bleed up into the header.
            GUI.BeginGroup(this.canvasRect);

            HandleInput(config);
            DrawGrid(config);

            foreach (var shape in FindObjectsByType<MapRoomShape>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                DrawRoom(shape);

            foreach (var marker in FindObjectsByType<MapDoorMarker>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                DrawDoor(marker);

            DrawCenterHandle(config);

            GUI.EndGroup();

            Repaint();
        }

        private void DrawToolbar(MapSceneConfig config)
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Map: {config.Map.name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Bake Now", EditorStyles.toolbarButton))
                MapBaker.Bake(config);
            GUILayout.EndHorizontal();
        }

        private void DrawGridSettings(MapSceneConfig config)
        {
            if (this.gridSettingsTarget != config.Map || this.gridSettingsSerialized == null)
            {
                this.gridSettingsTarget     = config.Map;
                this.gridSettingsSerialized = new SerializedObject(config.Map);
            }

            this.gridSettingsExpanded = EditorGUILayout.Foldout(
                this.gridSettingsExpanded, "Grid Settings", true);
            if (!this.gridSettingsExpanded)
                return;

            this.gridSettingsSerialized.Update();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(this.gridSettingsSerialized.FindProperty("gridSize"));
            EditorGUILayout.PropertyField(this.gridSettingsSerialized.FindProperty("cellSize"));
            EditorGUILayout.PropertyField(this.gridSettingsSerialized.FindProperty("center"));
            EditorGUILayout.HelpBox(
                "Grid Size × Cell Size sets the camera's orthographic size (half of the larger " +
                "extent). Center is where the camera looks by default — set it to the deck's " +
                "actual world coordinates, since room offsets are authored in world space.",
                MessageType.None);
            EditorGUILayout.EndVertical();

            this.gridSettingsSerialized.ApplyModifiedProperties();
        }

        // Shares gridSettingsSerialized with DrawGridSettings so dragging the handle and
        // typing into the Grid Settings field stay consistent (same SerializedObject, same
        // Undo group).
        private void MoveCenter(MapSceneConfig config, Vector2 deltaMap)
        {
            if (this.gridSettingsSerialized == null || this.gridSettingsTarget != config.Map)
                return;

            this.gridSettingsSerialized.Update();
            var centerProp = this.gridSettingsSerialized.FindProperty("center");
            centerProp.vector2Value += deltaMap;
            this.gridSettingsSerialized.ApplyModifiedProperties();
        }

        // Drawing happens inside GUI.BeginGroup(canvasRect), so (0,0) is the group's own
        // top-left corner — center on canvasRect's local size, not its absolute position.
        private Vector2 CanvasCenter => this.canvasRect.size * 0.5f;

        private Vector2 MapToScreen(Vector2 mapPos)
            => new Vector2(mapPos.x, -mapPos.y) * (PixelsPerUnit * this.zoom)
               + this.pan
               + CanvasCenter;

        private Vector2 ScreenToMap(Vector2 screenPos)
        {
            var p = (screenPos - this.pan - CanvasCenter) / (PixelsPerUnit * this.zoom);
            return new Vector2(p.x, -p.y);
        }

        private void DrawGrid(MapSceneConfig config)
        {
            var size = config.Map.GridSize;
            var cell = config.Map.CellSize;

            Handles.BeginGUI();
            Handles.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            for (int x = -size.x / 2; x <= size.x / 2; x++)
            {
                var a = MapToScreen(new Vector2(x * cell, -size.y * 0.5f * cell));
                var b = MapToScreen(new Vector2(x * cell, size.y * 0.5f * cell));
                Handles.DrawLine(a, b);
            }

            for (int y = -size.y / 2; y <= size.y / 2; y++)
            {
                var a = MapToScreen(new Vector2(-size.x * 0.5f * cell, y * cell));
                var b = MapToScreen(new Vector2(size.x * 0.5f * cell, y * cell));
                Handles.DrawLine(a, b);
            }
            Handles.EndGUI();
        }

        private void DrawRoom(MapRoomShape shape)
        {
            var points = shape.LocalPoints;
            if (points.Length < 3)
                return;

            bool selected = Selection.activeGameObject == shape.gameObject;
            var rot = Quaternion.Euler(0f, 0f, -shape.MapRotation);

            var screen = new Vector3[points.Length + 1];
            for (int i = 0; i <= points.Length; i++)
            {
                var p = points[i % points.Length];
                var mapPos = (Vector2)(rot * Vector2.Scale(p, shape.MapScale)) + shape.MapOffset;
                screen[i] = MapToScreen(mapPos);
            }

            Handles.BeginGUI();
            Handles.color = selected ? Color.yellow : Color.cyan;
            Handles.DrawPolyLine(screen);
            Handles.EndGUI();

            var label = MapToScreen(shape.MapOffset);
            GUI.Label(new Rect(label.x - 40f, label.y - 8f, 120f, 16f),
                shape.Room.RoomId, EditorStyles.miniBoldLabel);
        }

        private void DrawDoor(MapDoorMarker marker)
        {
            bool selected = Selection.activeGameObject == marker.gameObject;
            var center = MapToScreen(marker.MapOffset);
            var size = marker.Size * PixelsPerUnit * this.zoom;

            var oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(marker.MapRotation, center);
            EditorGUI.DrawRect(
                new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y),
                selected ? Color.yellow : new Color(0.9f, 0.4f, 0.3f));
            GUI.matrix = oldMatrix;
        }

        private void DrawCenterHandle(MapSceneConfig config)
        {
            var pos = MapToScreen(config.Map.Center);
            const float r = CenterHandleRadius;

            var diamond = new Vector3[]
            {
                new(pos.x, pos.y - r),
                new(pos.x + r, pos.y),
                new(pos.x, pos.y + r),
                new(pos.x - r, pos.y),
                new(pos.x, pos.y - r),
            };

            Handles.BeginGUI();
            Handles.color = this.draggingCenter ? Color.yellow : new Color(0.4f, 1f, 0.4f, 1f);
            Handles.DrawPolyLine(diamond);
            Handles.EndGUI();

            GUI.Label(new Rect(pos.x + r + 4f, pos.y - 8f, 60f, 16f), "Center", EditorStyles.miniBoldLabel);
        }

        private void HandleInput(MapSceneConfig config)
        {
            var e = Event.current;

            if (e.type == EventType.ScrollWheel)
            {
                this.zoom = Mathf.Clamp(this.zoom * (e.delta.y > 0 ? 0.9f : 1.1f), 0.2f, 5f);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 2)
            {
                this.pan += e.delta;
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (Vector2.Distance(e.mousePosition, MapToScreen(config.Map.Center)) <= CenterHandlePickRadius)
                {
                    this.draggingCenter = true;
                    e.Use();
                }
                else
                {
                    var hit = PickAt(e.mousePosition);
                    Selection.activeGameObject = hit;
                    this.draggingRoom = hit != null ? hit.GetComponent<MapRoomShape>() : null;
                    this.draggingDoor = hit != null ? hit.GetComponent<MapDoorMarker>() : null;
                    if (hit != null)
                        e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                var deltaMap = new Vector2(e.delta.x, -e.delta.y) / (PixelsPerUnit * this.zoom);
                if (this.draggingCenter)
                {
                    MoveCenter(config, deltaMap);
                    e.Use();
                }
                else if (this.draggingRoom != null)
                {
                    Undo.RecordObject(this.draggingRoom, "Move Map Room");
                    this.draggingRoom.MapOffset += deltaMap;
                    EditorUtility.SetDirty(this.draggingRoom);
                    e.Use();
                }
                else if (this.draggingDoor != null)
                {
                    Undo.RecordObject(this.draggingDoor, "Move Map Door");
                    this.draggingDoor.MapOffset += deltaMap;
                    EditorUtility.SetDirty(this.draggingDoor);
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp)
            {
                this.draggingRoom = null;
                this.draggingDoor = null;
                this.draggingCenter = false;
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
            {
                var go = Selection.activeGameObject;
                var shape = go != null ? go.GetComponent<MapRoomShape>() : null;
                var marker = go != null ? go.GetComponent<MapDoorMarker>() : null;
                if (shape != null)
                {
                    Undo.RecordObject(shape, "Rotate Map Room");
                    shape.MapRotation = (shape.MapRotation + 90f) % 360f;
                    EditorUtility.SetDirty(shape);
                    e.Use();
                }
                else if (marker != null)
                {
                    Undo.RecordObject(marker, "Rotate Map Door");
                    marker.MapRotation = (marker.MapRotation + 90f) % 360f;
                    EditorUtility.SetDirty(marker);
                    e.Use();
                }
            }
        }

        private GameObject? PickAt(Vector2 mousePos)
        {
            var mapPos = ScreenToMap(mousePos);

            foreach (var marker in FindObjectsByType<MapDoorMarker>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var half = marker.Size * 0.5f;
                var local = mapPos - marker.MapOffset;
                if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y)
                    return marker.gameObject;
            }

            foreach (var shape in FindObjectsByType<MapRoomShape>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (Vector2.Distance(mapPos, shape.MapOffset) * PixelsPerUnit * this.zoom < 400f
                    && ContainsPoint(shape, mapPos))
                    return shape.gameObject;
            }

            return null;
        }

        private static bool ContainsPoint(MapRoomShape shape, Vector2 mapPos)
        {
            var rot = Quaternion.Euler(0f, 0f, shape.MapRotation);
            var local = (Vector2)(rot * (mapPos - shape.MapOffset));
            local = new Vector2(
                shape.MapScale.x != 0 ? local.x / shape.MapScale.x : local.x,
                shape.MapScale.y != 0 ? local.y / shape.MapScale.y : local.y);

            var points = shape.LocalPoints;
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                if ((points[i].y > local.y) != (points[j].y > local.y)
                    && local.x < (points[j].x - points[i].x) * (local.y - points[i].y)
                        / (points[j].y - points[i].y) + points[i].x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
