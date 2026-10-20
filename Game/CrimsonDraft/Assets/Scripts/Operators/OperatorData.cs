#nullable enable

using UnityEngine;
using Yarn.Unity;

namespace CrimsonDraft.Operators
{
    [CreateAssetMenu(fileName = "OperatorData", menuName = "CrimsonDraft/Operators/Operator Data")]
    public sealed class OperatorData : ScriptableObject
    {
        public const string UnknownName = "Unknown";

        [SerializeField] private string     operatorId        = string.Empty;
        [SerializeField] private string     displayName       = string.Empty;
        // Combat callsign -- shown in combat/inventory UI instead of displayName while that's
        // the active naming scheme, without losing the real name (still read as DisplayName
        // everywhere else, e.g. dialogue/save UI). Empty falls back to displayName so operators
        // added before this field existed don't show a blank label.
        [SerializeField] private string     callsign          = string.Empty;
        [SerializeField] private GameObject? battlefieldPrefab = null;
        [SerializeField] private Sprite     portrait          = null!;
        // Emblem of the organization the operator belongs to -- shown on the inventory card.
        // Empty keeps whatever logo the card prefab already has.
        [SerializeField] private Sprite?    logo              = null;
        // Bio page, first section: the operator's real name. Hidden behind "Unknown" until the
        // player has learned it (off by default).
        [SerializeField] private bool       nameKnown         = false;
        // Yarn node with the rest of the bio shown on the inventory card's Bio page. Lines, in
        // order: group / organization, specialty, brief description.
        [SerializeField] private DialogueReference bio = new();
        [SerializeField, Range(1, 99)]  private int speed = 50;
        [SerializeField, Min(1)]        private int maxHp  = 100;

        public string      OperatorId        => this.operatorId;
        public string      DisplayName       => this.displayName;
        public string      CombatName        => string.IsNullOrEmpty(this.callsign) ? this.displayName : this.callsign;
        public GameObject? BattlefieldPrefab => this.battlefieldPrefab;
        public Sprite      Portrait          => this.portrait;
        public bool        NameKnown         => this.nameKnown;
        public string      BioName           => this.nameKnown ? this.displayName : UnknownName;
        public Sprite?     Logo              => this.logo;
        public DialogueReference Bio         => this.bio;
        public int         Speed             => this.speed;
        public int         MaxHp             => this.maxHp;
    }
}
