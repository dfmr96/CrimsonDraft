# Burst Pattern Editor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a standalone Unity EditorWindow for authoring and previewing weapon burst dispersion patterns, backed by a `BurstPatternData` ScriptableObject.

**Architecture:** Two files — a runtime `BurstPatternData` ScriptableObject (in `CrimsonDraft.Combat`) and a `BurstPatternEditorWindow` EditorWindow (in `CrimsonDraft.Editor`). The editor maintains a local `List<BurstShotEntry>` as working state, syncs to the asset only on explicit actions (MouseUp, Save). Drawing uses `Handles.BeginGUI()` / `Handles.EndGUI()` for lines and polygons in pixel space.

**Spec:** `docs/superpowers/specs/2026-05-13-burst-pattern-editor-design.md`

**Tech Stack:** Unity IMGUI (`EditorWindow.OnGUI`), `Handles` for 2D drawing, `EditorApplication.update` for simulation ticks, `AssetDatabase` for save/load.

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `Game/CrimsonDraft/Assets/Scripts/Combat/Data/BurstPatternData.cs` | Create | ScriptableObject + `BurstShotEntry` struct + `SamplePoint` formula |
| `Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs` | Create | Full EditorWindow — layout, drawing, interaction, simulation |

---

## Task 1: BurstPatternData ScriptableObject

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/BurstPatternData.cs`

- [ ] **Step 1: Create the file**

```csharp
// Game/CrimsonDraft/Assets/Scripts/Combat/Data/BurstPatternData.cs
#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    [Serializable]
    public struct BurstShotEntry
    {
        public Vector2 center;
        public float   semiAxisX;
        public float   semiAxisY;
    }

    [CreateAssetMenu(fileName = "BurstPattern", menuName = "CrimsonDraft/Combat/Burst Pattern")]
    public sealed class BurstPatternData : ScriptableObject
    {
        [SerializeField] private BurstShotEntry[] shots = new[]
        {
            new BurstShotEntry { center = Vector2.zero, semiAxisX = 20f, semiAxisY = 30f }
        };

        public BurstShotEntry[] Shots => this.shots;

        public void SetShots(BurstShotEntry[] entries) => this.shots = entries;

        public static Vector2 SamplePoint(in BurstShotEntry entry)
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float r     = Mathf.Sqrt(UnityEngine.Random.value);
            float ax    = Mathf.Max(1f, entry.semiAxisX);
            float ay    = Mathf.Max(1f, entry.semiAxisY);
            return new Vector2(
                entry.center.x + ax * r * Mathf.Cos(angle),
                entry.center.y + ay * r * Mathf.Sin(angle));
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

In Unity Console, confirm no errors after domain reload. Then verify the menu item exists:
`Assets > Create > CrimsonDraft > Combat > Burst Pattern` — it should appear.

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Combat/Data/BurstPatternData.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Combat/Data/BurstPatternData.cs.meta"
git commit -m "feat(combat): add BurstPatternData ScriptableObject and BurstShotEntry"
```

---

## Task 2: EditorWindow Skeleton

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs`

- [ ] **Step 1: Create the skeleton file**

```csharp
// Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs
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
        private const float MinPPU           = 4f;
        private const float MaxPPU           = 32f;

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
        private BurstPatternData?        asset             = null;
        private List<BurstShotEntry>     shots             = new();
        private Vector2                  shotListScrollPos;

        // ── State — canvas ─────────────────────────────────────────────
        private float pixelsPerUnit = 8f;
        private int   selectedIndex = -1;

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

        // ── Left panel (stub) ──────────────────────────────────────────

        private void DrawLeftPanel()
        {
            using var _ = new EditorGUILayout.VerticalScope(GUILayout.Width(LeftPanelWidth));
            EditorGUILayout.LabelField("Burst Pattern Editor", EditorStyles.boldLabel);
        }

        // ── Canvas panel (stub) ────────────────────────────────────────

        private void DrawCanvasPanel()
        {
            var canvasRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(canvasRect, new Color(0.15f, 0.15f, 0.15f));
        }

        // ── Simulation tick (stub) ─────────────────────────────────────

        private void OnEditorUpdate() { }

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
```

- [ ] **Step 2: Verify compilation and window opens**

Check Unity Console: no errors. Open the window via `Tools > CrimsonDraft > Burst Pattern Editor`. Confirm a window appears with a dark-grey canvas on the right and a bold label on the left.

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs.meta"
git commit -m "feat(combat-ui): add BurstPatternEditorWindow skeleton"
```

---

## Task 3: Left Panel — Shot List and Controls

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs` — replace `DrawLeftPanel` stub

- [ ] **Step 1: Replace `DrawLeftPanel` with the full implementation**

Replace the existing `DrawLeftPanel()` method with:

```csharp
private void DrawLeftPanel()
{
    using var _ = new EditorGUILayout.VerticalScope(GUILayout.Width(LeftPanelWidth));

    EditorGUILayout.Space(4);

    // Asset picker
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
    EditorGUILayout.LabelField("Disparos", EditorStyles.boldLabel);

    // Shot list
    shotListScrollPos = EditorGUILayout.BeginScrollView(shotListScrollPos, GUILayout.Height(180));
    for (int i = 0; i < shots.Count; i++)
    {
        var s      = shots[i];
        bool isSel = (i == selectedIndex);
        var  style = isSel ? EditorStyles.helpBox : EditorStyles.label;
        var  label = i == 0
            ? $"#0  (locked)  a={s.semiAxisX:F1} b={s.semiAxisY:F1}"
            : $"#{i}  ({s.center.x:F1},{s.center.y:F1})  a={s.semiAxisX:F1} b={s.semiAxisY:F1}";

        if (GUILayout.Button(label, style))
        {
            selectedIndex = i;
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
            lastShotTime = EditorApplication.timeSinceStartup - simDelay; // fire first shot on next tick immediately
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
```

- [ ] **Step 2: Add `LoadAsset`, `SaveAsset`, `CreateNewAsset` methods**

Add these methods to the class (before the closing brace):

```csharp
private void LoadAsset(BurstPatternData? newAsset)
{
    asset = newAsset;
    shots.Clear();
    scatterDots.Clear();
    simState = SimState.Idle;
    selectedIndex = -1;

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
```

- [ ] **Step 3: Verify in Unity**

Open the window. Confirm:
- Shot list shows `#0 (locked) a=20.0 b=30.0`
- "+ Agregar Disparo" adds a row; "− Eliminar Último" removes it (disabled at 1 shot)
- "New Pattern" opens a Save dialog

- [ ] **Step 4: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs"
git commit -m "feat(combat-ui): add left panel shot list and asset controls"
```

---

## Task 4: Canvas — Grid and Shot Rendering

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs` — replace `DrawCanvasPanel` stub and add drawing sub-methods

- [ ] **Step 1: Replace `DrawCanvasPanel` stub**

Replace the existing `DrawCanvasPanel()` method with:

```csharp
private void DrawCanvasPanel()
{
    var canvasRect = GUILayoutUtility.GetRect(0, 0,
        GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
    if (canvasRect.width < 1 || canvasRect.height < 1) return;

    EditorGUI.DrawRect(canvasRect, new Color(0.15f, 0.15f, 0.15f));

    var origin = new Vector2(
        canvasRect.x + canvasRect.width  / 2f,
        canvasRect.y + canvasRect.height / 2f);

    HandleZoom(canvasRect);

    Handles.BeginGUI();
    DrawGrid(canvasRect, origin);
    DrawScatterDots(origin);
    DrawShots(origin);
    Handles.EndGUI();

    DrawShotLabels(origin);

    ProcessCanvasEvents(canvasRect, origin);
}
```

- [ ] **Step 2: Add `HandleZoom`**

```csharp
private void HandleZoom(Rect canvasRect)
{
    var e = Event.current;
    if (e.type != EventType.ScrollWheel) return;
    if (!canvasRect.Contains(e.mousePosition)) return;
    pixelsPerUnit = Mathf.Clamp(pixelsPerUnit - e.delta.y * 0.4f, MinPPU, MaxPPU);
    e.Use();
    Repaint();
}
```

- [ ] **Step 3: Add `DrawGrid`**

```csharp
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
```

- [ ] **Step 4: Add `DrawShots` and `DrawScatterDots`**

```csharp
private void DrawShots(Vector2 origin)
{
    EnforceConstraints();
    for (int i = 0; i < shots.Count; i++)
    {
        var   s   = shots[i];
        var   col = ShotColors[i % ShotColors.Length];
        var   wp  = WorldToWindow(s.center, origin);
        bool  sel = (i == selectedIndex);

        // Ellipse
        var ellCol = new Color(col.r, col.g, col.b, sel ? 0.85f : 0.45f);
        DrawEllipse(wp, s.semiAxisX * pixelsPerUnit, s.semiAxisY * pixelsPerUnit, ellCol, sel ? 1.5f : 1f);

        // Handles (selected shot only)
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

        // Shot circle
        DrawCircle(wp, ShotRadius, sel ? Color.white : col, sel ? 2f : 1.5f);
    }
}

private void DrawScatterDots(Vector2 origin)
{
    foreach (var (idx, pos) in scatterDots)
    {
        var col = ShotColors[idx % ShotColors.Length];
        col.a = 0.8f;
        var wp = WorldToWindow(pos, origin);
        DrawFilledSquare(wp, ScatterHalfSize, col);
    }
}
```

- [ ] **Step 5: Add `DrawShotLabels`**

Note: GUI.Label must be called outside `Handles.BeginGUI()`/`EndGUI()` — it is, since `DrawShotLabels` is called after `Handles.EndGUI()`.

```csharp
private static readonly GUIStyle LabelStyle = new GUIStyle
{
    alignment = TextAnchor.MiddleCenter,
    fontSize  = 9,
    normal    = { textColor = Color.white },
};

private void DrawShotLabels(Vector2 origin)
{
    for (int i = 0; i < shots.Count; i++)
    {
        var wp = WorldToWindow(shots[i].center, origin);
        GUI.Label(new Rect(wp.x - 8f, wp.y - 8f, 16f, 16f), i.ToString(), LabelStyle);
    }
}
```

- [ ] **Step 6: Add `ProcessCanvasEvents` stub (needed to compile)**

```csharp
private void ProcessCanvasEvents(Rect canvasRect, Vector2 origin) { }
```

- [ ] **Step 7: Verify in Unity**

Open window. Add 2-3 shots via the button. Confirm:
- Dark background with visible grid lines
- Shot circles appear at origin (they'll all be at 0,0 until drag is wired)
- Ellipses drawn around each shot
- Scroll wheel zooms in/out
- Selecting a row in the left panel highlights the corresponding shot circle (white outline) and shows its handles

- [ ] **Step 8: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs"
git commit -m "feat(combat-ui): add canvas grid, ellipse, and shot rendering"
```

---

## Task 5: Canvas Interaction — Drag, Selection, Zoom

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs` — replace `ProcessCanvasEvents` stub

- [ ] **Step 1: Replace `ProcessCanvasEvents` stub with full implementation**

```csharp
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
```

- [ ] **Step 2: Add `HandleMouseDown`**

```csharp
private void HandleMouseDown(Vector2 mousePos, Vector2 origin)
{
    // Priority 1: handles of the selected shot
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
    }

    // Priority 2: shot circles
    for (int i = 0; i < shots.Count; i++)
    {
        var wp = WorldToWindow(shots[i].center, origin);
        if (Vector2.Distance(mousePos, wp) > ShotHitRadius) continue;

        selectedIndex = i;
        if (i > 0) // shot #0 center is locked
        {
            dragging       = DragTarget.ShotCenter;
            dragShotIndex  = i;
            dragStartMouse = mousePos;
            dragStartValue = shots[i].center;
        }
        Repaint();
        return;
    }

    // Click on empty canvas: deselect
    selectedIndex = -1;
    Repaint();
}
```

- [ ] **Step 3: Add `HandleMouseDrag`**

```csharp
private void HandleMouseDrag(Vector2 mousePos)
{
    var delta = mousePos - dragStartMouse;

    if (dragging == DragTarget.ShotCenter)
    {
        var s    = shots[dragShotIndex];
        s.center = new Vector2(
            dragStartValue.x + delta.x / pixelsPerUnit,
            dragStartValue.y - delta.y / pixelsPerUnit); // GUI Y is inverted vs world Y
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
        // Moving mouse up (negative delta.y) increases semiAxisY
        s.semiAxisY = Mathf.Max(MinSemiAxis, dragStartValue.y - delta.y / pixelsPerUnit);
        shots[dragShotIndex] = s;
    }
}
```

- [ ] **Step 4: Verify in Unity**

Open window. Add 2 shots. Confirm:
- Clicking a shot circle selects it (white outline + handles appear)
- Dragging shot #1 moves it on the canvas; left panel row updates coordinates
- Dragging the right handle stretches the ellipse horizontally
- Dragging the top handle stretches the ellipse vertically
- Shot #0 cannot be dragged (center stays at origin); its ellipse handles still work

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs"
git commit -m "feat(combat-ui): add canvas drag interaction for shots and ellipse handles"
```

---

## Task 6: Save / Load

This task has no new code beyond what's already written in Task 3 (`LoadAsset`, `SaveAsset`, `CreateNewAsset`). This task verifies the full save/load round-trip.

- [ ] **Step 1: Verify New Pattern creates an asset**

1. Open the window
2. Click "New Pattern" → choose a location (e.g., `Assets/`)
3. Confirm the `.asset` file appears in the Project window
4. Confirm the ObjectField now shows the asset

- [ ] **Step 2: Verify Save persists data**

1. Add 2 extra shots and drag them to non-zero positions
2. Click Save
3. Close and reopen the window (or reopen Unity)
4. Drag the asset into the ObjectField
5. Confirm shots appear in the same positions

- [ ] **Step 3: Verify loading an existing asset**

1. With an asset loaded, drag a different `BurstPatternData` asset into the ObjectField
2. Confirm the shots list immediately reflects the new asset's data

- [ ] **Step 4: Commit (no code changes — just verification note)**

```bash
git commit --allow-empty -m "test(combat-ui): verify save/load round-trip for BurstPatternData"
```

---

## Task 7: Simulation

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs` — replace `OnEditorUpdate` stub

- [ ] **Step 1: Replace `OnEditorUpdate` stub**

```csharp
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
```

- [ ] **Step 2: Verify simulation in Unity**

1. Open window, load or create a pattern with 3+ shots at different positions with different ellipse sizes
2. Click "Probar Ráfaga"
3. Confirm: dots appear one by one with the configured delay; each dot color matches its shot's color; dots land inside the visible ellipse of that shot
4. Click "Detener" mid-sequence — confirm playback stops (no more dots appear)
5. Click "Probar Ráfaga" again — confirm dots clear and replay from shot #0
6. Let it run to completion — confirm "Probar Ráfaga" label returns (Done → replays on next press)
7. Click "Limpiar Resultados" — confirm all scatter dots disappear and state returns to Idle

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Editor/BurstPatternEditorWindow.cs"
git commit -m "feat(combat-ui): add animated burst simulation with EditorApplication.update"
```

---

## Self-Review

### Spec coverage check

| Spec requirement | Task |
|---|---|
| BurstShotEntry: center, semiAxisX, semiAxisY | Task 1 |
| Shot #0 center forced to (0,0) | Task 1 (SetShots/EnforceConstraints) + Task 5 |
| SamplePoint uniform ellipse formula | Task 1 |
| EditorWindow menu item | Task 2 |
| Left panel: ObjectField, New, Save | Task 3 |
| Left panel: shot list, Add, Remove Last | Task 3 |
| Left panel: Delay slider, Probar Ráfaga, Limpiar | Task 3 |
| Canvas: grid, shot circles, ellipses, numbered labels | Task 4 |
| Scroll zoom (4–32 px/u) | Task 4 |
| Handles: right (semiAxisX), top (semiAxisY) | Task 4 |
| Drag shot center (index ≥ 1) | Task 5 |
| Drag handles (priority order) | Task 5 |
| Click to select | Task 5 |
| New Pattern: SaveFilePanelInProject + CreateAsset | Task 3 |
| Save: SetDirty + SaveAssets | Task 3 |
| Load: ObjectField replaces shots list | Task 3 |
| Simulation: animated per-shot with delay | Task 7 |
| Simulation state machine: Idle/Playing/Done | Task 7 |
| Scatter dots persist until cleared | Task 7 |
| Detener → Idle without clearing dots | Task 7 |
| Isolated from AimViewController/WeaponData | All — no such imports |

All requirements covered. No gaps found.
