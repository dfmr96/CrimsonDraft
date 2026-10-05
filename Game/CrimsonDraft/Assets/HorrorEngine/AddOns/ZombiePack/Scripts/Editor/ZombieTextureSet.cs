using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Zombie AddOn/Texture Set")]
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