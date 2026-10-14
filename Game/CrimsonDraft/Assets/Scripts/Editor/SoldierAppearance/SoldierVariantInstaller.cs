using System;
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
    /// Re-binds the variant meshes of SoldierCharacterRigged_Variants.fbx onto a soldier's existing
    /// skeleton (bones matched by path under "Rig", then by name) and wires a <see cref="SoldierAppearance"/>.
    /// Also creates the combat / navigation Prefab Variants used by the appearance tool.
    /// </summary>
    public static class SoldierVariantInstaller
    {
        public const string VariantsFbxPath = SoldierVariantsImportSettings.VariantsFolder + "SoldierCharacterRigged_Variants.fbx";
        private const string RigName      = "Rig";
        private const string RootBoneName = "Root";
        private const string BodyName     = "Marine";

        private static readonly (string basePath, string variantPath)[] AppearancePrefabs =
        {
            ("Assets/Prefabs/Characters/Ethan_Combat_FBX.prefab", "Assets/Prefabs/Characters/Ethan_Combat_Appearance.prefab"),
            ("Assets/Prefabs/Characters/Player.prefab",           "Assets/Prefabs/Characters/Player_Appearance.prefab"),
        };

        [MenuItem("CrimsonDraft/Soldier Appearance/Create Appearance Prefab Variants")]
        public static void CreateAppearancePrefabVariants()
        {
            var variantsModel = LoadVariantsModel();
            foreach (var (basePath, variantPath) in AppearancePrefabs)
            {
                if (File.Exists(variantPath))
                {
                    Debug.Log($"[SoldierAppearance] {variantPath} already exists, skipped (use Install/Update Variants on it instead).");
                    continue;
                }

                var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
                if (!basePrefab)
                    throw new InvalidOperationException($"Base prefab not found: {basePath}");

                // Built in a preview scene so the open scene is never touched or dirtied.
                var scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, scene);
                    Install(instance, variantsModel, recordUndo: false);
                    PrefabUtility.SaveAsPrefabAsset(instance, variantPath, out bool ok);
                    if (!ok) throw new InvalidOperationException($"Could not save {variantPath}");
                    Debug.Log($"[SoldierAppearance] Created Prefab Variant {variantPath} (base: {basePath}).");
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        /// <summary>Re-runs Install on the existing appearance prefabs (after exporting new variants or camos).</summary>
        [MenuItem("CrimsonDraft/Soldier Appearance/Update Appearance Prefab Variants")]
        public static void UpdateAppearancePrefabVariants()
        {
            var variantsModel = LoadVariantsModel();
            foreach (var (_, variantPath) in AppearancePrefabs)
            {
                if (!File.Exists(variantPath)) continue;
                var root = PrefabUtility.LoadPrefabContents(variantPath);
                try
                {
                    Install(root, variantsModel, recordUndo: false);
                    PrefabUtility.SaveAsPrefabAsset(root, variantPath);
                    Debug.Log($"[SoldierAppearance] Updated {variantPath}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        public static GameObject LoadVariantsModel()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(VariantsFbxPath);
            if (!model)
                throw new InvalidOperationException($"Variants FBX not found at {VariantsFbxPath}. Export it with Tools/Blender/soldier_variants.py.");
            return model;
        }

        /// <summary>
        /// Adds (or updates) one child SkinnedMeshRenderer per variant on <paramref name="character"/> and
        /// returns its SoldierAppearance. When the character already has the original piece (Helmet/Goggle/
        /// Pack/Vest) it stays as the slot's Default and only takes the "&lt;Slot&gt;_Default" mesh; the body
        /// ("Marine") takes the variants body mesh too, so everything has the body-type blend shapes.
        /// </summary>
        public static SoldierAppearance Install(GameObject character, GameObject variantsModel, bool recordUndo = true)
        {
            var targetRig = FindRig(character.transform)
                ?? throw new InvalidOperationException($"'{character.name}' has no '{RigName}/{RootBoneName}' skeleton.");
            var sourceRig = FindRig(variantsModel.transform)
                ?? throw new InvalidOperationException($"'{variantsModel.name}' has no '{RigName}/{RootBoneName}' skeleton.");

            var modelRoot = targetRig.parent; // object holding the Animator (root in combat, child in navigation)
            var bonesByPath = new Dictionary<string, Transform>();
            var bonesByName = new Dictionary<string, Transform>();
            foreach (var t in targetRig.GetComponentsInChildren<Transform>(true))
            {
                bonesByPath[RelativePath(targetRig, t)] = t;
                bonesByName[t.name] = bonesByName.ContainsKey(t.name) ? null : t; // null = ambiguous name
            }

            var legacy = modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => SoldierAppearance.TryParse(r.name, out _, out var v) && v == SoldierAppearanceVariants.Default && !r.name.Contains("_"))
                .ToList();
            var template = legacy.FirstOrDefault();
            var variantsParent = template ? template.transform.parent : modelRoot;

            var body = modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == BodyName);

            foreach (var source in variantsModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // The body and the original pieces keep their objects/materials but take the meshes of the
                // variants FBX: same geometry and skinning, plus the body-type blend shapes.
                SkinnedMeshRenderer swapTarget = null;
                if (source.name == BodyName)
                    swapTarget = body;
                else if (!SoldierAppearance.TryParse(source.name, out var slot, out var variant))
                    continue;
                else if (variant == SoldierAppearanceVariants.Default)
                    swapTarget = legacy.FirstOrDefault(l => SoldierAppearance.TryParse(l.name, out var s, out _) && s == slot);
                if (source.name == BodyName && !swapTarget)
                    continue;

                var bones = new Transform[source.bones.Length];
                var missing = new List<string>();
                for (int i = 0; i < bones.Length; i++)
                    bones[i] = MapBone(source.bones[i], sourceRig, bonesByPath, bonesByName, missing);
                var rootBone = MapBone(source.rootBone, sourceRig, bonesByPath, bonesByName, missing);
                if (missing.Count > 0)
                    throw new InvalidOperationException(
                        $"Cannot bind '{source.name}' to '{character.name}': missing bones {string.Join(", ", missing.Distinct())}.");

                // Body shapes make the mesh grow: bounds with margin so it is not culled early.
                var bounds = source.localBounds;
                bounds.extents *= 1.25f;

                if (swapTarget)
                {
                    if (recordUndo) Undo.RecordObject(swapTarget, "Update Soldier Mesh");
                    swapTarget.sharedMesh  = source.sharedMesh;
                    swapTarget.bones       = bones;
                    swapTarget.rootBone    = rootBone;
                    swapTarget.localBounds = bounds;
                    continue;
                }

                var existing = variantsParent.Find(source.name);
                SkinnedMeshRenderer target;
                if (existing)
                {
                    target = existing.GetComponent<SkinnedMeshRenderer>();
                    if (recordUndo) Undo.RecordObject(target, "Update Soldier Variant");
                }
                else
                {
                    var go = new GameObject(source.name) { layer = modelRoot.gameObject.layer };
                    go.transform.SetParent(variantsParent, false);
                    go.SetActive(false);
                    if (recordUndo) Undo.RegisterCreatedObjectUndo(go, "Install Soldier Variant");
                    target = go.AddComponent<SkinnedMeshRenderer>();
                }

                target.sharedMesh      = source.sharedMesh;
                target.sharedMaterials = source.sharedMaterials;
                target.bones           = bones;
                target.rootBone        = rootBone;
                target.localBounds     = bounds;
                if (template)
                {
                    target.quality              = template.quality;
                    target.updateWhenOffscreen  = template.updateWhenOffscreen;
                    target.skinnedMotionVectors = template.skinnedMotionVectors;
                    target.shadowCastingMode    = template.shadowCastingMode;
                    target.receiveShadows       = template.receiveShadows;
                }
            }

            var appearance = modelRoot.GetComponent<SoldierAppearance>();
            if (!appearance)
                appearance = recordUndo ? Undo.AddComponent<SoldierAppearance>(modelRoot.gameObject) : modelRoot.gameObject.AddComponent<SoldierAppearance>();
            else if (recordUndo)
                Undo.RecordObject(appearance, "Install Soldier Variants");
            appearance.ConfigureCamos(LoadCamoBaseMaterials(), LoadCamoMaterials());
            appearance.RebuildVariantList();
            // Height multiplies the source prefab's scale (e.g. 0.95), never the current, possibly already scaled, one.
            var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(modelRoot);
            if (sourcePrefab)
            {
                if (recordUndo) Undo.RecordObject(modelRoot, "Install Soldier Variants");
                appearance.ConfigureBaseScale(sourcePrefab.localScale);
            }
            EditorUtility.SetDirty(appearance);
            return appearance;
        }

        /// <summary>Soldier atlas materials a camo replaces (Soldier1 / Soldier2).</summary>
        public static IEnumerable<Material> LoadCamoBaseMaterials()
        {
            foreach (var path in new[] { SoldierVariantsImportSettings.SoldierMaterial1, SoldierVariantsImportSettings.SoldierMaterial2 })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m) yield return m;
            }
        }

        /// <summary>Every Mat_Camo_*.mat in the Variants/Camo folder, sorted by name.</summary>
        public static IEnumerable<Material> LoadCamoMaterials()
        {
            string folder = SoldierVariantsImportSettings.CamoFolder.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder)) return Enumerable.Empty<Material>();
            return AssetDatabase.FindAssets("t:Material", new[] { folder })
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(m => m && m.name.StartsWith("Mat_Camo_"))
                .OrderBy(m => m.name)
                .ToList();
        }

        /// <summary>The "Rig" transform that contains "Root" (the armature exported by Blender).</summary>
        public static Transform FindRig(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == RigName && t.Find(RootBoneName))
                    return t;
            return null;
        }

        private static Transform MapBone(Transform sourceBone, Transform sourceRig,
            Dictionary<string, Transform> byPath, Dictionary<string, Transform> byName, List<string> missing)
        {
            if (!sourceBone) return null;
            if (byPath.TryGetValue(RelativePath(sourceRig, sourceBone), out var bone)) return bone;
            if (byName.TryGetValue(sourceBone.name, out bone) && bone) return bone;
            missing.Add(sourceBone.name);
            return null;
        }

        private static string RelativePath(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (var x = t; x && x != root; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
