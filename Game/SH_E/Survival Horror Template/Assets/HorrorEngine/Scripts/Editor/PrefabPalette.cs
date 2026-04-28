using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    [Serializable]
    public class PrefabPaletteEntry
    {
        public Texture Icon;
        public GameObject Prefab;
        public SceneLayer Layer;
    }

    [CreateAssetMenu(fileName = "Prefab Palette", menuName = "Horror Engine/Design/Prefab Palette")]
    public class PrefabPalette : ScriptableObject
    {
        public string Name;
        public List<PrefabPalette> SubPalettes;
        public List<PrefabPaletteEntry> Prefabs;
        public bool Horizontal;
        public bool IsSubpalette;
    }
}