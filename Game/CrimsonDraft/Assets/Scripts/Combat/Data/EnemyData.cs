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
        [SerializeField] private AimHitMaskProfile? staggeredHitMaskProfile = null;
        [SerializeField] private int[]              maxHpPool          = System.Array.Empty<int>();
        [SerializeField] private float[]             attackBaseSecPool = System.Array.Empty<float>();
        [SerializeField, Min(0f)] private float     attackJitterSec = 1.25f;
        [SerializeField, Min(0f)] private float     attackDurationSec = 1.2f;
        [SerializeField, Min(0)] private int        attackDamage = 10;
        [SerializeField, Range(0f, 100f)] private float initialGaugePct = 0f;
        [SerializeField, Min(0)] private int   minPoise               = 15;
        [SerializeField, Min(0)] private int   maxPoise               = 30;
        [SerializeField, Range(0f, 100f)] private float staggerHpThresholdPct = 40f;
        [SerializeField, Min(0)] private int   staggerRecoveryActionCount = 2;

        public string EnemyId                    => this.enemyId;
        public GameObject? BattlefieldPrefab     => this.battlefieldPrefab;
        public Sprite Sprite                     => this.sprite;
        public AimHitMaskProfile? HitMaskProfile => this.hitMaskProfile;
        public AimHitMaskProfile? StaggeredHitMaskProfile => this.staggeredHitMaskProfile;
        public int[] MaxHpPool                   => this.maxHpPool;
        public float[] AttackBaseSecPool         => this.attackBaseSecPool;
        public float AttackJitterSec             => this.attackJitterSec;
        public float AttackDurationSec           => this.attackDurationSec;
        public int AttackDamage                  => this.attackDamage;
        public float InitialGaugePct             => this.initialGaugePct;
        public int   MinPoise                    => this.minPoise;
        public int   MaxPoise                    => this.maxPoise;
        public float StaggerHpThresholdPct       => this.staggerHpThresholdPct;
        public int   StaggerRecoveryActionCount  => this.staggerRecoveryActionCount;
    }
}
