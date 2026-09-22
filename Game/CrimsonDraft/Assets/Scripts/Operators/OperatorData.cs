#nullable enable

using UnityEngine;

namespace CrimsonDraft.Operators
{
    [CreateAssetMenu(fileName = "OperatorData", menuName = "CrimsonDraft/Operators/Operator Data")]
    public sealed class OperatorData : ScriptableObject
    {
        [SerializeField] private string     operatorId        = string.Empty;
        [SerializeField] private string     displayName       = string.Empty;
        // Combat callsign -- shown in combat/inventory UI instead of displayName while that's
        // the active naming scheme, without losing the real name (still read as DisplayName
        // everywhere else, e.g. dialogue/save UI). Empty falls back to displayName so operators
        // added before this field existed don't show a blank label.
        [SerializeField] private string     callsign          = string.Empty;
        [SerializeField] private GameObject? battlefieldPrefab = null;
        [SerializeField] private Sprite     portrait          = null!;
        [SerializeField, Range(1, 99)]  private int speed = 50;
        [SerializeField, Min(1)]        private int maxHp  = 100;

        public string      OperatorId        => this.operatorId;
        public string      DisplayName       => this.displayName;
        public string      CombatName        => string.IsNullOrEmpty(this.callsign) ? this.displayName : this.callsign;
        public GameObject? BattlefieldPrefab => this.battlefieldPrefab;
        public Sprite      Portrait          => this.portrait;
        public int         Speed             => this.speed;
        public int         MaxHp             => this.maxHp;
    }
}
