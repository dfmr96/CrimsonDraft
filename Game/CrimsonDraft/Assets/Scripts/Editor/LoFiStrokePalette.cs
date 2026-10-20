#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Editor
{
    /// <summary>
    /// Exact colour ramps for the Lo-Fi Stroke font shader. Each entry maps a key colour (the colour used in
    /// &lt;color&gt; tags or Yarn markup) to 8 hand-picked tones: 1-3 fill, 4-7 smoothing, 8 outline.
    /// Applying the palette bakes a 9x16 texture (column 0 = key) and assigns it to the listed materials;
    /// colours without a key fall back to the shader's tint.
    /// </summary>
    [CreateAssetMenu(menuName = "CrimsonDraft/UI/Lo-Fi Stroke Palette", fileName = "LoFiStrokePalette")]
    public sealed class LoFiStrokePalette : ScriptableObject
    {
        public const int MaxRows = 16;
        public const int Steps   = 8;
        public const int Columns = Steps + 1;

        [Serializable]
        public sealed class Entry
        {
            public string    name = "New";
            [Tooltip("Exact colour of the <color> tag or Yarn marker that selects this ramp.")]
            public Color32   key  = new(255, 255, 255, 255);
            [Tooltip("1-3 fill (light to dark), 4-7 smoothing, 8 outline.")]
            public Color32[] ramp = new Color32[Steps];

            public Entry() { }

            public Entry(string name, Color32 key, Color32[] ramp)
            {
                this.name = name;
                this.key  = key;
                this.ramp = ramp;
            }
        }

        [SerializeField] private List<Entry>    entries   = new();
        [SerializeField] private Texture2D?     texture;
        [SerializeField] private List<Material> materials = new();

        public List<Entry>    Entries   => this.entries;
        public List<Material> Materials => this.materials;

        public Texture2D? Texture
        {
            get => this.texture;
            set => this.texture = value;
        }
    }
}
