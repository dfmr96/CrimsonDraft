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
            public SurfaceType     SurfaceType;
            public AK.Wwise.Switch WwiseSwitch;
        }

        [SerializeField] private Entry[]           entries        = Array.Empty<Entry>();
        [SerializeField] private AK.Wwise.Switch   fallbackSwitch = new AK.Wwise.Switch();

        private Dictionary<SurfaceType, AK.Wwise.Switch>? lookup;

        private void OnEnable()
        {
            lookup = new Dictionary<SurfaceType, AK.Wwise.Switch>(entries.Length);
            foreach (var e in entries)
            {
                if (e.SurfaceType != null)
                    lookup[e.SurfaceType] = e.WwiseSwitch;
            }
        }

        public AK.Wwise.Switch Resolve(SurfaceType? surface)
        {
            if (surface != null && lookup != null && lookup.TryGetValue(surface, out var sw))
                return sw;
            return fallbackSwitch;
        }
    }
}
