#nullable enable

using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CrimsonDraft.Editor
{
    [CustomEditor(typeof(LoFiStrokePalette))]
    public sealed class LoFiStrokePaletteEditor : UnityEditor.Editor
    {
        private const string CreateMenuPath     = "CrimsonDraft/Fonts/Create Default Lo-Fi Stroke Palette";
        private const string DefaultPalettePath = "Assets/Art/UI/LoFiStroke/Silver LoFi Stroke Palette.asset";
        private const string DefaultFontPath    = "Assets/Resources/Fonts/Silver LoFi Stroke.asset";

        private static readonly int PaletteTexId  = Shader.PropertyToID("_PaletteTex");
        private static readonly int PaletteRowsId = Shader.PropertyToID("_PaletteRows");

        private static readonly int[] RampIds =
        {
            Shader.PropertyToID("_Ramp1"), Shader.PropertyToID("_Ramp2"), Shader.PropertyToID("_Ramp3"), Shader.PropertyToID("_Ramp4"),
            Shader.PropertyToID("_Ramp5"), Shader.PropertyToID("_Ramp6"), Shader.PropertyToID("_Ramp7"), Shader.PropertyToID("_Ramp8"),
        };

        // Brightness per step when generating a ramp from a key colour (fill light -> outline).
        private static readonly float[] GeneratedValue = { 1f, 0.87f, 0.74f, 0.63f, 0.52f, 0.42f, 0.30f, 0.20f };

        public override void OnInspectorGUI()
        {
            var palette = (LoFiStrokePalette)this.target;

            EditorGUILayout.HelpBox(
                "Each row maps a key colour (the <color> tag or Yarn marker colour) to 8 tones: " +
                "1-3 fill, 4-7 smoothing, 8 outline. Colours without a key use the shader's tint.",
                MessageType.Info);

            this.DrawDefaultInspector();

            EditorGUILayout.Space(6);
            foreach (var problem in Validate(palette))
                EditorGUILayout.HelpBox(problem, MessageType.Warning);

            EditorGUILayout.LabelField("Generate ramp from key", EditorStyles.boldLabel);
            foreach (var entry in palette.Entries)
            {
                if (GUILayout.Button(entry.name))
                {
                    Undo.RecordObject(palette, "Generate ramp");
                    entry.ramp = GenerateRamp(entry.key);
                    EditorUtility.SetDirty(palette);
                }
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Apply to materials", GUILayout.Height(28)))
                Debug.Log(Apply(palette));

            if (GUILayout.Button("Reset to defaults") &&
                EditorUtility.DisplayDialog("Lo-Fi Stroke Palette", "Replace every row with the default ramps?", "Reset", "Cancel"))
            {
                ResetToDefaults(palette);
            }
        }

        /// <summary>Replaces the rows with the default ramps and applies them (keeps the asset and texture GUIDs).</summary>
        public static void ResetToDefaults(LoFiStrokePalette palette)
        {
            Undo.RecordObject(palette, "Reset Lo-Fi Stroke palette");
            palette.Entries.Clear();
            palette.Entries.AddRange(DefaultEntries());
            EditorUtility.SetDirty(palette);
            Debug.Log(Apply(palette));
        }

        [MenuItem(CreateMenuPath)]
        public static void CreateDefault()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LoFiStrokePalette>(DefaultPalettePath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                Debug.Log($"[LoFiStrokePalette] Already exists: {DefaultPalettePath}");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(DefaultPalettePath)!);

            var palette = CreateInstance<LoFiStrokePalette>();
            palette.Entries.AddRange(DefaultEntries());

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);
            if (font != null && font.material != null)
                palette.Materials.Add(font.material);

            AssetDatabase.CreateAsset(palette, DefaultPalettePath);
            Debug.Log(Apply(palette));
            Selection.activeObject = palette;
        }

        /// <summary>
        /// Bakes the palette texture (column 0 = key, columns 1-8 = ramp, one row per entry, gamma values)
        /// and assigns it to every listed material.
        /// </summary>
        public static string Apply(LoFiStrokePalette palette)
        {
            var rows   = Mathf.Min(palette.Entries.Count, LoFiStrokePalette.MaxRows);
            var pixels = new Color32[LoFiStrokePalette.Columns * LoFiStrokePalette.MaxRows];

            for (var row = 0; row < rows; row++)
            {
                var entry = palette.Entries[row];
                pixels[row * LoFiStrokePalette.Columns] = entry.key;
                for (var step = 0; step < LoFiStrokePalette.Steps; step++)
                {
                    var colour = step < entry.ramp.Length ? entry.ramp[step] : default;
                    colour.a = 255;
                    pixels[row * LoFiStrokePalette.Columns + step + 1] = colour;
                }
            }

            var texture = palette.Texture;
            if (texture == null)
            {
                texture = new Texture2D(LoFiStrokePalette.Columns, LoFiStrokePalette.MaxRows, TextureFormat.RGBA32, false, true)
                {
                    name = palette.name + " Texture",
                };
                var palettePath = AssetDatabase.GetAssetPath(palette);
                var texturePath = Path.Combine(Path.GetDirectoryName(palettePath)!, texture.name + ".asset").Replace('\\', '/');
                AssetDatabase.CreateAsset(texture, texturePath);
                palette.Texture = texture;
                EditorUtility.SetDirty(palette);
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode   = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);

            // The row keyed white (untagged text) is also the base ramp that tinted colours multiply.
            var white = palette.Entries.Find(e => e.key.r == 255 && e.key.g == 255 && e.key.b == 255);

            var applied = 0;
            foreach (var material in palette.Materials)
            {
                if (material == null || !material.HasProperty(PaletteTexId))
                    continue;

                material.SetTexture(PaletteTexId, texture);
                material.SetFloat(PaletteRowsId, rows);
                if (white != null)
                {
                    for (var step = 0; step < LoFiStrokePalette.Steps && step < white.ramp.Length; step++)
                        material.SetColor(RampIds[step], white.ramp[step]);
                }
                EditorUtility.SetDirty(material);
                applied++;
            }

            AssetDatabase.SaveAssets();
            return $"[LoFiStrokePalette] {palette.name}: {rows} exact ramp(s) applied to {applied} material(s).";
        }

        private static IEnumerable<string> Validate(LoFiStrokePalette palette)
        {
            if (palette.Entries.Count > LoFiStrokePalette.MaxRows)
                yield return $"There are {palette.Entries.Count} rows; only the first {LoFiStrokePalette.MaxRows} are used.";

            for (var i = 0; i < palette.Entries.Count; i++)
            {
                if (palette.Entries[i].ramp.Length != LoFiStrokePalette.Steps)
                    yield return $"'{palette.Entries[i].name}' has {palette.Entries[i].ramp.Length} tones; it needs {LoFiStrokePalette.Steps}.";

                for (var j = i + 1; j < palette.Entries.Count; j++)
                {
                    if (KeysTooClose(palette.Entries[i].key, palette.Entries[j].key))
                        yield return $"The keys of '{palette.Entries[i].name}' and '{palette.Entries[j].name}' " +
                                     "differ by less than 2/255 per channel: the shader may mix them up.";
                }
            }

            if (palette.Materials.Count == 0)
                yield return "No materials assigned: 'Apply' will only update the texture.";
        }

        private static bool KeysTooClose(Color32 a, Color32 b) =>
            Mathf.Abs(a.r - b.r) <= 2 && Mathf.Abs(a.g - b.g) <= 2 && Mathf.Abs(a.b - b.b) <= 2;

        /// <summary>
        /// Starting point for a hand-tuned ramp: the key's hue with decreasing brightness and slightly more
        /// saturation in the darker steps.
        /// </summary>
        private static Color32[] GenerateRamp(Color32 key)
        {
            Color.RGBToHSV(key, out var hue, out var saturation, out var value);
            var ramp = new Color32[LoFiStrokePalette.Steps];
            for (var step = 0; step < ramp.Length; step++)
            {
                var s = Mathf.Clamp01(saturation * (1f + 0.15f * step / (ramp.Length - 1)));
                ramp[step] = Color.HSVToRGB(hue, s, value * GeneratedValue[step]);
            }

            return ramp;
        }

        /// <summary>
        /// Initial rows. "Normal" (key white, i.e. untagged text) is a brightened version of "Bone"; the other
        /// ramps come from the reference research. Keys are the lightest fill tone of each ramp.
        /// </summary>
        private static IEnumerable<LoFiStrokePalette.Entry> DefaultEntries()
        {
            static Color32 C(byte r, byte g, byte b) => new(r, g, b, 255);

            yield return new LoFiStrokePalette.Entry("Normal", C(255, 255, 255), new[]
            {
                C(248, 248, 236), C(232, 232, 220), C(200, 200, 188), C(160, 160, 150),
                C(128, 128, 120), C(92, 92, 88), C(64, 64, 64), C(56, 48, 72),
            });
            yield return new LoFiStrokePalette.Entry("Bone", C(216, 216, 200), new[]
            {
                C(216, 216, 200), C(200, 200, 184), C(168, 168, 152), C(136, 136, 128),
                C(112, 112, 104), C(80, 80, 80), C(56, 56, 56), C(56, 48, 72),
            });
            yield return new LoFiStrokePalette.Entry("Green", C(0, 184, 40), new[]
            {
                C(0, 184, 40), C(0, 160, 32), C(0, 136, 24), C(0, 120, 16),
                C(0, 96, 16), C(0, 80, 8), C(0, 56, 0), C(0, 40, 0),
            });
            yield return new LoFiStrokePalette.Entry("Crimson", C(152, 0, 72), new[]
            {
                C(152, 0, 72), C(144, 0, 64), C(120, 0, 40), C(96, 0, 24),
                C(80, 0, 0), C(72, 0, 0), C(56, 0, 0), C(16, 0, 0),
            });
            yield return new LoFiStrokePalette.Entry("Gray", C(104, 104, 104), new[]
            {
                C(104, 104, 104), C(88, 88, 88), C(72, 72, 72), C(56, 56, 56),
                C(40, 40, 40), C(24, 24, 24), C(16, 16, 16), C(8, 8, 8),
            });
            yield return new LoFiStrokePalette.Entry("Blue", C(32, 80, 232), new[]
            {
                C(32, 80, 232), C(24, 72, 224), C(24, 64, 200), C(24, 56, 168),
                C(16, 48, 120), C(16, 32, 104), C(16, 16, 80), C(8, 8, 64),
            });
        }
    }
}
