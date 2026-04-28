#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Audio
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Audio/Surface Type Mapping")]
    public sealed class SurfaceTypeMapping : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public SurfaceType SurfaceType;
            public string      WwiseSwitchState;
        }

        [SerializeField] private Entry[] entries      = Array.Empty<Entry>();
        [SerializeField] private string  fallbackState = "Metal";

        private Dictionary<SurfaceType, string>? lookup;

        private void OnEnable()
        {
            lookup = new Dictionary<SurfaceType, string>(entries.Length);
            foreach (var e in entries)
            {
                if (e.SurfaceType != null)
                    lookup[e.SurfaceType] = e.WwiseSwitchState;
            }
        }

        public string Resolve(SurfaceType? surface)
        {
            if (surface != null && lookup != null && lookup.TryGetValue(surface, out var state))
                return state;
            return fallbackState;
        }
    }
}
