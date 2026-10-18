#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Navigation.Radio
{
    /// <summary>
    /// Who can speak on the radio. The ids are the ones in Tools/Dialogue/personajes.json (the
    /// speaker written in the .yarn lines, e.g. "deer"); the roster operators are matched to them
    /// by callsign (OperatorData.CombatName, lower-cased).
    /// </summary>
    [CreateAssetMenu(fileName = "RadioCastData", menuName = "CrimsonDraft/Radio/Cast Data")]
    public sealed class RadioCastData : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string  id          = string.Empty;
            public string  displayName = string.Empty;
            public Color   color       = Color.white;
            public Sprite? portrait;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public Entry[] Entries => this.entries;

        public bool TryGet(string id, out Entry entry)
        {
            foreach (var e in this.entries)
            {
                if (string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase))
                {
                    entry = e;
                    return true;
                }
            }

            entry = null!;
            return false;
        }
    }
}
