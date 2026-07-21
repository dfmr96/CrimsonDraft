#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "CrimsonDraft/Combat/Enemy Data")]
    public sealed class EnemyData : ScriptableObject
    {
        [SerializeField] private string             enemyId           = string.Empty;
        [SerializeField] private GameObject?        battlefieldPrefab = null;
        [SerializeField] private Sprite             sprite            = null!;
        [SerializeField] private AimHitMaskProfile? hitMaskProfile    = null;
        [SerializeField] private int                maxHp          = 100;
        [SerializeField, Min(0f)] private float     attackBaseSec = 7f;
        [SerializeField, Min(0f)] private float     attackJitterSec = 1.25f;
        [SerializeField, Min(0f)] private float     attackDurationSec = 1.2f;
        [SerializeField, Min(0)] private int        attackDamage = 10;
        [SerializeField, Range(0f, 100f)] private float initialGaugePct = 0f;

        public string EnemyId                    => this.enemyId;
        public GameObject? BattlefieldPrefab     => this.battlefieldPrefab;
        public Sprite Sprite                     => this.sprite;
        public AimHitMaskProfile? HitMaskProfile => this.hitMaskProfile;
        public int MaxHp                         => this.maxHp;
        public float AttackBaseSec               => this.attackBaseSec;
        public float AttackJitterSec             => this.attackJitterSec;
        public float AttackDurationSec           => this.attackDurationSec;
        public int AttackDamage                  => this.attackDamage;
        public float InitialGaugePct             => this.initialGaugePct;
    }
}
