#nullable enable

using System;
using System.Linq;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    [CreateAssetMenu(fileName = "EncounterDatabase", menuName = "CrimsonDraft/Combat/Encounter Database")]
    public sealed class EncounterDatabase : ScriptableObject
    {
        [SerializeField] private EncounterData[] encounters = Array.Empty<EncounterData>();

        public EncounterData? GetById(string encounterId) =>
            this.encounters.FirstOrDefault(e => e.name == encounterId);
    }
}
