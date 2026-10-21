#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CrimsonDraft.Editor
{
    /// <summary>
    /// Prepara luces y props de la selección para que se vean las sombras en tiempo real.
    /// Revisa cada objeto antes de tocarlo: lo que ya está listo se informa y no se vuelve a modificar.
    /// </summary>
    public sealed class ShadowSetupWindow : EditorWindow
    {
        private const string MenuPath = "CrimsonDraft/Shadow Setup";

        // Propiedades serializadas (se editan por SerializedObject para que los overrides de prefab queden bien).
        private const string PropShadowType     = "m_Shadows.m_Type";
        private const string PropShadowStrength = "m_Shadows.m_Strength";
        private const string PropShadowNear     = "m_Shadows.m_NearPlane";
        private const string PropShadowTier     = "m_AdditionalLightsShadowResolutionTier"; // vive en UniversalAdditionalLightData
        private const string PropCastShadows    = "m_CastShadows";
        private const string PropReceiveShadows = "m_ReceiveShadows";

        private const string UrpLightDataType   = "UniversalAdditionalLightData";
        private const string PsxEnableShadows   = "_EnableShadows";
        private const string PsxCastShadows     = "_CastShadows";
        private const string LitReceiveShadows  = "_ReceiveShadows";
        private const string LitReceiveOffKw    = "_RECEIVE_SHADOWS_OFF";
        private const string ShadowCasterPass   = "ShadowCaster";

        private static readonly string[] TierNames = { "Low", "Medium", "High" };

        private enum SaveTarget
        {
            PrefabBase,  // prefab más cercano (ej. Lamp4.prefab, Desk1.prefab)
            PrefabSala,  // prefab más externo (ej. Seaman_Room_2.prefab)
            Instancia,   // solo override en la escena / prefab abierto
        }

        private static readonly string[] SaveTargetLabels =
        {
            "Prefab base (Lamp4, Desk1…)",
            "Prefab de la sala (raíz)",
            "Solo esta instancia",
        };

        [SerializeField] private bool       includeChildren   = true;
        [SerializeField] private bool       includeInactive   = true;
        [SerializeField] private SaveTarget saveTarget        = SaveTarget.PrefabBase;

        [SerializeField] private LightShadows shadowType      = LightShadows.Hard;
        [SerializeField] private float        shadowStrength  = 1f;
        [SerializeField] private float        maxNearPlane    = 0.2f;
        [SerializeField] private int          resolutionTier  = 2;
        [SerializeField] private bool         fixtureCastOff  = true;

        [SerializeField] private bool includeMaterials = true;

        [SerializeField] private bool showLights    = true;
        [SerializeField] private bool showProps     = true;
        [SerializeField] private bool showMaterials = true;
        [SerializeField] private bool showReady;

        private readonly List<Entry> lights    = new();
        private readonly List<Entry> props     = new();
        private readonly List<Entry> materials = new();
        private int     selectedRootCount;
        private bool    isFixing;
        private string  lastResult = string.Empty;
        private Vector2 scrollPos;

        /// <summary>Un objeto revisado: qué le falta (vacío = listo) y si se puede arreglar.</summary>
        private sealed class Entry
        {
            public readonly Object       Target;
            public readonly string       Label;
            public readonly List<string> Issues = new();
            public          string?      Note;
            public          bool         Editable = true;

            public Entry(Object target, string label)
            {
                this.Target = target;
                this.Label  = label;
            }

            public bool IsReady => this.Issues.Count == 0;
        }

        [MenuItem(MenuPath)]
        public static void ShowWindow() => GetWindow<ShadowSetupWindow>("Shadow Setup");

        private void OnEnable()
        {
            Undo.undoRedoPerformed += this.Rescan;
            this.Rescan();
        }

        private void OnDisable() => Undo.undoRedoPerformed -= this.Rescan;

        private void OnSelectionChange()
        {
            this.lastResult = string.Empty;
            this.Rescan();
        }

        private void OnFocus() => this.Rescan();

        private void OnHierarchyChange() => this.Rescan();

        private bool HasStaleEntries()
        {
            foreach (var list in new[] { this.lights, this.props, this.materials })
                foreach (var e in list)
                    if (e.Target == null) return true;
            return false;
        }

        // ───────────────────────────── UI ─────────────────────────────

        private void OnGUI()
        {
            if (Event.current.type == EventType.Layout && this.HasStaleEntries())
                this.Rescan();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Shadow Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Seleccioná luces, props o una sala entera (en la escena o en Prefab Mode). " +
                "La ventana revisa qué le falta a cada objeto para tener sombras y solo toca lo que no está listo.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            this.includeChildren = EditorGUILayout.Toggle("Incluir hijos", this.includeChildren);
            this.includeInactive = EditorGUILayout.Toggle("Incluir inactivos", this.includeInactive);
            this.saveTarget      = (SaveTarget)EditorGUILayout.Popup("Guardar cambios en", (int)this.saveTarget, SaveTargetLabels);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Luces", EditorStyles.miniBoldLabel);
            this.shadowType     = (LightShadows)EditorGUILayout.EnumPopup("Shadow Type", this.shadowType);
            if (this.shadowType == LightShadows.None) this.shadowType = LightShadows.Hard;
            this.shadowStrength = EditorGUILayout.Slider("Shadow Strength", this.shadowStrength, 0f, 1f);
            this.maxNearPlane   = EditorGUILayout.FloatField(new GUIContent("Near Plane máximo", "Si el Near Plane es mayor, los objetos cercanos a la luz no proyectan sombra."), this.maxNearPlane);
            this.resolutionTier = EditorGUILayout.Popup("Shadow Resolution", this.resolutionTier, TierNames);
            this.fixtureCastOff = EditorGUILayout.Toggle(new GUIContent("Apagar sombra de la lámpara", "La pieza que contiene la luz (padre con MeshRenderer) no proyecta sombra; si no, deja una mancha en la pared."), this.fixtureCastOff);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Props", EditorStyles.miniBoldLabel);
            this.includeMaterials = EditorGUILayout.Toggle(new GUIContent("Tocar materiales", "Los materiales son compartidos: el cambio afecta a todos los objetos que los usan."), this.includeMaterials);
            if (EditorGUI.EndChangeCheck())
                this.Rescan();

            this.DrawPipelineWarnings();

            EditorGUILayout.Space(6);

            if (this.selectedRootCount == 0)
            {
                EditorGUILayout.HelpBox("No hay GameObjects seleccionados.", MessageType.None);
                return;
            }

            int pendingLights    = CountPending(this.lights);
            int pendingProps     = CountPending(this.props);
            int pendingMaterials = this.includeMaterials ? CountPending(this.materials) : 0;
            int pendingTotal     = pendingLights + pendingProps + pendingMaterials;

            if (pendingTotal == 0)
                EditorGUILayout.HelpBox("Todo lo seleccionado ya tiene las sombras configuradas.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledGroupScope(pendingTotal == 0))
            {
                if (GUILayout.Button($"Preparar todo ({pendingTotal})", GUILayout.Height(30)))
                {
                    this.Fix(this.lights, this.props, this.includeMaterials ? this.materials : null);
                    GUIUtility.ExitGUI();
                }
            }
            if (GUILayout.Button("Refrescar", GUILayout.Width(80), GUILayout.Height(30)))
                this.Rescan();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(this.lastResult))
                EditorGUILayout.HelpBox(this.lastResult, MessageType.None);

            this.showReady = EditorGUILayout.ToggleLeft("Mostrar también los que ya están listos", this.showReady);

            this.scrollPos = EditorGUILayout.BeginScrollView(this.scrollPos);
            this.DrawSection("Luces", this.lights, ref this.showLights, () => this.Fix(this.lights, null, null));
            this.DrawSection("Props", this.props, ref this.showProps, () => this.Fix(null, this.props, null));
            if (this.includeMaterials)
                this.DrawSection("Materiales (compartidos)", this.materials, ref this.showMaterials, () => this.Fix(null, null, this.materials));
            EditorGUILayout.EndScrollView();
        }

        private void DrawPipelineWarnings()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null) return;

            var so        = new SerializedObject(rp);
            var supported = so.FindProperty("m_AdditionalLightShadowsSupported");
            if (supported != null && !supported.boolValue)
                EditorGUILayout.HelpBox($"El URP Asset \"{rp.name}\" tiene desactivadas las sombras de luces adicionales: ninguna luz puntual o spot va a dar sombra.", MessageType.Error);

            int shadowed = 0;
            foreach (var e in this.lights)
                if (e.Target is Light l && l.type != LightType.Directional && l.gameObject.activeInHierarchy) shadowed++;

            var atlas = so.FindProperty("m_AdditionalLightsShadowmapResolution");
            var high  = so.FindProperty("m_AdditionalLightsShadowResolutionTierHigh");
            if (atlas == null || high == null || shadowed == 0) return;

            int perAtlas = Mathf.Max(1, (atlas.intValue / Mathf.Max(1, high.intValue)) * (atlas.intValue / Mathf.Max(1, high.intValue)));
            if (this.resolutionTier == 2 && shadowed > perAtlas)
                EditorGUILayout.HelpBox(
                    $"Hay {shadowed} luces activas en la selección. Con resolución High el atlas de {atlas.intValue} entra ~{perAtlas} sombras de spot a la vez " +
                    "(una point light usa 6). Si se ven a la vez, URP les baja la calidad o deja alguna sin sombra.",
                    MessageType.Warning);
        }

        private void DrawSection(string title, List<Entry> entries, ref bool expanded, Action fixAll)
        {
            int pending = CountPending(entries);
            int ready   = entries.Count - pending;

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            expanded = EditorGUILayout.Foldout(expanded, $"{title}  —  {ready} listos, {pending} pendientes", true, EditorStyles.foldoutHeader);
            using (new EditorGUI.DisabledGroupScope(pending == 0))
            {
                if (GUILayout.Button(pending == 0 ? "Listo" : $"Preparar {pending}", GUILayout.Width(100)))
                {
                    fixAll();
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!expanded) return;

            foreach (var entry in entries)
            {
                if (entry.IsReady && !this.showReady) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                var icon = EditorGUIUtility.IconContent(entry.IsReady ? "TestPassed" : (entry.Editable ? "console.warnicon.sml" : "console.erroricon.sml"));
                GUILayout.Label(icon, GUILayout.Width(18), GUILayout.Height(18));
                EditorGUILayout.LabelField(entry.Label, entry.IsReady ? EditorStyles.label : EditorStyles.boldLabel);
                if (GUILayout.Button("Ping", GUILayout.Width(42)))
                    EditorGUIUtility.PingObject(entry.Target);
                using (new EditorGUI.DisabledGroupScope(entry.IsReady || !entry.Editable))
                {
                    if (GUILayout.Button("Arreglar", GUILayout.Width(64)))
                    {
                        var single = new List<Entry> { entry };
                        if (entries == this.lights)         this.Fix(single, null, null);
                        else if (entries == this.props)     this.Fix(null, single, null);
                        else                                this.Fix(null, null, single);
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (entry.IsReady)
                    EditorGUILayout.LabelField("Ya está listo.", EditorStyles.miniLabel);
                foreach (var issue in entry.Issues)
                    EditorGUILayout.LabelField("• " + issue, EditorStyles.wordWrappedMiniLabel);
                if (entry.Note != null)
                    EditorGUILayout.LabelField(entry.Note, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
            }
        }

        // ─────────────────────────── Análisis ───────────────────────────

        private void Rescan()
        {
            if (this.isFixing) return;

            this.lights.Clear();
            this.props.Clear();
            this.materials.Clear();

            var roots = Selection.gameObjects;
            this.selectedRootCount = roots.Length;

            var seenLights    = new HashSet<Light>();
            var seenRenderers = new HashSet<Renderer>();
            var seenMaterials = new HashSet<Material>();

            foreach (var root in roots)
            {
                if (root == null || EditorUtility.IsPersistent(root)) continue; // ignorar assets del Project

                Light[]    foundLights    = this.includeChildren ? root.GetComponentsInChildren<Light>(this.includeInactive)    : root.GetComponents<Light>();
                Renderer[] foundRenderers = this.includeChildren ? root.GetComponentsInChildren<Renderer>(this.includeInactive) : root.GetComponents<Renderer>();

                foreach (var l in foundLights)
                    if (seenLights.Add(l)) this.lights.Add(this.AnalyzeLight(l));

                foreach (var r in foundRenderers)
                {
                    if (!seenRenderers.Add(r) || !IsShadowRenderer(r)) continue;

                    // Las piezas de lámpara las maneja la sección de luces.
                    if (IsLightFixture(r)) continue;

                    this.props.Add(this.AnalyzeRenderer(r));

                    foreach (var m in r.sharedMaterials)
                        if (m != null && seenMaterials.Add(m)) this.materials.Add(AnalyzeMaterial(m));
                }
            }

            SortPendingFirst(this.lights);
            SortPendingFirst(this.props);
            SortPendingFirst(this.materials);
            this.Repaint();
        }

        private Entry AnalyzeLight(Light light)
        {
            var entry = new Entry(light, PathOf(light.transform));

            if (light.shadows == LightShadows.None)
                entry.Issues.Add("Shadow Type en None");
            if (light.shadowStrength < this.shadowStrength - 0.001f)
                entry.Issues.Add($"Shadow Strength {light.shadowStrength:0.##} (objetivo {this.shadowStrength:0.##})");

            if (light.type != LightType.Directional)
            {
                if (light.shadowNearPlane > this.maxNearPlane + 0.001f)
                    entry.Issues.Add($"Near Plane {light.shadowNearPlane:0.##}: lo que esté más cerca de la luz no proyecta sombra");

                var tier = FindTierProperty(light);
                if (tier == null)
                    entry.Note = "No tiene UniversalAdditionalLightData: no se puede revisar la resolución.";
                else if (tier.intValue != this.resolutionTier)
                    entry.Issues.Add($"Shadow Resolution {TierName(tier.intValue)} (objetivo {TierNames[this.resolutionTier]})");
            }

            if (this.fixtureCastOff)
            {
                var fixture = FindFixture(light);
                if (fixture != null && fixture.shadowCastingMode != ShadowCastingMode.Off)
                    entry.Issues.Add($"La pieza \"{fixture.name}\" proyecta sombra (mancha en la pared)");
            }

            entry.Note ??= SaveTargetNote(light, this.saveTarget);
            return entry;
        }

        private Entry AnalyzeRenderer(Renderer renderer)
        {
            var entry = new Entry(renderer, PathOf(renderer.transform));

            if (renderer.shadowCastingMode == ShadowCastingMode.Off)
                entry.Issues.Add("Cast Shadows en Off");
            if (!renderer.receiveShadows)
                entry.Issues.Add("Receive Shadows apagado");

            entry.Note = SaveTargetNote(renderer, this.saveTarget);
            return entry;
        }

        private static Entry AnalyzeMaterial(Material mat)
        {
            string path  = AssetDatabase.GetAssetPath(mat);
            var    entry = new Entry(mat, $"{mat.name}  ({mat.shader.name})");

            if (mat.HasProperty(PsxEnableShadows) && mat.GetFloat(PsxEnableShadows) < 0.5f)
                entry.Issues.Add("EnableShadows apagado (shader PSX): no recibe sombras");
            if (mat.HasProperty(PsxCastShadows) && mat.GetFloat(PsxCastShadows) < 0.5f)
                entry.Issues.Add("_CastShadows apagado: no proyecta sombra");
            if (mat.HasProperty(LitReceiveShadows) && mat.GetFloat(LitReceiveShadows) < 0.5f)
                entry.Issues.Add("Receive Shadows apagado en el material");
            if (HasShadowCasterPass(mat) && !mat.GetShaderPassEnabled(ShadowCasterPass))
                entry.Issues.Add("Pase ShadowCaster desactivado");

            if (string.IsNullOrEmpty(path) || !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                entry.Editable = false;
                entry.Note     = entry.IsReady ? null : $"Está embebido en \"{path}\": extraelo a un .mat para poder editarlo.";
            }
            else
            {
                entry.Note = path;
            }
            return entry;
        }

        // ─────────────────────────── Aplicar ───────────────────────────

        private void Fix(List<Entry>? lightEntries, List<Entry>? propEntries, List<Entry>? materialEntries)
        {
            int fixedCount = 0, alreadyReady = 0, skipped = 0;
            var log = new List<string>();

            // Aplicar a un prefab dispara OnSelectionChange/OnFocus, que vuelven a llenar las listas: trabajamos sobre copias.
            lightEntries    = lightEntries    != null ? new List<Entry>(lightEntries)    : null;
            propEntries     = propEntries     != null ? new List<Entry>(propEntries)     : null;
            materialEntries = materialEntries != null ? new List<Entry>(materialEntries) : null;
            this.isFixing   = true;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Shadow Setup");

            try
            {
                if (lightEntries != null)
                    foreach (var e in lightEntries)
                    {
                        if (e.IsReady) { alreadyReady++; continue; }
                        if (e.Target is Light l) { this.FixLight(l); fixedCount++; log.Add("Luz: " + e.Label); }
                    }

                if (propEntries != null)
                    foreach (var e in propEntries)
                    {
                        if (e.IsReady) { alreadyReady++; continue; }
                        if (e.Target is Renderer r) { this.FixRenderer(r); fixedCount++; log.Add("Prop: " + e.Label); }
                    }

                if (materialEntries != null)
                {
                    bool needsMaterials = false;
                    foreach (var e in materialEntries) if (!e.IsReady && e.Editable) needsMaterials = true;

                    if (needsMaterials && !EditorUtility.DisplayDialog(
                            "Materiales compartidos",
                            "Los materiales se modifican en el asset: el cambio afecta a todos los objetos del proyecto que los usan. ¿Seguir?",
                            "Sí, modificar", "Cancelar"))
                    {
                        materialEntries = null;
                    }

                    if (materialEntries != null)
                    {
                        foreach (var e in materialEntries)
                        {
                            if (e.IsReady) { alreadyReady++; continue; }
                            if (!e.Editable) { skipped++; continue; }
                            if (e.Target is Material m) { FixMaterial(m); fixedCount++; log.Add("Material: " + AssetDatabase.GetAssetPath(m)); }
                        }
                        AssetDatabase.SaveAssets();
                    }
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
                this.isFixing = false;
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) EditorSceneManager.MarkSceneDirty(stage.scene);

            this.lastResult = $"{fixedCount} arreglado(s), {alreadyReady} ya estaban listos" + (skipped > 0 ? $", {skipped} no editables." : ".");
            Debug.Log($"[ShadowSetup] {this.lastResult}\n" + string.Join("\n", log));
            this.Rescan();
        }

        private void FixLight(Light light)
        {
            var so = new SerializedObject(light);
            var changed = new List<string>();

            var type = so.FindProperty(PropShadowType);
            if (type.intValue == (int)LightShadows.None) { type.intValue = (int)this.shadowType; changed.Add(PropShadowType); }

            var strength = so.FindProperty(PropShadowStrength);
            if (strength.floatValue < this.shadowStrength - 0.001f) { strength.floatValue = this.shadowStrength; changed.Add(PropShadowStrength); }

            if (light.type != LightType.Directional)
            {
                var near = so.FindProperty(PropShadowNear);
                if (near.floatValue > this.maxNearPlane + 0.001f) { near.floatValue = this.maxNearPlane; changed.Add(PropShadowNear); }
            }

            so.ApplyModifiedProperties();
            this.Persist(so, changed, v => v.propertyPath switch
            {
                PropShadowType     => v.intValue != (int)LightShadows.None,
                PropShadowStrength => v.floatValue >= this.shadowStrength - 0.001f,
                PropShadowNear     => v.floatValue <= this.maxNearPlane + 0.001f,
                _                  => true,
            });

            if (light.type != LightType.Directional)
            {
                var tier = FindTierProperty(light);
                if (tier != null && tier.intValue != this.resolutionTier)
                {
                    tier.intValue = this.resolutionTier;
                    tier.serializedObject.ApplyModifiedProperties();
                    this.Persist(tier.serializedObject, new List<string> { PropShadowTier }, v => v.intValue == this.resolutionTier);
                }
            }

            if (this.fixtureCastOff)
            {
                var fixture = FindFixture(light);
                if (fixture != null && fixture.shadowCastingMode != ShadowCastingMode.Off)
                {
                    var fso  = new SerializedObject(fixture);
                    var cast = fso.FindProperty(PropCastShadows);
                    cast.intValue = (int)ShadowCastingMode.Off;
                    fso.ApplyModifiedProperties();
                    this.Persist(fso, new List<string> { PropCastShadows }, v => v.intValue == (int)ShadowCastingMode.Off);
                }
            }
        }

        private void FixRenderer(Renderer renderer)
        {
            var so      = new SerializedObject(renderer);
            var changed = new List<string>();

            var cast = so.FindProperty(PropCastShadows);
            if (cast.intValue == (int)ShadowCastingMode.Off) { cast.intValue = (int)ShadowCastingMode.On; changed.Add(PropCastShadows); }

            var receive = so.FindProperty(PropReceiveShadows);
            if (!receive.boolValue) { receive.boolValue = true; changed.Add(PropReceiveShadows); }

            so.ApplyModifiedProperties();
            this.Persist(so, changed, v => v.propertyPath == PropCastShadows
                ? v.intValue != (int)ShadowCastingMode.Off
                : v.boolValue);
        }

        private static void FixMaterial(Material mat)
        {
            Undo.RecordObject(mat, "Shadow Setup");

            if (mat.HasProperty(PsxEnableShadows) && mat.GetFloat(PsxEnableShadows) < 0.5f)
                mat.SetFloat(PsxEnableShadows, 1f);
            if (mat.HasProperty(PsxCastShadows) && mat.GetFloat(PsxCastShadows) < 0.5f)
                mat.SetFloat(PsxCastShadows, 1f);
            if (mat.HasProperty(LitReceiveShadows) && mat.GetFloat(LitReceiveShadows) < 0.5f)
            {
                mat.SetFloat(LitReceiveShadows, 1f);
                mat.DisableKeyword(LitReceiveOffKw);
            }
            if (HasShadowCasterPass(mat) && !mat.GetShaderPassEnabled(ShadowCasterPass))
                mat.SetShaderPassEnabled(ShadowCasterPass, true);

            EditorUtility.SetDirty(mat);
        }

        /// <summary>
        /// Lleva los cambios de una instancia al prefab elegido. Si un prefab intermedio
        /// (ej. la sala) pisa el valor, también se aplica ahí para que el resultado final quede bien.
        /// </summary>
        private void Persist(SerializedObject so, List<string> changedPaths, Func<SerializedProperty, bool> isOk)
        {
            if (changedPaths.Count == 0 || this.saveTarget == SaveTarget.Instancia) return;

            var target = so.targetObject;
            if (!PrefabUtility.IsPartOfPrefabInstance(target)) return; // objeto propio de la escena o del prefab abierto

            var paths = new List<string>();
            string? nearest   = EditablePrefabPath(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target));
            string? outermost = IsInPrefabStage(target) ? null : EditablePrefabPath(OutermostAssetPath(target));
            if (this.saveTarget == SaveTarget.PrefabBase && nearest != null) paths.Add(nearest);
            if (outermost != null && !paths.Contains(outermost)) paths.Add(outermost);
            // En Prefab Mode, lo que no se aplica al prefab base queda como override del prefab abierto (la sala).

            foreach (string propPath in changedPaths)
            {
                foreach (string assetPath in paths)
                {
                    so.Update();
                    var prop = so.FindProperty(propPath);
                    if (prop == null || !prop.prefabOverride) break; // ya quedó heredado del prefab

                    PrefabUtility.ApplyPropertyOverride(prop, assetPath, InteractionMode.UserAction);

                    so.Update();
                    prop = so.FindProperty(propPath);
                    if (prop == null || !isOk(prop))
                    {
                        // Un prefab intermedio pisó el valor al quitar el override: lo volvemos a poner y seguimos al siguiente nivel.
                        this.Reassign(so, propPath);
                        continue;
                    }
                    if (!prop.prefabOverride) break;
                }
            }
        }

        private void Reassign(SerializedObject so, string propPath)
        {
            var prop = so.FindProperty(propPath);
            switch (propPath)
            {
                case PropShadowType:     prop.intValue   = (int)this.shadowType;           break;
                case PropShadowStrength: prop.floatValue = this.shadowStrength;            break;
                case PropShadowNear:     prop.floatValue = this.maxNearPlane;              break;
                case PropShadowTier:     prop.intValue   = this.resolutionTier;            break;
                case PropReceiveShadows: prop.boolValue  = true;                           break;
                case PropCastShadows:
                    prop.intValue = so.targetObject is Renderer r && IsLightFixture(r)
                        ? (int)ShadowCastingMode.Off
                        : (int)ShadowCastingMode.On;
                    break;
            }
            so.ApplyModifiedProperties();
        }

        // ─────────────────────────── Helpers ───────────────────────────

        private static bool IsShadowRenderer(Renderer r)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is BillboardRenderer) return false;
            if (r is not MeshRenderer && r is not SkinnedMeshRenderer) return false;

            // Si todos los materiales son transparentes no tiene sentido forzar sombras.
            bool anyOpaque = false;
            foreach (var m in r.sharedMaterials)
                if (m != null && m.renderQueue < (int)RenderQueue.Transparent) anyOpaque = true;
            return anyOpaque;
        }

        /// <summary>Pieza de lámpara: renderer en el mismo GameObject que la luz o en su padre directo.</summary>
        private static bool IsLightFixture(Renderer r)
        {
            if (r.GetComponent<Light>() != null) return true;
            foreach (Transform child in r.transform)
                if (child.GetComponent<Light>() != null) return true;
            return false;
        }

        private static Renderer? FindFixture(Light light)
        {
            var own = light.GetComponent<MeshRenderer>();
            if (own != null) return own;
            return light.transform.parent != null ? light.transform.parent.GetComponent<MeshRenderer>() : null;
        }

        private static SerializedProperty? FindTierProperty(Light light)
        {
            var data = light.GetComponent(UrpLightDataType);
            if (data == null) return null;
            return new SerializedObject(data).FindProperty(PropShadowTier);
        }

        private static bool HasShadowCasterPass(Material mat)
        {
            var shader = mat.shader;
            if (shader == null) return false;
            var tag = new ShaderTagId("LightMode");
            for (int i = 0; i < shader.passCount; i++)
                if (string.Equals(shader.FindPassTagValue(i, tag).name, ShadowCasterPass, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string? OutermostAssetPath(Object target)
        {
            GameObject? go = target is Component c ? c.gameObject : target as GameObject;
            if (go == null) return null;
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            return root != null ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) : null;
        }

        /// <summary>Devuelve la ruta solo si es un prefab editable (no un modelo FBX).</summary>
        private static string? EditablePrefabPath(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null || PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Model) return null;
            return path;
        }

        private static string? SaveTargetNote(Component c, SaveTarget saveTarget)
        {
            var stage = PrefabStageUtility.GetPrefabStage(c.gameObject);
            if (saveTarget == SaveTarget.Instancia || !PrefabUtility.IsPartOfPrefabInstance(c))
                return stage != null ? "Se guarda en " + System.IO.Path.GetFileName(stage.assetPath) : null;

            string? nearest   = EditablePrefabPath(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(c));
            string? outermost = stage != null ? stage.assetPath : EditablePrefabPath(OutermostAssetPath(c));
            string? dest      = saveTarget == SaveTarget.PrefabBase ? nearest ?? outermost : outermost;
            return dest != null ? "Se guarda en " + System.IO.Path.GetFileName(dest) : "No hay prefab editable: queda como override de la instancia.";
        }

        private static bool IsInPrefabStage(Object target)
        {
            GameObject? go = target is Component c ? c.gameObject : target as GameObject;
            return go != null && PrefabStageUtility.GetPrefabStage(go) != null;
        }

        private static string PathOf(Transform t)
        {
            var root = t;
            while (root.parent != null) root = root.parent;
            return t == root ? t.name : root.name + "/" + AnimationUtility.CalculateTransformPath(t, root);
        }

        private static string TierName(int tier) => tier >= 0 && tier < TierNames.Length ? TierNames[tier] : $"Custom ({tier})";

        private static int CountPending(List<Entry> entries)
        {
            int count = 0;
            foreach (var e in entries)
                if (!e.IsReady && e.Editable) count++;
            return count;
        }

        private static void SortPendingFirst(List<Entry> entries) =>
            entries.Sort((a, b) => a.IsReady == b.IsReady ? string.CompareOrdinal(a.Label, b.Label) : (a.IsReady ? 1 : -1));
    }
}
