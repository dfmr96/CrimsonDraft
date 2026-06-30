#nullable enable

using UnityEngine;

namespace CrimsonDraft.Operators
{
    [CreateAssetMenu(fileName = "OperatorData", menuName = "CrimsonDraft/Operators/Operator Data")]
    public sealed class OperatorData : ScriptableObject
    {
        [SerializeField] private string     operatorId        = string.Empty;
        [SerializeField] private string     displayName       = string.Empty;
        [SerializeField] private GameObject? battlefieldPrefab = null;
        [SerializeField] private Sprite     portrait          = null!;
        [SerializeField, Range(1, 99)]  private int speed = 50;
        [SerializeField, Min(1)]        private int maxHp  = 100;

        public string      OperatorId        => this.operatorId;
        public string      DisplayName       => this.displayName;
        public GameObject? BattlefieldPrefab => this.battlefieldPrefab;
        public Sprite      Portrait          => this.portrait;
        public int         Speed             => this.speed;
        public int         MaxHp             => this.maxHp;
    }
}
