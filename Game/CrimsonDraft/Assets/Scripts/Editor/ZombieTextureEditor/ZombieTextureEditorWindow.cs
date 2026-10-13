using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CrimsonDraft.Editor
{
    /// <summary>
    /// Builds a zombie albedo texture by picking a texture per layer (skin, clothes, hair...),
    /// tinting each one with HSL sliders and blending them through the set's mix material.
    /// The result can be saved as a PNG and optionally assigned to a material.
    /// Ported from the Retro Horror Template Zombie Pack.
    /// </summary>
    public class ZombieTextureEditorWindow : EditorWindow
    {
        private const string MenuPath      = "CrimsonDraft/Zombie Texture Editor";
        private const string HslShaderName = "HorrorEngine/ZombieAddOn/TextureColorAdjustHSL";

        // Defaults assigned through the script's .meta (MonoImporter default references).
        public Material AdjustHSLMaterial;
        public ZombieTextureSet DetailsSet;
        public ZombieTextureSet ClothesSet;

        public int ResultSize = 1024;

        class TextureSlot
        {
            public string Name;
            public RenderTexture RenderTexture;
            public float Hue = 0.0f;
            public float Saturation = 1.0f;
            public float Lightness = 1.0f;
            public float PreviewSize = 150f;
            public int TextureIndex = 0;
        }

        private RenderTexture m_ResultRenderTexture;
        private RenderTexture m_ResultRenderTextureFinal;
        private float m_ResultHue = 0;
        private float m_ResultSaturation = 1;
        private float m_ResultLightness = 1;
        private Dictionary<string, TextureSlot> m_Slots = new Dictionary<string, TextureSlot>();

        private bool m_IsResultDirty;
        private Vector2 m_TextureSetScrollPos;

        private ZombieTextureSet m_TextureSet;
        private Material m_TargetMaterial;
        private string m_OutputPath;
        private Texture2D m_ClearTexture;

        private static readonly string k_DefaultFileName = "ZombieResultTexture";
        private static readonly string k_DefaultFileFormat = "png";

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            GetWindow<ZombieTextureEditorWindow>("Zombie Texture Editor");
        }

        private void OnEnable()
        {
            if (!m_ClearTexture)
            {
                m_ClearTexture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                m_ClearTexture.SetPixels(new[] { Color.clear, Color.clear, Color.clear, Color.clear });
                m_ClearTexture.alphaIsTransparency = true;
                m_ClearTexture.Apply();
            }

            if (!AdjustHSLMaterial)
            {
                var shader = Shader.Find(HslShaderName);
                if (shader)
                    AdjustHSLMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            if (!m_TextureSet)
                m_TextureSet = ClothesSet;

            if (string.IsNullOrEmpty(m_OutputPath))
                SetDefaultOutputPath();
        }

        private void OnDisable()
        {
            ReleaseSlots();
            ReleaseRenderTexture(ref m_ResultRenderTexture);
            ReleaseRenderTexture(ref m_ResultRenderTextureFinal);
        }

        private void SetDefaultOutputPath()
        {
            m_OutputPath = $"{Application.dataPath}/{k_DefaultFileName}.{k_DefaultFileFormat}";
        }

        private void OnGUI()
        {
            if (!AdjustHSLMaterial)
            {
                EditorGUILayout.HelpBox($"Shader '{HslShaderName}' not found.", MessageType.Error);
                return;
            }

            if (m_ResultRenderTexture == null)
            {
                m_ResultRenderTexture = CreateRenderTexture(16);
                m_ResultRenderTextureFinal = CreateRenderTexture(16);
            }

            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            if (DetailsSet && GUILayout.Button("Details (head)"))
                SelectTextureSet(DetailsSet);
            if (ClothesSet && GUILayout.Button("Clothes (body)"))
                SelectTextureSet(ClothesSet);
            EditorGUILayout.EndHorizontal();

            var textureSet = (ZombieTextureSet)EditorGUILayout.ObjectField("Texture Set", m_TextureSet, typeof(ZombieTextureSet), false);
            if (textureSet != m_TextureSet)
                SelectTextureSet(textureSet);

            m_TargetMaterial = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Apply To Material", "Optional: the saved texture is assigned as this material's base map."),
                m_TargetMaterial, typeof(Material), false);

            if (m_TextureSet)
            {
                EditorGUILayout.BeginHorizontal();

                ShowTextureSet(m_TextureSet, m_Slots);

                EditorGUILayout.BeginVertical();
                GUILayout.Label("Result");

                if (m_IsResultDirty)
                    UpdateResult();

                GUILayout.Box(m_ResultRenderTextureFinal, GUILayout.Width(ResultSize * 0.5f), GUILayout.Height(ResultSize * 0.5f));
                Slider("Hue", ref m_ResultHue, -1f, 1f, UpdateResult);
                Slider("Saturation", ref m_ResultSaturation, 0f, 2f, UpdateResult);
                Slider("Lightness", ref m_ResultLightness, 0f, 2f, UpdateResult);

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Save"))
                {
                    var path = EditorUtility.SaveFilePanel(
                        "Save Result Texture",
                        Path.GetDirectoryName(m_OutputPath),
                        Path.GetFileNameWithoutExtension(m_OutputPath),
                        k_DefaultFileFormat
                    );

                    if (!string.IsNullOrEmpty(path))
                    {
                        m_OutputPath = path;
                        UpdateResult();
                        SaveResult(m_OutputPath);
                    }
                }
                GUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void SelectTextureSet(ZombieTextureSet textureSet)
        {
            if (textureSet == m_TextureSet)
                return;

            SetDefaultOutputPath();
            m_TextureSet = textureSet;
            ReleaseSlots();
        }

        private RenderTexture CreateRenderTexture(int depth)
        {
            var rt = new RenderTexture(ResultSize, ResultSize, depth, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            rt.Create();
            return rt;
        }

        private static void ReleaseRenderTexture(ref RenderTexture rt)
        {
            if (rt == null)
                return;

            rt.Release();
            DestroyImmediate(rt);
            rt = null;
        }

        private void ReleaseSlots()
        {
            foreach (var slot in m_Slots.Values)
                ReleaseRenderTexture(ref slot.RenderTexture);
            m_Slots.Clear();
        }

        private void SaveResult(string path)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = m_ResultRenderTextureFinal;

            Texture2D texture2D = new Texture2D(m_ResultRenderTextureFinal.width, m_ResultRenderTextureFinal.height, TextureFormat.RGBA32, false);
            texture2D.ReadPixels(new Rect(0, 0, m_ResultRenderTextureFinal.width, m_ResultRenderTextureFinal.height), 0, 0);
            texture2D.Apply();

            RenderTexture.active = previous;

            byte[] bytes = texture2D.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            DestroyImmediate(texture2D);

            path = path.Replace('\\', '/');
            if (path.StartsWith(Application.dataPath))
            {
                var assetPath = "Assets" + path.Substring(Application.dataPath.Length);
                AssetDatabase.ImportAsset(assetPath);

                if (m_TargetMaterial)
                    ApplyToMaterial(AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath));
            }
            else if (m_TargetMaterial)
            {
                Debug.LogWarning("[ZombieTextureEditor] Texture saved outside Assets/, it was not applied to the material.");
            }

            Debug.Log("[ZombieTextureEditor] Saved texture at: " + path);
        }

        private void ApplyToMaterial(Texture2D texture)
        {
            if (!texture)
                return;

            Undo.RecordObject(m_TargetMaterial, "Apply Zombie Texture");
            if (m_TargetMaterial.HasProperty("_BaseMap"))
                m_TargetMaterial.SetTexture("_BaseMap", texture);
            if (m_TargetMaterial.HasProperty("_MainTex"))
                m_TargetMaterial.SetTexture("_MainTex", texture);
            EditorUtility.SetDirty(m_TargetMaterial);
            AssetDatabase.SaveAssetIfDirty(m_TargetMaterial);
        }

        private void ShowTextureSet(ZombieTextureSet set, Dictionary<string, TextureSlot> slots)
        {
            EditorGUILayout.BeginVertical();
            m_TextureSetScrollPos = EditorGUILayout.BeginScrollView(m_TextureSetScrollPos);
            foreach (var entry in set.Entries)
            {
                if (!slots.TryGetValue(entry.Name, out var slot))
                {
                    slot = new TextureSlot() { Name = entry.Name };
                    slot.RenderTexture = CreateRenderTexture(0);

                    slots.Add(entry.Name, slot);
                    UpdateSlotWithTexture(slot, GetOption(entry.Textures, 0));
                }
                ShowTextureSlot(slot, entry.Textures);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private Texture GetOption(Texture2D[] options, int index)
        {
            return options != null && options.Length > 0 && options[index] ? options[index] : m_ClearTexture;
        }

        private void ShowTextureSlot(TextureSlot slot, Texture2D[] options)
        {
            int optionCount = options?.Length ?? 0;
            GUILayout.Label(optionCount > 1 ? $"{slot.Name}  ({slot.TextureIndex + 1}/{optionCount})" : slot.Name);

            // Texture Preview with buttons on each side
            EditorGUILayout.BeginHorizontal();

            // Left Button
            if (GUILayout.Button("<", GUILayout.Width(30), GUILayout.Height(150)) && optionCount > 0)
            {
                slot.TextureIndex = Wrap(slot.TextureIndex - 1, optionCount);
                UpdateSlotWithTexture(slot, GetOption(options, slot.TextureIndex));
            }

            // Texture Preview
            if (slot.RenderTexture != null)
            {
                GUILayout.Box(slot.RenderTexture, GUILayout.Width(slot.PreviewSize), GUILayout.Height(slot.PreviewSize));
            }
            else
            {
                GUILayout.Box("No Texture Selected", GUILayout.Width(slot.PreviewSize), GUILayout.Height(slot.PreviewSize));
            }

            // Right Button
            if (GUILayout.Button(">", GUILayout.Width(30), GUILayout.Height(150)) && optionCount > 0)
            {
                slot.TextureIndex = Wrap(slot.TextureIndex + 1, optionCount);
                UpdateSlotWithTexture(slot, GetOption(options, slot.TextureIndex));
            }

            EditorGUILayout.BeginVertical();

            Action refresh = () => UpdateSlotWithTexture(slot, GetOption(options, slot.TextureIndex));
            Slider("Hue", ref slot.Hue, -1f, 1f, refresh);
            Slider("Saturation", ref slot.Saturation, 0f, 2f, refresh);
            Slider("Lightness", ref slot.Lightness, 0f, 2f, refresh);

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private static int Wrap(int value, int count)
        {
            return ((value % count) + count) % count;
        }

        private void Slider(string name, ref float val, float minVal, float maxVal, Action onChange)
        {
            EditorGUILayout.LabelField(name);
            float newVal = EditorGUILayout.Slider(val, minVal, maxVal);
            if (val != newVal)
            {
                val = newVal;
                onChange.Invoke();
            }
        }

        private void UpdateResult()
        {
            var mixMaterial = m_TextureSet.SetMixMaterial;
            if (mixMaterial)
            {
                foreach (var t in m_Slots)
                {
                    mixMaterial.SetTexture(t.Key, t.Value.RenderTexture);
                }
                Graphics.Blit(Texture2D.blackTexture, m_ResultRenderTexture, mixMaterial);

                UpdateTextureHSL(m_ResultRenderTexture, m_ResultRenderTextureFinal, m_ResultHue, m_ResultSaturation, m_ResultLightness);
            }
            else
            {
                Debug.LogWarning("[ZombieTextureEditor] Texture set has no mix material");
            }

            m_IsResultDirty = false;
        }

        private void UpdateSlotWithTexture(TextureSlot slot, Texture texture)
        {
            UpdateTextureHSL(texture, slot.RenderTexture, slot.Hue, slot.Saturation, slot.Lightness);
        }

        private void UpdateTextureHSL(Texture from, RenderTexture to, float h, float s, float l)
        {
            AdjustHSLMaterial.SetTexture("_MainTex", from);
            AdjustHSLMaterial.SetFloat("_Hue", h);
            AdjustHSLMaterial.SetFloat("_Saturation", s);
            AdjustHSLMaterial.SetFloat("_Lightness", l);
            Graphics.Blit(from, to, AdjustHSLMaterial);

            m_IsResultDirty = true;
        }
    }
}
