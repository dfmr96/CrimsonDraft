#nullable enable

using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat
{
    [CreateAssetMenu(fileName = "EncounterData", menuName = "CrimsonDraft/Combat/Encounter Data")]
    public sealed class EncounterData : ScriptableObject
    {
        [SerializeField] private EnemyData?[]    enemySlots = new EnemyData?[6];
        [SerializeField] private OperatorData?[] operators  = new OperatorData?[4];

        public EnemyData?[]    EnemySlots => this.enemySlots;
        public OperatorData?[] Operators  => this.operators;
    }
}
