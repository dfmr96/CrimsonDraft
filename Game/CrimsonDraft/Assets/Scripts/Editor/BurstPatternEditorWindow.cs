#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Editor
{
    public sealed class BurstPatternEditorWindow : EditorWindow
    {
        // ── Constants ──────────────────────────────────────────────────
        private const float LeftPanelWidth   = 240f;
        private const float ShotRadius       = 6f;
        private const float ShotHitRadius    = 10f;
        private const float HandleHitRadius  = 8f;
        private const float HandleHalfSize   = 5f;
        private const float ScatterHalfSize  = 2f;
        private const float DefaultSemiAxisX = 20f;
        private const float DefaultSemiAxisY = 30f;
        private const float MinSemiAxis      = 1f;
        private const float MinPPU            = 4f;
        private const float MaxPPU            = 32f;
        private const float MoveGizmoHitRadius = 14f;
        private const float MoveGizmoArmLen    = 11f;
        private const float TableColIdx        = 22f;
        private const float TableColVal        = 47f;

        private static readonly Color[] ShotColors = new Color[]
        {
            new Color(0.30f, 0.70f, 1.00f),
            new Color(1.00f, 0.50f, 0.20f),
            new Color(0.40f, 0.90f, 0.40f),
            new Color(0.90f, 0.30f, 0.90f),
            new Color(0.90f, 0.90f, 0.20f),
            new Color(1.00f, 0.30f, 0.30f),
        };

        // ── State — data ───────────────────────────────────────────────
        private BurstPatternData?    asset             = null;
        private List<BurstShotEntry> shots             = new();
        private Vector2              shotListScrollPos;

        // ── State — canvas ─────────────────────────────────────────────
        private float   pixelsPerUnit      = 8f;
        private int     selectedIndex      = -1;
        private Vector2 canvasOffset       = Vector2.zero;
        private Sprite? referenceSprite    = null;
        private float   referenceSpriteAlpha = 0.4f;

        // ── State — drag ───────────────────────────────────────────────
        private enum DragTarget { None, ShotCenter, HandleRight, HandleTop }
        private DragTarget dragging       = DragTarget.None;
        private int        dragShotIndex  = -1;
        private Vector2    dragStartMouse;
        private Vector2    dragStartValue;

        // ── State — simulation ─────────────────────────────────────────
        private enum SimState { Idle, Playing, Done }
        private SimState                     simState     = SimState.Idle;
        private int                          simShotIndex = 0;
        private double                       lastShotTime = 0;
        private float                        simDelay     = 0.3f;
        private List<(int idx, Vector2 pos)> scatterDots  = new();

        // ── Label style (lazy — GUIStyle can't be created at static init time) ──
        private GUIStyle? labelStyle;
        private GUIStyle LabelStyle => labelStyle ??= new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 9,
            normal    = { textColor = Color.white },
        };

        // ── Menu ───────────────────────────────────────────────────────

        [MenuItem("Tools/CrimsonDraft/Burst Pattern Editor")]
        private static void Open() => GetWindow<BurstPatternEditorWindow>("Burst Pattern Editor");

        // ── Lifecycle ──────────────────────────────────────────────────

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            if (shots.Count == 0)
                shots.Add(new BurstShotEntry { center = Vector2.zero, semiAxisX = DefaultSemiAxisX, semiAxisY = DefaultSemiAxisY });
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        // ── OnGUI entry ────────────────────────────────────────────────

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLeftPanel();
                DrawCanvasPanel();
            }
        }

        // ── Left panel ─────────────────────────────────────────────────

        private void DrawLeftPanel()
        {
            using var _ = new EditorGUILayout.VerticalScope(GUILayout.Width(LeftPanelWidth));

            EditorGUILayout.Space(4);

            var newAsset = (BurstPatternData?)EditorGUILayout.ObjectField(
                "Pattern Asset", asset, typeof(BurstPatternData), allowSceneObjects: false);
            if (newAsset != asset)
                LoadAsset(newAsset);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Pattern"))
                    CreateNewAsset();
                GUI.enabled = asset != null;
                if (GUILayout.Button("Save"))
                    SaveAsset();
                GUI.enabled = true;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Silueta de Referencia", EditorStyles.boldLabel);
            referenceSprite      = (Sprite?)EditorGUILayout.ObjectField(referenceSprite, typeof(Sprite), allowSceneObjects: false);
            referenceSpriteAlpha = EditorGUILayout.Slider("Alpha", referenceSpriteAlpha, 0f, 1f);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Disparos", EditorStyles.boldLabel);

            // Table
            shotListScrollPos = EditorGUILayout.BeginScrollView(shotListScrollPos, GUILayout.Height(180));

            // Header row
            var headerRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            EditorGUI.DrawRect(headerRect, new Color(0.18f, 0.18f, 0.18f));
            {
                float hx = headerRect.x + TableColIdx;
                var hs = EditorStyles.centeredGreyMiniLabel;
                EditorGUI.LabelField(new Rect(hx, headerRect.y, TableColVal, headerRect.height), "X", hs); hx += TableColVal;
                EditorGUI.LabelField(new Rect(hx, headerRect.y, TableColVal, headerRect.height), "Y", hs); hx += TableColVal;
                EditorGUI.LabelField(new Rect(hx, headerRect.y, TableColVal, headerRect.height), "a", hs); hx += TableColVal;
                EditorGUI.LabelField(new Rect(hx, headerRect.y, TableColVal, headerRect.height), "b", hs);
            }

            // Data rows
            float rowH = EditorGUIUtility.singleLineHeight + 2f;
            for (int i = 0; i < shots.Count; i++)
            {
                var  s     = shots[i];
                bool isSel = (i == selectedIndex);

                var rowRect = EditorGUILayout.GetControlRect(false, rowH);

                if (isSel)
                    EditorGUI.DrawRect(rowRect, new Color(0.25f, 0.45f, 0.85f, 0.3f));

                // Clicking anywhere on the row selects it (don't consume — let FloatFields also get the event)
                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    selectedIndex = i;
                    Repaint();
                }

                float cx = rowRect.x;
                float cy = rowRect.y;

                EditorGUI.LabelField(new Rect(cx, cy, TableColIdx, rowH), $"#{i}");
                cx += TableColIdx;

                GUI.enabled = (i > 0);
                EditorGUI.BeginChangeCheck();
                float nx = EditorGUI.FloatField(new Rect(cx, cy, TableColVal, rowH), s.center.x);   cx += TableColVal;
                float ny = EditorGUI.FloatField(new Rect(cx, cy, TableColVal, rowH), s.center.y);   cx += TableColVal;
                GUI.enabled = true;
                float na = EditorGUI.FloatField(new Rect(cx, cy, TableColVal, rowH), s.semiAxisX);  cx += TableColVal;
                float nb = EditorGUI.FloatField(new Rect(cx, cy, TableColVal, rowH), s.semiAxisY);
                if (EditorGUI.EndChangeCheck())
                {
                    if (i > 0) s.center = new Vector2(nx, ny);
                    s.semiAxisX = Mathf.Max(MinSemiAxis, na);
                    s.semiAxisY = Mathf.Max(MinSemiAxis, nb);
                    shots[i] = s;
                    MarkDirty();
                    Repaint();
                }
            }

            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Agregar Disparo"))
                {
                    shots.Add(new BurstShotEntry
                    {
                        center    = Vector2.zero,
                        semiAxisX = DefaultSemiAxisX,
                        semiAxisY = DefaultSemiAxisY,
                    });
                    selectedIndex = shots.Count - 1;
                    MarkDirty();
                    Repaint();
                }

                GUI.enabled = shots.Count > 1;
                if (GUILayout.Button("− Eliminar Último"))
                {
                    shots.RemoveAt(shots.Count - 1);
                    if (selectedIndex >= shots.Count)
                        selectedIndex = shots.Count - 1;
                    MarkDirty();
                    Repaint();
                }
                GUI.enabled = true;
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Simulación", EditorStyles.boldLabel);

            simDelay = EditorGUILayout.Slider("Delay (s)", simDelay, 0.05f, 2.0f);

            string btnLabel = simState == SimState.Playing ? "Detener" : "Probar Ráfaga";
            if (GUILayout.Button(btnLabel))
            {
                if (simState == SimState.Playing)
                {
                    simState = SimState.Idle;
                }
                else
                {
                    scatterDots.Clear();
                    simShotIndex = 0;
                    lastShotTime = EditorApplication.timeSinceStartup - simDelay;
                    simState     = SimState.Playing;
                }
            }

            if (GUILayout.Button("Limpiar Resultados"))
            {
                scatterDots.Clear();
                simState = SimState.Idle;
                Repaint();
            }
        }

        private void LoadAsset(BurstPatternData? newAsset)
        {
            asset         = newAsset;
            selectedIndex = -1;
            simState      = SimState.Idle;
            canvasOffset  = Vector2.zero;
            shots.Clear();
            scatterDots.Clear();

            if (asset != null)
                shots.AddRange(asset.Shots);
            else
                shots.Add(new BurstShotEntry { center = Vector2.zero, semiAxisX = DefaultSemiAxisX, semiAxisY = DefaultSemiAxisY });

            EnforceConstraints();
            Repaint();
        }

        private void SaveAsset()
        {
            if (asset == null) return;
            EnforceConstraints();
            asset.SetShots(shots.ToArray());
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private void CreateNewAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "New Burst Pattern", "BurstPattern", "asset",
                "Choose location for the new Burst Pattern asset");
            if (string.IsNullOrEmpty(path)) return;

            var newAsset = CreateInstance<BurstPatternData>();
            AssetDatabase.CreateAsset(newAsset, path);
            AssetDatabase.SaveAssets();
            LoadAsset(newAsset);
        }

        // ── Canvas panel ───────────────────────────────────────────────

        private void DrawCanvasPanel()
        {
            var canvasRect = GUILayoutUtility.GetRect(0, 0,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (canvasRect.width < 1 || canvasRect.height < 1) return;

            EditorGUI.DrawRect(canvasRect, new Color(0.15f, 0.15f, 0.15f));

            var origin = new Vector2(
                canvasRect.x + canvasRect.width  / 2f + canvasOffset.x,
                canvasRect.y + canvasRect.height / 2f + canvasOffset.y);

            HandleZoom(canvasRect);

            DrawBackgroundSprite(canvasRect, origin);

            Handles.BeginGUI();
            DrawGrid(canvasRect, origin);
            DrawScatterDots(origin);
            DrawShots(origin);
            Handles.EndGUI();

            DrawShotLabels(origin);

            ProcessCanvasEvents(canvasRect, origin);
        }

        private void DrawBackgroundSprite(Rect canvasRect, Vector2 origin)
        {
            if (referenceSprite == null) return;

            var   tex      = referenceSprite.texture;
            var   tr       = referenceSprite.textureRect;
            float sprPPU   = referenceSprite.pixelsPerUnit;
            float screenW  = (tr.width  / sprPPU) * pixelsPerUnit;
            float screenH  = (tr.height / sprPPU) * pixelsPerUnit;

            var screenRect = new Rect(
                origin.x - screenW / 2f,
                origin.y - screenH / 2f,
                screenW,
                screenH);

            // Clip to canvas bounds
            screenRect = Rect.MinMaxRect(
                Mathf.Max(screenRect.xMin, canvasRect.xMin),
                Mathf.Max(screenRect.yMin, canvasRect.yMin),
                Mathf.Min(screenRect.xMax, canvasRect.xMax),
                Mathf.Min(screenRect.yMax, canvasRect.yMax));

            if (screenRect.width <= 0 || screenRect.height <= 0) return;

            var uvRect = new Rect(
                tr.x      / tex.width,
                tr.y      / tex.height,
                tr.width  / tex.width,
                tr.height / tex.height);

            var prev  = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, referenceSpriteAlpha);
            GUI.DrawTextureWithTexCoords(screenRect, tex, uvRect, alphaBlend: true);
            GUI.color = prev;
        }

        private void HandleZoom(Rect canvasRect)
        {
            var e = Event.current;
            if (e.type != EventType.ScrollWheel) return;
            if (!canvasRect.Contains(e.mousePosition)) return;
            pixelsPerUnit = Mathf.Clamp(pixelsPerUnit - e.delta.y * 0.4f, MinPPU, MaxPPU);
            e.Use();
            Repaint();
        }

        private void DrawGrid(Rect canvasRect, Vector2 origin)
        {
            float wLeft   = (canvasRect.xMin - origin.x) / pixelsPerUnit;
            float wRight  = (canvasRect.xMax - origin.x) / pixelsPerUnit;
            float wBottom = -(canvasRect.yMax - origin.y) / pixelsPerUnit;
            float wTop    = -(canvasRect.yMin - origin.y) / pixelsPerUnit;

            var minorColor = new Color(0.25f, 0.25f, 0.25f);
            var majorColor = new Color(0.35f, 0.35f, 0.35f);
            var axisColor  = new Color(0.55f, 0.55f, 0.55f);

            for (int wx = Mathf.FloorToInt(wLeft); wx <= Mathf.CeilToInt(wRight); wx++)
            {
                float px = origin.x + wx * pixelsPerUnit;
                var   c  = wx == 0 ? axisColor : (wx % 5 == 0 ? majorColor : minorColor);
                DrawLine(new Vector2(px, canvasRect.yMin), new Vector2(px, canvasRect.yMax), c);
            }

            for (int wy = Mathf.FloorToInt(wBottom); wy <= Mathf.CeilToInt(wTop); wy++)
            {
                float py = origin.y - wy * pixelsPerUnit;
                var   c  = wy == 0 ? axisColor : (wy % 5 == 0 ? majorColor : minorColor);
                DrawLine(new Vector2(canvasRect.xMin, py), new Vector2(canvasRect.xMax, py), c);
            }
        }

        private void DrawShots(Vector2 origin)
        {
            EnforceConstraints();
            for (int i = 0; i < shots.Count; i++)
            {
                var  s   = shots[i];
                var  col = ShotColors[i % ShotColors.Length];
                var  wp  = WorldToWindow(s.center, origin);
                bool sel = (i == selectedIndex);

                var ellCol = new Color(col.r, col.g, col.b, sel ? 0.85f : 0.45f);
                DrawEllipse(wp, s.semiAxisX * pixelsPerUnit, s.semiAxisY * pixelsPerUnit, ellCol, sel ? 1.5f : 1f);

                if (sel)
                {
                    var hRight = WorldToWindow(new Vector2(s.center.x + s.semiAxisX, s.center.y), origin);
                    var hTop   = WorldToWindow(new Vector2(s.center.x, s.center.y + s.semiAxisY), origin);
                    DrawLine(wp, hRight, new Color(1f, 1f, 1f, 0.35f));
                    DrawLine(wp, hTop,   new Color(1f, 1f, 1f, 0.35f));
                    DrawFilledSquare(hRight, HandleHalfSize, col);
                    DrawFilledSquare(hTop,   HandleHalfSize, col);
                    DrawCircle(hRight, HandleHalfSize + 1f, col, 1f);
                    DrawCircle(hTop,   HandleHalfSize + 1f, col, 1f);
                }

                DrawCircle(wp, ShotRadius, sel ? Color.white : col, sel ? 2f : 1.5f);

                // Move gizmo — only for selected non-locked shot
                if (sel && i > 0)
                    DrawMoveGizmo(wp, Color.white);
            }
        }

        private static void DrawMoveGizmo(Vector2 center, Color color)
        {
            DrawLine(center + new Vector2(-MoveGizmoArmLen, 0), center + new Vector2(MoveGizmoArmLen, 0), color, 2f);
            DrawLine(center + new Vector2(0, -MoveGizmoArmLen), center + new Vector2(0, MoveGizmoArmLen), color, 2f);
            DrawFilledSquare(center, 3f, color);
        }

        private void DrawScatterDots(Vector2 origin)
        {
            foreach (var (idx, pos) in scatterDots)
            {
                var col = ShotColors[idx % ShotColors.Length];
                col.a = 0.8f;
                DrawFilledSquare(WorldToWindow(pos, origin), ScatterHalfSize, col);
            }
        }

        private void DrawShotLabels(Vector2 origin)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                var wp = WorldToWindow(shots[i].center, origin);
                GUI.Label(new Rect(wp.x - 8f, wp.y - 8f, 16f, 16f), i.ToString(), LabelStyle);
            }
        }

        private void ProcessCanvasEvents(Rect canvasRect, Vector2 origin)
        {
            var e = Event.current;
            if (!canvasRect.Contains(e.mousePosition)) return;

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    HandleMouseDown(e.mousePosition, origin);
                    e.Use();
                    break;

                case EventType.MouseDrag when e.button == 2:
                    canvasOffset += e.delta;
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseDrag when e.button == 0 && dragging != DragTarget.None:
                    HandleMouseDrag(e.mousePosition);
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseUp when e.button == 0:
                    if (dragging != DragTarget.None)
                    {
                        dragging = DragTarget.None;
                        MarkDirty();
                    }
                    e.Use();
                    break;
            }
        }

        private void HandleMouseDown(Vector2 mousePos, Vector2 origin)
        {
            // Priority 1: ellipse handles of the selected shot
            if (selectedIndex >= 0 && selectedIndex < shots.Count)
            {
                var s      = shots[selectedIndex];
                var hRight = WorldToWindow(new Vector2(s.center.x + s.semiAxisX, s.center.y), origin);
                var hTop   = WorldToWindow(new Vector2(s.center.x, s.center.y + s.semiAxisY), origin);

                if (Vector2.Distance(mousePos, hRight) <= HandleHitRadius)
                {
                    dragging       = DragTarget.HandleRight;
                    dragShotIndex  = selectedIndex;
                    dragStartMouse = mousePos;
                    dragStartValue = new Vector2(s.semiAxisX, 0f);
                    return;
                }
                if (Vector2.Distance(mousePos, hTop) <= HandleHitRadius)
                {
                    dragging       = DragTarget.HandleTop;
                    dragShotIndex  = selectedIndex;
                    dragStartMouse = mousePos;
                    dragStartValue = new Vector2(0f, s.semiAxisY);
                    return;
                }

                // Priority 2: move gizmo of selected shot (index > 0 only)
                if (selectedIndex > 0)
                {
                    var wp = WorldToWindow(s.center, origin);
                    if (Vector2.Distance(mousePos, wp) <= MoveGizmoHitRadius)
                    {
                        dragging       = DragTarget.ShotCenter;
                        dragShotIndex  = selectedIndex;
                        dragStartMouse = mousePos;
                        dragStartValue = s.center;
                        return;
                    }
                }
            }

            // Priority 3: any shot circle → selection only, no drag
            for (int i = 0; i < shots.Count; i++)
            {
                var wp = WorldToWindow(shots[i].center, origin);
                if (Vector2.Distance(mousePos, wp) > ShotHitRadius) continue;

                selectedIndex = i;
                Repaint();
                return;
            }

            selectedIndex = -1;
            Repaint();
        }

        private void HandleMouseDrag(Vector2 mousePos)
        {
            var delta = mousePos - dragStartMouse;

            if (dragging == DragTarget.ShotCenter)
            {
                var s    = shots[dragShotIndex];
                s.center = new Vector2(
                    dragStartValue.x + delta.x / pixelsPerUnit,
                    dragStartValue.y - delta.y / pixelsPerUnit);
                shots[dragShotIndex] = s;
            }
            else if (dragging == DragTarget.HandleRight)
            {
                var s       = shots[dragShotIndex];
                s.semiAxisX = Mathf.Max(MinSemiAxis, dragStartValue.x + delta.x / pixelsPerUnit);
                shots[dragShotIndex] = s;
            }
            else if (dragging == DragTarget.HandleTop)
            {
                var s       = shots[dragShotIndex];
                s.semiAxisY = Mathf.Max(MinSemiAxis, dragStartValue.y - delta.y / pixelsPerUnit);
                shots[dragShotIndex] = s;
            }
        }

        // ── Simulation tick ────────────────────────────────────────────

        private void OnEditorUpdate()
        {
            if (simState != SimState.Playing) return;
            if (EditorApplication.timeSinceStartup - lastShotTime < simDelay) return;

            lastShotTime = EditorApplication.timeSinceStartup;

            EnforceConstraints();

            if (simShotIndex >= shots.Count)
            {
                simState = SimState.Done;
                Repaint();
                return;
            }

            var point = BurstPatternData.SamplePoint(shots[simShotIndex]);
            scatterDots.Add((simShotIndex, point));
            simShotIndex++;

            if (simShotIndex >= shots.Count)
                simState = SimState.Done;

            Repaint();
        }

        // ── Coordinate helpers ─────────────────────────────────────────

        private Vector2 WorldToWindow(Vector2 world, Vector2 origin) =>
            new Vector2(origin.x + world.x * pixelsPerUnit,
                        origin.y - world.y * pixelsPerUnit);

        private Vector2 WindowToWorld(Vector2 window, Vector2 origin) =>
            new Vector2((window.x - origin.x) / pixelsPerUnit,
                       -(window.y - origin.y) / pixelsPerUnit);

        // ── Drawing helpers ────────────────────────────────────────────

        private static void DrawLine(Vector2 a, Vector2 b, Color color, float thickness = 1f)
        {
            Handles.color = color;
            Handles.DrawAAPolyLine(thickness, new Vector3(a.x, a.y, 0f), new Vector3(b.x, b.y, 0f));
        }

        private static void DrawCircle(Vector2 center, float radius, Color color, float thickness = 1.5f)
        {
            const int N = 20;
            var pts = new Vector3[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                pts[i] = new Vector3(center.x + Mathf.Cos(a) * radius,
                                     center.y + Mathf.Sin(a) * radius, 0f);
            }
            Handles.color = color;
            Handles.DrawAAPolyLine(thickness, pts);
        }

        private static void DrawEllipse(Vector2 center, float rx, float ry, Color color, float thickness = 1f)
        {
            const int N = 24;
            var pts = new Vector3[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                pts[i] = new Vector3(center.x + Mathf.Cos(a) * rx,
                                     center.y + Mathf.Sin(a) * ry, 0f);
            }
            Handles.color = color;
            Handles.DrawAAPolyLine(thickness, pts);
        }

        private static void DrawFilledSquare(Vector2 center, float halfSize, Color color)
        {
            var verts = new Vector3[]
            {
                new Vector3(center.x - halfSize, center.y - halfSize, 0f),
                new Vector3(center.x + halfSize, center.y - halfSize, 0f),
                new Vector3(center.x + halfSize, center.y + halfSize, 0f),
                new Vector3(center.x - halfSize, center.y + halfSize, 0f),
            };
            Handles.DrawSolidRectangleWithOutline(verts, color, Color.clear);
        }

        // ── Constraints ────────────────────────────────────────────────

        private void EnforceConstraints()
        {
            if (shots.Count == 0)
                shots.Add(new BurstShotEntry { center = Vector2.zero, semiAxisX = DefaultSemiAxisX, semiAxisY = DefaultSemiAxisY });

            var s0 = shots[0];
            s0.center    = Vector2.zero;
            s0.semiAxisX = Mathf.Max(MinSemiAxis, s0.semiAxisX);
            s0.semiAxisY = Mathf.Max(MinSemiAxis, s0.semiAxisY);
            shots[0] = s0;

            for (int i = 1; i < shots.Count; i++)
            {
                var s = shots[i];
                s.semiAxisX = Mathf.Max(MinSemiAxis, s.semiAxisX);
                s.semiAxisY = Mathf.Max(MinSemiAxis, s.semiAxisY);
                shots[i] = s;
            }
        }

        private void MarkDirty()
        {
            if (asset == null) return;
            asset.SetShots(shots.ToArray());
            EditorUtility.SetDirty(asset);
        }
    }
}
