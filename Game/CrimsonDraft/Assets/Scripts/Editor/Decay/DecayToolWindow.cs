#nullable enable

using System.Collections.Generic;
using CrimsonDraft.Rendering.Decay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrimsonDraft.Editor.Decay
{
    // Flujo: elegir una sala (un prefab de sala en la escena; seleccionar cualquier cosa de la
    // sala alcanza) -> "Aplicar deterioro" -> ajustar sliders -> generar decals -> probar Detalle.
    // Todo se guarda en el prefab de la sala y solo afecta a esa sala.
    public sealed class DecayToolWindow : EditorWindow
    {
        private const string MenuPath = "CrimsonDraft/Decay Tool";

        [SerializeField] private int        noiseSeed       = 1;
        [SerializeField] private bool       guessSurface    = true;
        [SerializeField] private bool       followSelection = true;
        [SerializeField] private GameObject? room;

        private UnityEditor.Editor? volumeEditor;
        private Vector2 scroll;
        private bool    showResources;

        [MenuItem(MenuPath)]
        public static void ShowWindow() => GetWindow<DecayToolWindow>("Decay Tool");

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            if (this.volumeEditor != null) DestroyImmediate(this.volumeEditor);
        }

        private void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode) => this.room = null;

        private void OnSelectionChanged()
        {
            if (this.followSelection)
            {
                var selected = DecayRoomTool.RoomOf(Selection.activeGameObject);
                if (selected != null) this.room = selected;
            }
            Repaint();
        }

        private void OnGUI()
        {
            this.scroll = EditorGUILayout.BeginScrollView(this.scroll);

            DrawResources();
            Space();
            DrawRoomPicker();

            if (this.room != null)
            {
                string? problem = DecayRoomTool.Problem(this.room);
                if (problem != null)
                    EditorGUILayout.HelpBox(problem, MessageType.Warning);
                else
                {
                    var volume = DecayRoomTool.VolumeOf(this.room);
                    Space();
                    if (volume == null) DrawApply(this.room);
                    else DrawRoom(this.room, volume);
                }
            }

            Space();
            DrawDetail();
            Space();
            DrawSummary();

            EditorGUILayout.EndScrollView();
        }

        // --- Recursos ------------------------------------------------------------------------

        private void DrawResources()
        {
            var palette = AssetDatabase.LoadAssetAtPath<DecayPalette>(DecayTextureGenerator.PalettePath);
            if (palette == null)
            {
                Header("Recursos");
                EditorGUILayout.HelpBox("Falta crear la paleta y las texturas del deterioro (una sola vez por proyecto).", MessageType.Info);
                if (GUILayout.Button("Crear paleta y texturas")) GenerateResources();
                return;
            }

            this.showResources = EditorGUILayout.Foldout(this.showResources, "Recursos (paleta y texturas)", true);
            if (!this.showResources) return;

            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("Paleta", palette, typeof(DecayPalette), false);
                this.noiseSeed = EditorGUILayout.IntField("Seed de texturas", this.noiseSeed);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Regenerar texturas")) GenerateResources();
                    if (GUILayout.Button("Editar paleta")) Selection.activeObject = palette;
                }
                EditorGUILayout.HelpBox(
                    "Texturas procedurales (128px ruido, 64px decals, filtro point). Para usar las tuyas, reemplazá la " +
                    "de ruido en la paleta o Base_Map en los materiales de Assets/Art/Decay/Decals.", MessageType.None);
            }
        }

        private void GenerateResources()
        {
            var noise   = DecayTextureGenerator.GenerateNoise(this.noiseSeed);
            var palette = DecayTextureGenerator.GetOrCreatePalette();
            palette.NoiseTexture = noise;
            EditorUtility.SetDirty(palette);

            foreach (DecayDecal.Kind kind in System.Enum.GetValues(typeof(DecayDecal.Kind)))
                DecayTextureGenerator.GetOrCreateDecalMaterial(kind, DecayTextureGenerator.GenerateDecalShape(kind, this.noiseSeed));

            AssetDatabase.SaveAssets();
            DecayShaderGlobals.MarkDirty();
            SceneView.RepaintAll();
        }

        // --- Sala ----------------------------------------------------------------------------

        private void DrawRoomPicker()
        {
            Header("Sala");
            var rooms = DecayRoomTool.RoomsInOpenScenes();
            if (rooms.Count == 0)
            {
                EditorGUILayout.HelpBox("No hay prefabs de sala en las escenas abiertas.", MessageType.Info);
                return;
            }

            int current = this.room != null ? rooms.IndexOf(this.room) : -1;
            var labels  = new string[rooms.Count];
            for (int i = 0; i < rooms.Count; i++)
            {
                var v = DecayRoomTool.VolumeOf(rooms[i]);
                labels[i] = v != null ? $"{rooms[i].name}   ({v.Decay:0.00})" : rooms[i].name;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                int picked = EditorGUILayout.Popup(current, labels);
                if (picked != current && picked >= 0) SelectRoom(rooms[picked], frame: true);

                using (new EditorGUI.DisabledScope(this.room == null))
                    if (GUILayout.Button("Ver", GUILayout.Width(40))) SelectRoom(this.room!, frame: true);
            }

            this.followSelection = EditorGUILayout.ToggleLeft(
                new GUIContent("Seguir la selección", "Al seleccionar cualquier objeto de una sala (en la escena o la jerarquía), la tool pasa a esa sala."),
                this.followSelection);

            if (this.room == null)
                EditorGUILayout.HelpBox("Elegí una sala de la lista o seleccioná algo de ella en la escena.", MessageType.None);
        }

        private void SelectRoom(GameObject target, bool frame)
        {
            this.room = target;
            Selection.activeGameObject = target;
            if (frame) SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void DrawApply(GameObject target)
        {
            EditorGUILayout.LabelField(target.name, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Esta sala no tiene deterioro. Se va a agregar al prefab {System.IO.Path.GetFileName(DecayRoomTool.AssetPath(target))} " +
                "y solo va a afectar a sus objetos (menos Interactables).", MessageType.None);

            this.guessSurface = EditorGUILayout.ToggleLeft(
                new GUIContent("Adivinar metal / pintura de los materiales por nombre", "DecayMetal = 0.8 si el nombre sugiere metal; DecayPaint = 0 en telas, madera, vidrio. Se ajusta después."),
                this.guessSurface);

            if (GUILayout.Button($"Aplicar deterioro a {target.name}", GUILayout.Height(28)))
                Run("Aplicar deterioro", () =>
                {
                    DecayRoomTool.Apply(target, DecayTextureGenerator.GetOrCreatePalette(), this.guessSurface);
                    var v = DecayRoomTool.VolumeOf(target);
                    if (v != null) DecayRoomTool.GenerateDecals(target, v, reseed: false);
                });
        }

        private void DrawRoom(GameObject target, DecayVolume volume)
        {
            EditorGUILayout.LabelField($"{target.name}   ·   id {volume.RoomId}", EditorStyles.boldLabel);

            int pending = DecayRoomTool.PendingOverrides(target, volume);
            if (pending > 0)
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"{pending} cambio(s) en la escena sin guardar en el prefab.", EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button("Guardar en el prefab", GUILayout.Width(150)))
                        Run("Guardar en el prefab", () => DecayRoomTool.ApplyOverrides(target, volume));
                }

            // Inspector del volumen de la instancia: cada cambio se lleva al prefab al soltar el mouse.
            UnityEditor.Editor.CreateCachedEditor(volume, typeof(DecayVolumeEditor), ref this.volumeEditor);
            this.volumeEditor.OnInspectorGUI();

            Space();
            Header("Decals");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generar")) Run("Generar decals", () => DecayRoomTool.GenerateDecals(target, volume, reseed: false));
                if (GUILayout.Button("Otra variante")) Run("Generar decals", () => DecayRoomTool.GenerateDecals(target, volume, reseed: true));
                if (GUILayout.Button("Borrar no fijados")) Run("Borrar decals", () => DecayRoomTool.ClearUnlockedDecals(target, volume));
            }
            var selectedDecals = SelectedDecals(volume);
            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(selectedDecals.Count == 0))
            {
                if (GUILayout.Button($"Fijar seleccionados ({selectedDecals.Count})")) SetLocked(target, volume, selectedDecals, true);
                if (GUILayout.Button("Soltar")) SetLocked(target, volume, selectedDecals, false);
            }
            EditorGUILayout.HelpBox(
                "Los decals se pueden mover o borrar a mano en la escena; después \"Guardar en el prefab\". " +
                "Fijados, Generar/Borrar no los tocan. El óxido solo aparece sobre materiales con Metal > 0.3.",
                MessageType.None);

            Space();
            DrawMaterials(volume);

            Space();
            Header("Sala");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Reajustar caja", "Vuelve a calcular piso, techo y paredes desde los hijos Floor/Bounds.")))
                    Run("Reajustar caja", () => DecayRoomTool.RefitBox(target, volume));
                if (GUILayout.Button(new GUIContent("Actualizar objetos", "Después de agregar objetos a la sala o cambiar Excluded: vuelve a marcar qué se ensucia.")))
                    Run("Actualizar objetos", () => DecayRoomTool.StampRenderers(target, volume));
                if (GUILayout.Button("Quitar deterioro") &&
                    EditorUtility.DisplayDialog("Decay Tool", $"¿Quitar el deterioro y sus decals del prefab de {target.name}?", "Quitar", "Cancelar"))
                    Run("Quitar deterioro", () => DecayRoomTool.Remove(target));
            }
        }

        private void DrawMaterials(DecayVolume volume)
        {
            Header("Materiales de la sala");
            var renderers = DecayRoomTool.RoomRenderers(volume);
            var missing   = DecayMaterialMigrator.FindMaterials(renderers, DecayMaterialMigrator.SourceShader);
            var materials = DecayMaterialMigrator.FindMaterials(renderers, DecayMaterialMigrator.DecayShader);

            if (missing.Count > 0)
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"{missing.Count} material(es) todavía con PSXMaster (no se ensucian).", EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button("Pasar a PSXDecay", GUILayout.Width(130)))
                        Run("Migrar materiales", () => DecayMaterialMigrator.Migrate(missing, this.guessSurface));
                }

            EditorGUILayout.LabelField("Compartidos con otras salas: cambiar Metal/Pintura acá cambia en todas.", EditorStyles.miniLabel);
            foreach (var m in materials)
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(m, typeof(Material), false, GUILayout.MinWidth(120));
                    EditorGUI.BeginChangeCheck();
                    float metal = EditorGUILayout.Slider("Metal", m.GetFloat("_DecayMetal"), 0f, 1f);
                    float paint = EditorGUILayout.Slider("Pintura", m.GetFloat("_DecayPaint"), 0f, 1f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(m, "Edit decay material");
                        m.SetFloat("_DecayMetal", metal);
                        m.SetFloat("_DecayPaint", paint);
                        EditorUtility.SetDirty(m);
                    }
                }
        }

        // --- Detalle / resumen ----------------------------------------------------------------

        private static void DrawDetail()
        {
            Header("Detalle (vista previa)");
            EditorGUI.BeginChangeCheck();
            var detail = (DecayDetail)EditorGUILayout.EnumPopup("Detalle", DecayQuality.Current);
            if (EditorGUI.EndChangeCheck())
            {
                DecayQuality.Set(detail);
                SceneView.RepaintAll();
            }
            EditorGUILayout.HelpBox(
                "Bajo: una muestra de ruido por píxel, sin grietas y la mitad de los decals. " +
                "En el juego: GraphicsSettingsService.SetDecayDetail.", MessageType.None);
        }

        private void DrawSummary()
        {
            var volumes = new List<DecayVolume>(DecayVolume.Active);
            if (volumes.Count == 0) return;
            volumes.Sort((a, b) => EditorUtility.NaturalCompare(a.Room.name, b.Room.name));

            Header("Salas con deterioro");
            var seen = new Dictionary<int, DecayVolume>();
            foreach (var v in volumes)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(v.Room.name, EditorStyles.miniButtonLeft, GUILayout.Width(180)))
                        SelectRoom(v.Room.gameObject, frame: true);
                    EditorGUILayout.LabelField($"deterioro {v.Decay:0.00}   id {v.RoomId}", EditorStyles.miniLabel);
                }
                if (seen.TryGetValue(v.RoomId, out var other))
                    EditorGUILayout.HelpBox($"{v.Room.name} y {other.Room.name} tienen el mismo id: quitá y volvé a aplicar el deterioro en una de las dos.", MessageType.Warning);
                else seen[v.RoomId] = v;
            }
        }

        // --- Helpers -------------------------------------------------------------------------

        private static List<DecayDecal> SelectedDecals(DecayVolume volume)
        {
            var result = new List<DecayDecal>();
            foreach (var go in Selection.gameObjects)
                foreach (var d in go.GetComponentsInChildren<DecayDecal>())
                    if (d.transform.IsChildOf(volume.transform)) result.Add(d);
            return result;
        }

        private static void SetLocked(GameObject target, DecayVolume volume, List<DecayDecal> decals, bool locked)
        {
            Undo.RecordObjects(decals.ToArray(), locked ? "Lock decay decals" : "Unlock decay decals");
            foreach (var d in decals)
            {
                d.Locked = locked;
                EditorUtility.SetDirty(d);
            }
            Run("Guardar en el prefab", () => DecayRoomTool.ApplyOverrides(target, volume));
        }

        private static void Run(string title, System.Action action)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Decay Tool", title + "...", 0.5f);
                action();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Decay Tool", $"{title}: {e.Message}", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                DecayShaderGlobals.MarkDirty();
                SceneView.RepaintAll();
            }
            GUIUtility.ExitGUI();
        }

        private static void Header(string text) => EditorGUILayout.LabelField(text, EditorStyles.boldLabel);

        private static void Space() => EditorGUILayout.Space(8);
    }
}
