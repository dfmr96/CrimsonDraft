#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Lays out a floor's room sprites on a free canvas. Position is each room's
    /// bottom-left corner in map pixels; dragging snaps to the grid and, with Magnet on, to
    /// nearby room edges (Alt inverts the magnet while dragging). Edits the MapData asset only
    /// (never the scene): sprites, pixel position and 90° rotation. Rooms and their pickup/door
    /// ids come from the scene bake (MapBaker); rooms no longer in the scene show as orphans.</summary>
    public sealed class MapEditorWindow : EditorWindow
    {
        private const string SnapPrefKey   = "CrimsonDraft.MapEditor.Snap";
        private const string MagnetPrefKey = "CrimsonDraft.MapEditor.Magnet";
        private const float  ListWidth   = 280f;
        private const float  MinZoom     = 0.1f;
        private const float  MaxZoom     = 8f;
        private static readonly int[]    SnapOptions = { 1, 4, 8 };
        private static readonly string[] SnapLabels  = { "Snap 1px", "Snap 4px", "Snap 8px" };

        [SerializeField] private MapData? map;
        [SerializeField] private string   selectedRoomId = "";
        [SerializeField] private Vector2  pan;
        [SerializeField] private float    zoom = 1f;
        [SerializeField] private int      snap = 1;
        [SerializeField] private bool     previewComplete;
        [SerializeField] private bool     magnet = true;
        [SerializeField] private Vector2  listScroll;

        private bool       dragging;
        private Vector2    dragStartMouse;
        private Vector2Int dragStartPosition;

        [MenuItem("Tools/CrimsonDraft/Map Editor")]
        public static void Open()
        {
            var window = GetWindow<MapEditorWindow>("Map Editor");
            window.minSize = new Vector2(720f, 420f);
        }

        private void OnEnable()
        {
            this.snap   = EditorPrefs.GetInt(SnapPrefKey, this.snap);
            this.magnet = EditorPrefs.GetBool(MagnetPrefKey, this.magnet);
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable() => Undo.undoRedoPerformed -= Repaint;

        private void OnGUI()
        {
            DrawToolbar();

            if (this.map == null)
            {
                EditorGUILayout.HelpBox("Pick a MapData asset in the toolbar.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawRoomPanel(this.map);
            var canvas = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndHorizontal();

            GUI.BeginGroup(canvas);
            var local = new Rect(Vector2.zero, canvas.size);
            HandleCanvasInput(this.map, local);
            DrawCanvas(this.map, local);
            GUI.EndGroup();
        }

        // ---------- Toolbar ----------

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var maps   = AssetDatabase.FindAssets("t:MapData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<MapData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(m => m != null)
                .ToArray();
            int index  = System.Array.IndexOf(maps, this.map);
            int picked = EditorGUILayout.Popup(index, maps.Select(m => m.name).ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(200f));
            if (picked != index && picked >= 0)
            {
                this.map = maps[picked];
                this.selectedRoomId = "";
            }

            this.previewComplete = GUILayout.Toggle(this.previewComplete, this.previewComplete ? "Preview: Complete" : "Preview: Incomplete", EditorStyles.toolbarButton, GUILayout.Width(130f));

            int snapIndex = Mathf.Max(0, System.Array.IndexOf(SnapOptions, this.snap));
            EditorGUI.BeginChangeCheck();
            snapIndex = EditorGUILayout.Popup(snapIndex, SnapLabels, EditorStyles.toolbarPopup, GUILayout.Width(80f));
            if (EditorGUI.EndChangeCheck())
            {
                this.snap = SnapOptions[snapIndex];
                EditorPrefs.SetInt(SnapPrefKey, this.snap);
            }

            if (GUILayout.Button("Frame", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                Frame();

            EditorGUI.BeginChangeCheck();
            this.magnet = GUILayout.Toggle(this.magnet, new GUIContent("Magnet",
                $"Snap dragged room edges to other rooms' edges within {MapAlign.DefaultTolerance}px. Hold Alt to invert while dragging."),
                EditorStyles.toolbarButton, GUILayout.Width(60f));
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetBool(MagnetPrefKey, this.magnet);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void Frame()
        {
            if (this.map == null) return;
            var bounds = MapLayoutBounds.Compute(MapRoomVisuals.Preview(this.map, this.previewComplete));
            this.pan = new Vector2(-bounds.center.x, bounds.center.y) * this.zoom;
        }

        private static Vector2Int Magnet(IReadOnlyList<MapRoomVisual> visuals, string movingId, RectInt moving)
            => MapAlign.SnapOffset(
                moving,
                visuals.Where(v => v.RoomId != movingId).Select(MapLayoutBounds.RectOf),
                MapAlign.DefaultTolerance);

        // ---------- Room list + inspector ----------

        private void DrawRoomPanel(MapData target)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));
            var standard = MapSpriteStandard.Majority(target.Rooms.SelectMany(r => new[] { r.IncompleteSprite, r.CompleteSprite }));

            this.listScroll = EditorGUILayout.BeginScrollView(this.listScroll, GUILayout.ExpandHeight(true));
            foreach (var room in target.Rooms)
            {
                var style = room.RoomId == this.selectedRoomId ? EditorStyles.boldLabel : EditorStyles.label;
                if (GUILayout.Button($"{Status(room, standard)}  {room.RoomId}", style))
                    this.selectedRoomId = room.RoomId;
            }
            EditorGUILayout.EndScrollView();

            var selected = target.Rooms.FirstOrDefault(r => r.RoomId == this.selectedRoomId);
            if (selected != null)
                DrawRoomInspector(target, selected, standard);

            EditorGUILayout.EndVertical();
        }

        private static string Status(MapRoomData room, (float PixelsPerUnit, FilterMode Filter)? standard)
        {
            if (room.IsOrphan) return "✖";
            if (room.IncompleteSprite == null || room.CompleteSprite == null) return "⚠";
            if (standard != null && (!MapSpriteStandard.Matches(room.IncompleteSprite, standard.Value) || !MapSpriteStandard.Matches(room.CompleteSprite, standard.Value))) return "⚠ PPU";
            return "✔";
        }

        private void DrawRoomInspector(MapData target, MapRoomData room, (float PixelsPerUnit, FilterMode Filter)? standard)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(room.RoomId, EditorStyles.boldLabel);
            if (room.IsOrphan)
                EditorGUILayout.HelpBox("Not found in the scene at the last bake.", MessageType.Warning);

            bool hadSprite = room.IncompleteSprite != null || room.CompleteSprite != null;

            EditorGUI.BeginChangeCheck();
            var incomplete = (Sprite?)EditorGUILayout.ObjectField("Incomplete", room.IncompleteSprite, typeof(Sprite), false);
            var complete   = (Sprite?)EditorGUILayout.ObjectField("Complete",   room.CompleteSprite,   typeof(Sprite), false);
            var position   = EditorGUILayout.Vector2IntField("Position", room.Position);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Edit map room");
                room.IncompleteSprite = incomplete;
                room.CompleteSprite   = complete;
                room.Position         = position;
                if (!hadSprite && (incomplete != null || complete != null))
                    room.Position = VisibleCentre();
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⟲ 90°")) Rotate(target, room, +1);
            if (GUILayout.Button("⟳ 90°")) Rotate(target, room, -1);
            EditorGUILayout.EndHorizontal();

            if (standard != null)
            {
                FixButton(room.IncompleteSprite, standard.Value);
                FixButton(room.CompleteSprite, standard.Value);
            }

            if (room.IsOrphan && GUILayout.Button("Delete orphan"))
            {
                Undo.RecordObject(target, "Delete map room");
                target.EditorRemoveRoom(room.RoomId);
                this.selectedRoomId = "";
                EditorUtility.SetDirty(target);
            }
        }

        private static void Rotate(MapData target, MapRoomData room, int quarterTurns)
        {
            Undo.RecordObject(target, "Rotate map room");
            room.QuarterTurns = ((room.QuarterTurns + quarterTurns) % 4 + 4) % 4;
            EditorUtility.SetDirty(target);
        }

        private static void FixButton(Sprite? sprite, (float PixelsPerUnit, FilterMode Filter) standard)
        {
            if (sprite == null || MapSpriteStandard.Matches(sprite, standard))
                return;

            if (!GUILayout.Button($"Fix {sprite.name} → {standard.PixelsPerUnit} PPU, {standard.Filter}"))
                return;

            var path = AssetDatabase.GetAssetPath(sprite);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return;

            importer.spritePixelsPerUnit = standard.PixelsPerUnit;
            importer.filterMode          = standard.Filter;
            importer.SaveAndReimport();
            Debug.LogWarning($"[MapEditor] {path}: set to {standard.PixelsPerUnit} PPU, {standard.Filter} to match the floor.", sprite);
        }

        // ---------- Canvas ----------

        private Vector2 ToScreen(Rect canvas, Vector2 map)
            => canvas.center + this.pan + new Vector2(map.x, -map.y) * this.zoom;

        private Vector2 ToMap(Rect canvas, Vector2 screen)
        {
            var v = (screen - canvas.center - this.pan) / this.zoom;
            return new Vector2(v.x, -v.y);
        }

        private Vector2Int VisibleCentre()
        {
            var centre = new Vector2(-this.pan.x, this.pan.y) / this.zoom;
            return Snap(centre);
        }

        private Vector2Int DragMagnet(MapData target, MapRoomData room, Vector2Int position)
        {
            var visuals = MapRoomVisuals.Preview(target, this.previewComplete);
            var moving  = visuals.FirstOrDefault(v => v.RoomId == room.RoomId);
            if (moving.Sprite == null) return Vector2Int.zero;

            var footprint = MapLayoutBounds.RectOf(moving);
            footprint.position = position;
            return Magnet(visuals, room.RoomId, footprint);
        }

        private Vector2Int Snap(Vector2 p)
            => new(Mathf.RoundToInt(p.x / this.snap) * this.snap, Mathf.RoundToInt(p.y / this.snap) * this.snap);

        // Position is the footprint's bottom-left corner (map y up), so the screen rect's
        // top-left is the map rect's (xMin, yMax).
        private Rect ScreenRect(Rect canvas, MapRoomVisual visual)
        {
            var footprint = MapLayoutBounds.RectOf(visual);
            return new Rect(ToScreen(canvas, new Vector2(footprint.xMin, footprint.yMax)), (Vector2)footprint.size * this.zoom);
        }

        private void DrawCanvas(MapData target, Rect canvas)
        {
            EditorGUI.DrawRect(canvas, new Color(0.13f, 0.13f, 0.13f));

            var visuals = MapRoomVisuals.Preview(target, this.previewComplete);
            foreach (var visual in visuals)
                DrawSprite(canvas, visual);

            foreach (var visual in visuals)
                if (visual.RoomId == this.selectedRoomId)
                    DrawOutline(ScreenRect(canvas, visual), Color.yellow);

            if (visuals.Count > 0)
            {
                var c = ToScreen(canvas, MapLayoutBounds.Compute(visuals).center);
                EditorGUI.DrawRect(new Rect(c.x - 6f, c.y - 0.5f, 12f, 1f), Color.cyan);
                EditorGUI.DrawRect(new Rect(c.x - 0.5f, c.y - 6f, 1f, 12f), Color.cyan);
            }
        }

        private void DrawSprite(Rect canvas, MapRoomVisual visual)
        {
            var sprite  = visual.Sprite;
            var size    = sprite.rect.size * this.zoom;
            var centre  = ScreenRect(canvas, visual).center;
            var rect    = new Rect(centre - size * 0.5f, size);
            var tex     = sprite.texture;
            var uv      = new Rect(
                sprite.textureRect.x / tex.width,
                sprite.textureRect.y / tex.height,
                sprite.textureRect.width / tex.width,
                sprite.textureRect.height / tex.height);

            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(-visual.QuarterTurns * 90f, centre);
            GUI.DrawTextureWithTexCoords(rect, tex, uv);
            GUI.matrix = matrix;
        }

        private static void DrawOutline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.yMin, 1f, rect.height), color);
        }

        private void HandleCanvasInput(MapData target, Rect canvas)
        {
            var e = Event.current;
            if (!canvas.Contains(e.mousePosition) && !this.dragging)
                return;

            switch (e.type)
            {
                case EventType.ScrollWheel:
                {
                    var before = ToMap(canvas, e.mousePosition);
                    this.zoom = Mathf.Clamp(this.zoom * (1f - e.delta.y * 0.05f), MinZoom, MaxZoom);
                    var after = ToScreen(canvas, before);
                    this.pan += e.mousePosition - after;
                    e.Use();
                    break;
                }
                case EventType.MouseDrag when e.button == 2:
                    this.pan += e.delta;
                    e.Use();
                    break;
                case EventType.MouseDown when e.button == 0:
                {
                    var hit = MapRoomVisuals.Preview(target, this.previewComplete)
                        .Reverse()
                        .FirstOrDefault(v => ScreenRect(canvas, v).Contains(e.mousePosition));
                    this.selectedRoomId = hit.Sprite != null ? hit.RoomId : "";
                    var room = target.Rooms.FirstOrDefault(r => r.RoomId == this.selectedRoomId);
                    if (room != null)
                    {
                        Undo.RecordObject(target, "Move map room");
                        this.dragging          = true;
                        this.dragStartMouse    = e.mousePosition;
                        this.dragStartPosition = room.Position;
                    }
                    e.Use();
                    break;
                }
                case EventType.MouseDrag when e.button == 0 && this.dragging:
                {
                    var room = target.Rooms.FirstOrDefault(r => r.RoomId == this.selectedRoomId);
                    if (room != null)
                    {
                        var delta = (e.mousePosition - this.dragStartMouse) / this.zoom;
                        var moved = Snap(this.dragStartPosition + new Vector2(delta.x, -delta.y));
                        if (this.magnet != e.alt)
                            moved += DragMagnet(target, room, moved);
                        if (moved != room.Position)
                        {
                            room.Position = moved;
                            EditorUtility.SetDirty(target);
                        }
                    }
                    e.Use();
                    break;
                }
                case EventType.MouseUp when e.button == 0:
                    this.dragging = false;
                    e.Use();
                    break;
            }

            if (e.type == EventType.Used)
                Repaint();
        }
    }
}
