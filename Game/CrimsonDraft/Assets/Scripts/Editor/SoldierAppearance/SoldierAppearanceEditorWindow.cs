using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrimsonDraft.Operators.Appearance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrimsonDraft.Editor
{
    /// <summary>
    /// Picks the equipment variant shown per slot (Helmet, Glasses, Backpack, Vest) on a soldier with a
    /// <see cref="SoldierAppearance"/>, previewing live on the selected instance or Prefab Stage, with Undo.
    /// Selections can be saved to / loaded from a <see cref="SoldierAppearanceProfile"/>.
    /// Same layout idea as the Zombie Texture Editor (one row per slot, &lt; preview &gt;).
    /// </summary>
    public class SoldierAppearanceEditorWindow : EditorWindow
    {
        private const string MenuPath    = "CrimsonDraft/Soldier Appearance Editor";
        private const string ProfilesDir = "Assets/Data/Characters/Appearance";
        private const float  PreviewSize = 96f;

        private class SlotLook
        {
            public Material MaterialOverride;
            public bool OverrideTint;
            public Color Tint = Color.white;
        }

        private SoldierAppearance m_Target;
        private SoldierAppearanceProfile m_Profile;
        private SlotLook[] m_Looks;
        private readonly List<string> m_Names = new List<string>();
        private Vector2 m_Scroll;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            GetWindow<SoldierAppearanceEditorWindow>("Soldier Appearance");
        }

        private void OnEnable()
        {
            if (m_Looks == null || m_Looks.Length != SoldierAppearanceVariants.SlotCount)
            {
                m_Looks = new SlotLook[SoldierAppearanceVariants.SlotCount];
                for (int i = 0; i < m_Looks.Length; i++) m_Looks[i] = new SlotLook();
            }
            this.OnSelectionChange();
            Undo.undoRedoPerformed += this.Repaint;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= this.Repaint;
        }

        private void OnSelectionChange()
        {
            var go = Selection.activeGameObject;
            if (go)
            {
                var found = go.GetComponentInChildren<SoldierAppearance>(true) ?? go.GetComponentInParent<SoldierAppearance>(true);
                if (found) m_Target = found;
            }
            this.Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            m_Target = (SoldierAppearance)EditorGUILayout.ObjectField("Soldier", m_Target, typeof(SoldierAppearance), true);

            if (!this.DrawTargetState())
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            m_Profile = (SoldierAppearanceProfile)EditorGUILayout.ObjectField("Profile", m_Profile, typeof(SoldierAppearanceProfile), false);
            GUI.enabled = m_Profile;
            if (GUILayout.Button("Load", GUILayout.Width(50))) this.LoadProfile(m_Profile);
            if (GUILayout.Button("Save", GUILayout.Width(50))) this.SaveProfile(m_Profile);
            GUI.enabled = true;
            if (GUILayout.Button("Save As...", GUILayout.Width(80))) this.SaveProfileAs();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Random")) this.Randomize();
            if (GUILayout.Button("Reset to default")) this.ResetToDefault();
            EditorGUILayout.EndHorizontal();

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            this.DrawBody();
            this.DrawCamo();
            for (int i = 0; i < SoldierAppearanceVariants.SlotCount; i++)
                this.DrawSlot((SoldierAppearanceSlot)i);
            EditorGUILayout.EndScrollView();

            this.DrawFooter();
            EditorGUILayout.EndVertical();

            if (AssetPreview.IsLoadingAssetPreviews())
                this.Repaint();
        }

        // Returns true when the target can be edited.
        private bool DrawTargetState()
        {
            var selected = Selection.activeGameObject;
            if (!m_Target)
            {
                if (selected && EditorUtility.IsPersistent(selected))
                {
                    EditorGUILayout.HelpBox("Prefab asset selected: open it to preview and edit its appearance.", MessageType.Info);
                    if (GUILayout.Button("Open Prefab")) AssetDatabase.OpenAsset(selected);
                }
                else if (selected && !EditorUtility.IsPersistent(selected) && SoldierVariantInstaller.FindRig(selected.transform))
                {
                    EditorGUILayout.HelpBox("This soldier has no SoldierAppearance yet.", MessageType.Info);
                    if (GUILayout.Button("Install Variants")) this.InstallOn(selected);
                }
                else
                {
                    EditorGUILayout.HelpBox("Select a soldier (scene instance or Prefab Stage) with a SoldierAppearance.\n" +
                        "Ready to use: Ethan_Combat_Appearance (combat) and Player (navigation).", MessageType.Info);
                    if (GUILayout.Button("Create Appearance Prefab Variants"))
                        SoldierVariantInstaller.CreateAppearancePrefabVariants();
                }
                return false;
            }

            if (EditorUtility.IsPersistent(m_Target))
            {
                EditorGUILayout.HelpBox("This is the prefab asset: open it to preview the changes.", MessageType.Info);
                if (GUILayout.Button("Open Prefab")) AssetDatabase.OpenAsset(m_Target.gameObject);
                return false;
            }
            return true;
        }

        private void DrawSlot(SoldierAppearanceSlot slot)
        {
            m_Target.GetVariantNames(slot, m_Names);
            m_Names.Insert(0, SoldierAppearanceVariants.None);
            string current = m_Target.GetVariant(slot);
            int index = Mathf.Max(0, m_Names.FindIndex(n => string.Equals(n, current, System.StringComparison.OrdinalIgnoreCase)));

            GUILayout.Space(6);
            GUILayout.Label($"{slot}  ({index}/{m_Names.Count - 1})", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("<", GUILayout.Width(30), GUILayout.Height(PreviewSize)))
                this.ChangeVariant(slot, m_Names[Wrap(index - 1, m_Names.Count)]);

            var preview = this.GetPreview(slot, current);
            if (preview) GUILayout.Box(preview, GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));
            else GUILayout.Box(current == SoldierAppearanceVariants.None ? "None" : current, GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));

            if (GUILayout.Button(">", GUILayout.Width(30), GUILayout.Height(PreviewSize)))
                this.ChangeVariant(slot, m_Names[Wrap(index + 1, m_Names.Count)]);

            EditorGUILayout.BeginVertical();
            int picked = EditorGUILayout.Popup(index, m_Names.ToArray());
            if (picked != index) this.ChangeVariant(slot, m_Names[picked]);
            if (GUILayout.Button("None")) this.ChangeVariant(slot, SoldierAppearanceVariants.None);

            var look = m_Looks[(int)slot];
            EditorGUI.BeginChangeCheck();
            look.MaterialOverride = (Material)EditorGUILayout.ObjectField("Material", look.MaterialOverride, typeof(Material), false);
            EditorGUILayout.BeginHorizontal();
            look.OverrideTint = EditorGUILayout.ToggleLeft("Tint", look.OverrideTint, GUILayout.Width(50));
            GUI.enabled = look.OverrideTint;
            look.Tint = EditorGUILayout.ColorField(look.Tint);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) this.ApplyLook(slot);

            if (SoldierAppearanceVariants.SupportsFit(slot))
            {
                var fit = m_Target.GetSlotFit(slot);
                var edited = new Vector3(
                    EditorGUILayout.Slider(new GUIContent("Depth", "+ closer to the face, - further away"), fit.x, -1f, 1f),
                    EditorGUILayout.Slider(new GUIContent("Height", "+ up, - down"), fit.y, -1f, 1f),
                    EditorGUILayout.Slider(new GUIContent("Size", "+ bigger, - smaller"), fit.z, -1f, 1f));
                if (edited != fit) this.ChangeSlotFit(slot, edited);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCamo()
        {
            m_Target.GetCamoNames(m_Names);
            string current = m_Target.Camo;
            int index = Mathf.Max(0, m_Names.FindIndex(n => string.Equals(n, current, System.StringComparison.OrdinalIgnoreCase)));

            GUILayout.Space(6);
            GUILayout.Label($"Camo  ({index}/{m_Names.Count - 1})", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(30), GUILayout.Height(PreviewSize)))
                this.ChangeCamo(m_Names[Wrap(index - 1, m_Names.Count)]);

            var material = m_Target.FindCamo(current) ?? AssetDatabase.LoadAssetAtPath<Material>(SoldierVariantsImportSettings.SoldierMaterial2);
            var texture = material ? material.mainTexture : null; // PSX shader: _MainTex
            if (texture) GUILayout.Box(texture, GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));
            else GUILayout.Box(current, GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));

            if (GUILayout.Button(">", GUILayout.Width(30), GUILayout.Height(PreviewSize)))
                this.ChangeCamo(m_Names[Wrap(index + 1, m_Names.Count)]);
            EditorGUILayout.BeginVertical();
            int picked = EditorGUILayout.Popup(index, m_Names.ToArray());
            if (picked != index) this.ChangeCamo(m_Names[picked]);
            EditorGUILayout.HelpBox("Swaps the soldier atlas (same UVs) on the body and every piece. Gear (masks, NVG) keeps Mat_Gear.", MessageType.None);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBody()
        {
            GUILayout.Space(6);
            GUILayout.Label("Body", EditorStyles.boldLabel);
            // Soldiers installed before body types existed keep the old body mesh (no blend shapes).
            var outdated = m_Target.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(r => r.sharedMesh && r.sharedMesh.GetBlendShapeIndex(nameof(SoldierBodyShape.Heavy)) < 0);
            if (outdated)
            {
                EditorGUILayout.HelpBox($"'{outdated.name}' has no body-type blend shapes, so it won't change shape. " +
                    "Press Install / Update Variants to update this soldier.", MessageType.Warning);
                if (GUILayout.Button("Install / Update Variants")) this.InstallOn(m_Target.gameObject);
            }
            for (int i = 0; i < SoldierAppearanceVariants.BodyShapeCount; i++)
            {
                var shape = (SoldierBodyShape)i;
                float current = m_Target.GetBodyShape(shape);
                float value = EditorGUILayout.Slider(shape.ToString(), current, 0f, 100f);
                if (!Mathf.Approximately(value, current)) this.ChangeBodyShape(shape, value);
            }
            float height = EditorGUILayout.Slider("Height", m_Target.Height, SoldierAppearanceVariants.MinHeight, SoldierAppearanceVariants.MaxHeight);
            if (!Mathf.Approximately(height, m_Target.Height)) this.ChangeHeight(height);
            EditorGUILayout.HelpBox("Blend shapes on the body and on every piece (gear follows the body). Height is a uniform scale.", MessageType.None);
        }

        private void ChangeBodyShape(SoldierBodyShape shape, float weight)
        {
            var renderers = m_Target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var objects = new List<Object> { m_Target };
            objects.AddRange(renderers);
            Undo.RecordObjects(objects.ToArray(), "Change Soldier Body");
            m_Target.SetBodyShape(shape, weight);
            this.MarkDirty();
            foreach (var r in renderers)
            {
                EditorUtility.SetDirty(r);
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
        }

        private void ChangeHeight(float height)
        {
            Undo.RecordObjects(new Object[] { m_Target, m_Target.transform }, "Change Soldier Height");
            m_Target.SetHeight(height);
            PrefabUtility.RecordPrefabInstancePropertyModifications(m_Target.transform);
            this.MarkDirty();
        }

        private void DrawFooter()
        {
            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Install / Update Variants")) this.InstallOn(m_Target.gameObject);

            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(m_Target);
            bool inPrefabStage = PrefabStageUtility.GetPrefabStage(m_Target.gameObject) != null;
            GUI.enabled = !inPrefabStage && !string.IsNullOrEmpty(assetPath);
            if (GUILayout.Button(new GUIContent("Apply to Prefab", inPrefabStage ? "In Prefab Stage changes are saved with the prefab." : assetPath)))
                this.ApplyToPrefab(assetPath);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            if (inPrefabStage)
                EditorGUILayout.HelpBox("Editing in Prefab Stage: save the prefab to keep the selection.", MessageType.None);
        }

        private Texture GetPreview(SoldierAppearanceSlot slot, string variant)
        {
            foreach (var e in m_Target.Variants)
                if (e.slot == slot && e.renderer && e.variant == variant && e.renderer.sharedMesh)
                    return AssetPreview.GetAssetPreview(e.renderer.sharedMesh);
            return null;
        }

        // ------------------------------------------------------------------------------------------
        // Changes (all go through Undo + dirty marking so they are saved in the scene/prefab)
        // ------------------------------------------------------------------------------------------
        private void ChangeVariant(SoldierAppearanceSlot slot, string variant)
        {
            this.RecordUndo(slot, "Change Soldier Variant");
            m_Target.SetVariant(slot, variant);
            this.MarkDirty();
        }

        private void ChangeCamo(string camo)
        {
            var renderers = m_Target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var objects = new List<Object> { m_Target };
            objects.AddRange(renderers);
            Undo.RecordObjects(objects.ToArray(), "Change Soldier Camo");
            m_Target.SetCamo(camo);
            this.MarkDirty();
            foreach (var r in renderers)
            {
                EditorUtility.SetDirty(r);
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
        }

        private void ChangeSlotFit(SoldierAppearanceSlot slot, Vector3 fit)
        {
            this.RecordUndo(slot, "Change Soldier Fit");
            m_Target.SetSlotFit(slot, fit);
            this.MarkDirty();
        }

        private void ApplyLook(SoldierAppearanceSlot slot)
        {
            var look = m_Looks[(int)slot];
            this.RecordUndo(slot, "Change Soldier Look");
            m_Target.SetSlotLook(slot, look.MaterialOverride, look.OverrideTint, look.Tint);
            this.MarkDirty();
        }

        private void Randomize()
        {
            for (int i = 0; i < SoldierAppearanceVariants.SlotCount; i++)
            {
                var slot = (SoldierAppearanceSlot)i;
                m_Target.GetVariantNames(slot, m_Names);
                m_Names.Add(SoldierAppearanceVariants.None);
                this.ChangeVariant(slot, m_Names[Random.Range(0, m_Names.Count)]);
            }
            m_Target.GetCamoNames(m_Names);
            this.ChangeCamo(m_Names[Random.Range(0, m_Names.Count)]);
            // Body: usually one dominant type, sometimes a mix, plus a small height variation.
            for (int i = 0; i < SoldierAppearanceVariants.BodyShapeCount; i++)
                this.ChangeBodyShape((SoldierBodyShape)i, Random.value < 0.35f ? Random.Range(20f, 100f) : 0f);
            this.ChangeHeight(Random.Range(0.94f, 1.06f));
        }

        private void ResetToDefault()
        {
            for (int i = 0; i < SoldierAppearanceVariants.SlotCount; i++)
            {
                var slot = (SoldierAppearanceSlot)i;
                m_Looks[i] = new SlotLook();
                this.ChangeVariant(slot, SoldierAppearanceVariants.DefaultVariant(slot));
                this.ApplyLook(slot);
            }
            this.ChangeCamo(SoldierAppearanceVariants.Default);
            for (int i = 0; i < SoldierAppearanceVariants.BodyShapeCount; i++)
                this.ChangeBodyShape((SoldierBodyShape)i, 0f);
            this.ChangeHeight(1f);
            for (int i = 0; i < SoldierAppearanceVariants.SlotCount; i++)
                if (SoldierAppearanceVariants.SupportsFit((SoldierAppearanceSlot)i))
                    this.ChangeSlotFit((SoldierAppearanceSlot)i, Vector3.zero);
        }

        private void LoadProfile(SoldierAppearanceProfile profile)
        {
            for (int i = 0; i < SoldierAppearanceVariants.SlotCount; i++)
            {
                var data = profile.Get((SoldierAppearanceSlot)i);
                m_Looks[i] = new SlotLook { MaterialOverride = data.materialOverride, OverrideTint = data.overrideTint, Tint = data.tint };
                if (SoldierAppearanceVariants.SupportsFit(data.slot))
                    this.ChangeSlotFit(data.slot, data.fit);
                this.ChangeVariant(data.slot, data.variant);
                this.ApplyLook(data.slot);
            }
            this.ChangeCamo(m_Target.HasCamo(profile.Camo) ? profile.Camo : SoldierAppearanceVariants.Default);
            for (int i = 0; i < SoldierAppearanceVariants.BodyShapeCount; i++)
                this.ChangeBodyShape((SoldierBodyShape)i, profile.GetBodyShape((SoldierBodyShape)i));
            this.ChangeHeight(profile.Height);
        }

        private void SaveProfile(SoldierAppearanceProfile profile)
        {
            Undo.RecordObject(profile, "Save Soldier Appearance Profile");
            for (int i = 0; i < SoldierAppearanceVariants.SlotCount; i++)
            {
                var data = profile.Get((SoldierAppearanceSlot)i);
                data.variant = m_Target.GetVariant(data.slot);
                data.materialOverride = m_Looks[i].MaterialOverride;
                data.overrideTint = m_Looks[i].OverrideTint;
                data.tint = m_Looks[i].Tint;
                data.fit = m_Target.GetSlotFit(data.slot);
            }
            profile.Camo = m_Target.Camo;
            for (int i = 0; i < SoldierAppearanceVariants.BodyShapeCount; i++)
                profile.SetBodyShape((SoldierBodyShape)i, m_Target.GetBodyShape((SoldierBodyShape)i));
            profile.Height = m_Target.Height;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            Debug.Log($"[SoldierAppearance] Saved profile {AssetDatabase.GetAssetPath(profile)}");
        }

        private void SaveProfileAs()
        {
            Directory.CreateDirectory(ProfilesDir);
            var path = EditorUtility.SaveFilePanelInProject("Save Soldier Appearance Profile", "SoldierAppearance_New", "asset", "", ProfilesDir);
            if (string.IsNullOrEmpty(path)) return;
            var profile = CreateInstance<SoldierAppearanceProfile>();
            AssetDatabase.CreateAsset(profile, path);
            this.SaveProfile(profile);
            m_Profile = profile;
        }

        private void InstallOn(GameObject go)
        {
            try
            {
                m_Target = SoldierVariantInstaller.Install(go, SoldierVariantInstaller.LoadVariantsModel());
                this.MarkDirty();
            }
            catch (System.InvalidOperationException e)
            {
                EditorUtility.DisplayDialog("Soldier Appearance", e.Message, "OK");
            }
        }

        private void ApplyToPrefab(string assetPath)
        {
            // Applies only what this tool changes: the variant objects and the SoldierAppearance component.
            foreach (var e in m_Target.Variants)
            {
                if (!e.renderer) continue;
                var go = e.renderer.gameObject;
                if (PrefabUtility.IsAddedGameObjectOverride(go))
                    PrefabUtility.ApplyAddedGameObject(go, assetPath, InteractionMode.UserAction);
                else
                {
                    PrefabUtility.ApplyObjectOverride(go, assetPath, InteractionMode.UserAction);
                    PrefabUtility.ApplyObjectOverride(e.renderer, assetPath, InteractionMode.UserAction);
                }
            }
            // The camo also changes the body's materials.
            foreach (var r in m_Target.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (!PrefabUtility.IsAddedGameObjectOverride(r.gameObject) && !PrefabUtility.IsAddedComponentOverride(r))
                    PrefabUtility.ApplyObjectOverride(r, assetPath, InteractionMode.UserAction);
            // Height: only the scale (the root's scene position/rotation must not go into the prefab).
            var scale = new SerializedObject(m_Target.transform).FindProperty("m_LocalScale");
            if (scale.prefabOverride)
                PrefabUtility.ApplyPropertyOverride(scale, assetPath, InteractionMode.UserAction);
            if (PrefabUtility.IsAddedComponentOverride(m_Target))
                PrefabUtility.ApplyAddedComponent(m_Target, assetPath, InteractionMode.UserAction);
            else
                PrefabUtility.ApplyObjectOverride(m_Target, assetPath, InteractionMode.UserAction);
            Debug.Log($"[SoldierAppearance] Applied appearance to {assetPath}");
        }

        private void RecordUndo(SoldierAppearanceSlot slot, string label)
        {
            var objects = new List<Object> { m_Target };
            foreach (var e in m_Target.Variants)
                if (e.slot == slot && e.renderer)
                {
                    objects.Add(e.renderer.gameObject);
                    objects.Add(e.renderer);
                }
            Undo.RecordObjects(objects.ToArray(), label);
        }

        private void MarkDirty()
        {
            EditorUtility.SetDirty(m_Target);
            foreach (var e in m_Target.Variants)
                if (e.renderer)
                {
                    EditorUtility.SetDirty(e.renderer.gameObject);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(e.renderer.gameObject);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(e.renderer);
                }
            PrefabUtility.RecordPrefabInstancePropertyModifications(m_Target);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(m_Target.gameObject.scene);
        }

        private static int Wrap(int value, int count)
        {
            return ((value % count) + count) % count;
        }
    }
}
