using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.IO;

namespace HorrorEngine
{
    public class ZombieTextureEditorWindow : EditorWindow
    {
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
        
        private static readonly string k_DefaultFileName = "ZombieResultTexture";
        private static readonly string k_DefaultFileFormat = "png";

        private static ZombieTextureSet m_TextureSet;
        private static string m_OutputPath;
        private static Texture2D m_ClearTexture;

        [MenuItem("Horror Engine/Zombie Addon/Texture Editor")]
        public static void ShowWindow()
        {
            if (!m_ClearTexture)
            {
                m_ClearTexture = new Texture2D(2, 2);
                m_ClearTexture.SetPixel(0, 0, new Color(0, 0, 0, 0));
                m_ClearTexture.SetPixel(1, 0, new Color(0, 0, 0, 0));
                m_ClearTexture.SetPixel(0, 1, new Color(0, 0, 0, 0));
                m_ClearTexture.SetPixel(1, 1, new Color(0, 0, 0, 0));
                m_ClearTexture.alphaIsTransparency = true;
                m_ClearTexture.Apply();
            }

            if (string.IsNullOrEmpty(m_OutputPath))
                SetDefaultOutputPath();

            var window = GetWindow<ZombieTextureEditorWindow>("Zombie Texture Editor");
        }

        private static void SetDefaultOutputPath()
        {
            m_OutputPath = $"{Application.dataPath}/{k_DefaultFileName}.{k_DefaultFileFormat}";
        }

        private void OnGUI()
        {
            if (m_ResultRenderTexture == null)
            {
                m_ResultRenderTexture = new RenderTexture(ResultSize, ResultSize, 16, RenderTextureFormat.ARGB32);
                m_ResultRenderTexture.antiAliasing = 1;
                m_ResultRenderTexture.wrapMode = TextureWrapMode.Clamp;
                m_ResultRenderTexture.filterMode = FilterMode.Bilinear;
                m_ResultRenderTexture.Create();

                m_ResultRenderTextureFinal = new RenderTexture(ResultSize, ResultSize, 16, RenderTextureFormat.ARGB32);
                m_ResultRenderTextureFinal.antiAliasing = 1;
                m_ResultRenderTextureFinal.wrapMode = TextureWrapMode.Clamp;
                m_ResultRenderTextureFinal.filterMode = FilterMode.Bilinear;
                m_ResultRenderTextureFinal.Create();
            }
            
            EditorGUILayout.BeginVertical();

            
            var textureSet = (ZombieTextureSet)EditorGUILayout.ObjectField("Texture Set", m_TextureSet, typeof(ZombieTextureSet), false);
            if (textureSet != m_TextureSet)
            {
                SetDefaultOutputPath();
                m_TextureSet = textureSet;
                m_Slots.Clear();
            }

            if (m_TextureSet)
            {
                EditorGUILayout.BeginHorizontal();

                ShowTextureSet(m_TextureSet, m_Slots);
            
                EditorGUILayout.BeginVertical();
                GUILayout.Label("Result");

                if (m_IsResultDirty)
                    UpdateResult();

                GUILayout.Box(m_ResultRenderTextureFinal, GUILayout.Width(ResultSize * 0.5f), GUILayout.Height(ResultSize * 0.5f));
                Slider("Hue", ref m_ResultHue, -1f, 1f, () => { UpdateResult(); });
                Slider("Saturation", ref m_ResultSaturation, 0f, 2f, () => { UpdateResult(); });
                Slider("Lightness", ref m_ResultLightness, 0f, 2f, () => { UpdateResult(); });


                GUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Save"))
                {
                    m_OutputPath = EditorUtility.SaveFilePanel(
                        "Save Result Texture",
                        m_OutputPath,
                        k_DefaultFileName,
                        k_DefaultFileFormat
                    );

                    if (!string.IsNullOrEmpty(m_OutputPath))
                    {
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

        private void SaveResult(string path)
        {
            Texture2D texture2D = new Texture2D(m_ResultRenderTextureFinal.width, m_ResultRenderTextureFinal.height, TextureFormat.RGBA32, false);
            texture2D.ReadPixels(new Rect(0, 0, m_ResultRenderTextureFinal.width, m_ResultRenderTextureFinal.height), 0, 0);
            texture2D.Apply();

            byte[] bytes = texture2D.EncodeToPNG();
            File.WriteAllBytes(path, bytes);

            if (path.StartsWith(Application.dataPath))
            {
                AssetDatabase.Refresh();
            }

            DestroyImmediate(texture2D);
            Debug.Log("Saved RenderTexture as asset at: " + path);
        }
        

        private void ShowTextureSet(ZombieTextureSet set, Dictionary<string, TextureSlot> slots)
        {
            EditorGUILayout.BeginVertical();
            m_TextureSetScrollPos = EditorGUILayout.BeginScrollView(m_TextureSetScrollPos);
            foreach (var entry in set.Entries)
            {
                //if (entry.Textures.Length > 0) 
                //{
                    TextureSlot slot;

                    if (!slots.TryGetValue(entry.Name, out slot))
                    {
                        slot = new TextureSlot() { Name = entry.Name };
                        slot.RenderTexture = new RenderTexture(ResultSize, ResultSize, 0, RenderTextureFormat.ARGB32);
                        slot.RenderTexture.Create();

                        // TODO - Initialize texture
                        slots.Add(entry.Name, slot);
                        UpdateSlotWithTexture(slot, entry.Textures.Length > 0 ? entry.Textures[0] : m_ClearTexture);
                    }
                    ShowTextureSlot(slot, entry.Textures);
               // }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }


        private void ShowTextureSlot(TextureSlot slot, Texture2D[] options)
        {
            GUILayout.Label(slot.Name);

            // Texture Preview with buttons on each side
            EditorGUILayout.BeginHorizontal();

            if (options != null)
            {
                // Left Button
                if (GUILayout.Button("<", GUILayout.Width(30), GUILayout.Height(150)) && options.Length > 0)
                {
                    slot.TextureIndex = MathUtils.Wrap(slot.TextureIndex - 1, 0, options.Length-1);
                    UpdateSlotWithTexture(slot, options[slot.TextureIndex]);
                }
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

            if (options != null)
            {
                // Right Button
                if (GUILayout.Button(">", GUILayout.Width(30), GUILayout.Height(150)) && options.Length > 0)
                {
                    slot.TextureIndex = MathUtils.Wrap(slot.TextureIndex + 1, 0, options.Length-1);
                    UpdateSlotWithTexture(slot, options[slot.TextureIndex]);
                }
            }

            EditorGUILayout.BeginVertical();

            Slider("Hue", ref slot.Hue, -1f, 1f, () => { UpdateSlotWithTexture(slot, options[slot.TextureIndex]); });
            Slider("Saturation", ref slot.Saturation,  0f, 2f, () => { UpdateSlotWithTexture(slot, options[slot.TextureIndex]); });
            Slider("Lightness", ref slot.Lightness, 0f, 2f, () => { UpdateSlotWithTexture(slot, options[slot.TextureIndex]); });

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
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
                Debug.LogWarning("m_TextureSet has no mix material");
            }

            m_IsResultDirty = false;
        }

        private void UpdateSlotWithTexture(TextureSlot slot, Texture2D texture)
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