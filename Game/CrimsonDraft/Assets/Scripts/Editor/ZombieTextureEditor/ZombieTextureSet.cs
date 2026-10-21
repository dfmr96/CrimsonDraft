using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Editor
{
    /// <summary>
    /// Layered texture set for the Zombie Texture Editor. Each entry name must match a texture
    /// property of <see cref="SetMixMaterial"/>, which blends the layers into the final albedo.
    /// Ported from the Retro Horror Template Zombie Pack.
    /// </summary>
    [CreateAssetMenu(menuName = "CrimsonDraft/Zombie Texture Set")]
    public class ZombieTextureSet : ScriptableObject
    {
        [Serializable]
        public class ZombieTextureSetEntry
        {
            public string Name;
            public Texture2D[] Textures;
        }

        public Material SetMixMaterial;
        public List<ZombieTextureSetEntry> Entries;
    }
}
