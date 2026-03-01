#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    [CreateAssetMenu(fileName = "EncounterData", menuName = "CrimsonDraft/Combat/Encounter Data")]
    public sealed class EncounterData : ScriptableObject
    {
        [SerializeField] private string       encounterId = string.Empty;
        [SerializeField] private EnemyData?[] enemySlots  = new EnemyData?[6];
        [SerializeField] private OperatorData?[] operators = new OperatorData?[4];

        public string          EncounterId => this.encounterId;
        public EnemyData?[]    EnemySlots  => this.enemySlots;
        public OperatorData?[] Operators   => this.operators;
    }
}
