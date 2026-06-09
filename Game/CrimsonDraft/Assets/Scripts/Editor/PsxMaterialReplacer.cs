#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CrimsonDraft.Editor
{
    public sealed class PsxMaterialReplacer : EditorWindow
    {
        private const string MenuPath            = "CrimsonDraft/PSX Material Replacer";
        private const string DefaultSourceShader = "PSX/PSXLit";
        private const string DefaultTargetShader = "Universal Render Pipeline/Lit";

        [SerializeField] private string sourceShaderName = DefaultSourceShader;
        [SerializeField] private string targetShaderName = DefaultTargetShader;
        [SerializeField] private bool   preserveTexture  = true;
        [SerializeField] private bool   preserveColor    = true;

        private readonly List<MaterialEntry> found = new();
        private Vector2 scrollPos;
        private bool    hasScanned;

        private sealed class MaterialEntry
        {
            public readonly Material Material;
            public readonly string   Path;
            public          bool     Selected;

            public MaterialEntry(Material material, string path)
            {
                this.Material = material;
                this.Path     = path;
                this.Selected = true;
            }
        }

        [MenuItem(MenuPath)]
        public static void ShowWindow() => GetWindow<PsxMaterialReplacer>("PSX Material Replacer");

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("PSX → URP Material Replacer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Busca todos los materiales que usen un shader PSX y lo reemplaza con URP Lit " +
                "en el mismo asset, preservando textura y color.",
                MessageType.Info);
            EditorGUILayout.Space(6);

            EditorGUI.BeginChangeCheck();
            this.sourceShaderName = EditorGUILayout.TextField("Shader origen",  this.sourceShaderName);
            this.targetShaderName = EditorGUILayout.TextField("Shader destino", this.targetShaderName);
            if (EditorGUI.EndChangeCheck())
                this.hasScanned = false;

            EditorGUILayout.Space(4);
            this.preserveTexture = EditorGUILayout.Toggle("Preservar textura  (_MainTex → _BaseMap)",  this.preserveTexture);
            this.preserveColor   = EditorGUILayout.Toggle("Preservar color    (_Color → _BaseColor)", this.preserveColor);
            EditorGUILayout.Space(8);

            if (GUILayout.Button("Escanear proyecto", GUILayout.Height(28)))
                this.ScanProject();

            if (!this.hasScanned)
                return;

            EditorGUILayout.Space(4);

            if (this.found.Count == 0)
            {
                EditorGUILayout.HelpBox($"No se encontraron materiales con \"{this.sourceShaderName}\".", MessageType.Info);
                return;
            }

            int selected = this.CountSelected();
            EditorGUILayout.HelpBox(
                $"{this.found.Count} material(es) encontrado(s)  —  {selected} seleccionado(s).",
                MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Todos"))   this.SetAllSelected(true);
            if (GUILayout.Button("Ninguno")) this.SetAllSelected(false);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            this.scrollPos = EditorGUILayout.BeginScrollView(this.scrollPos);
            foreach (var entry in this.found)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                entry.Selected = EditorGUILayout.Toggle(entry.Selected, GUILayout.Width(18));
                EditorGUILayout.LabelField(entry.Material.name, GUILayout.Width(170));
                EditorGUILayout.LabelField(entry.Path, EditorStyles.miniLabel);
                if (GUILayout.Button("Ping", GUILayout.Width(42)))
                    EditorGUIUtility.PingObject(entry.Material);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);

            using (new EditorGUI.DisabledGroupScope(selected == 0))
            {
                if (GUILayout.Button($"Reemplazar {selected} material(es) seleccionado(s)", GUILayout.Height(32)))
                    this.ReplaceSelected();
            }
        }

        private void ScanProject()
        {
            this.found.Clear();
            this.hasScanned = true;

            string[] guids = AssetDatabase.FindAssets("t:Material");
            int      total = guids.Length;

            try
            {
                for (int i = 0; i < total; i++)
                {
                    string   path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    Material? mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                    if (mat == null || mat.shader == null) continue;
                    if (!mat.shader.name.Contains(this.sourceShaderName)) continue;

                    this.found.Add(new MaterialEntry(mat, path));
                    EditorUtility.DisplayProgressBar("Escaneando materiales…", path, (float)(i + 1) / total);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[PsxMaterialReplacer] Scan completo: {this.found.Count} material(es) con \"{this.sourceShaderName}\".");
            Repaint();
        }

        private void ReplaceSelected()
        {
            Shader? target = Shader.Find(this.targetShaderName);
            if (target == null)
            {
                EditorUtility.DisplayDialog(
                    "Shader no encontrado",
                    $"No se encontró \"{this.targetShaderName}\".\nVerificá que URP esté instalado y el nombre sea correcto.",
                    "OK");
                return;
            }

            var toReplace = new List<MaterialEntry>(this.found.Count);
            foreach (var e in this.found)
                if (e.Selected) toReplace.Add(e);

            int total    = toReplace.Count;
            int replaced = 0;

            try
            {
                for (int i = 0; i < total; i++)
                {
                    Material mat = toReplace[i].Material;
                    EditorUtility.DisplayProgressBar("Reemplazando shaders…", mat.name, (float)(i + 1) / total);

                    Undo.RecordObject(mat, "PSX → URP Shader Replacement");

                    Texture? savedTex   = this.preserveTexture ? ReadMainTexture(mat) : null;
                    Color    savedColor = this.preserveColor   ? ReadMainColor(mat)   : Color.white;

                    mat.shader = target;

                    if (this.preserveTexture && savedTex != null)
                        mat.SetTexture("_BaseMap", savedTex);
                    if (this.preserveColor)
                        mat.SetColor("_BaseColor", savedColor);

                    EditorUtility.SetDirty(mat);
                    replaced++;
                }

                AssetDatabase.SaveAssets();
                this.found.RemoveAll(e => e.Selected);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[PsxMaterialReplacer] {replaced} material(es) migrados a \"{this.targetShaderName}\".");
            EditorUtility.DisplayDialog(
                "Reemplazo completo",
                $"{replaced} de {total} material(es) migrados a \"{this.targetShaderName}\".",
                "OK");

            Repaint();
        }

        private static Texture? ReadMainTexture(Material mat)
        {
            if (mat.HasProperty("_MainTex"))
            {
                var t = mat.GetTexture("_MainTex");
                if (t != null) return t;
            }
            if (mat.HasProperty("_BaseMap"))
                return mat.GetTexture("_BaseMap");
            return null;
        }

        private static Color ReadMainColor(Material mat)
        {
            if (mat.HasProperty("_Color"))    return mat.GetColor("_Color");
            if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
            return Color.white;
        }

        private int CountSelected()
        {
            int count = 0;
            foreach (var e in this.found)
                if (e.Selected) count++;
            return count;
        }

        private void SetAllSelected(bool value)
        {
            foreach (var e in this.found)
                e.Selected = value;
            Repaint();
        }
    }
}
