using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class OnOverlapAttack : AttackBase
    {
        private const string k_DebugCategory = "OverlapAttack";
        [Tooltip("Delay applied recursively to wait before applying the attack impact is checked. Set Delay to 0 to check every fixedUpdate. If Duration is 0 this is at least waited once")]
        public float DamageRate = 1f;
        [Tooltip("Duration passed before the attack deactivates. Set to -1 for infinite duration")]
        public float Duration = 0f;
        [Tooltip("The HitShape used for this attack. If null it will be try to be obtained from this object")]
        [FormerlySerializedAs("m_HitBox")]
        [SerializeField] private HitShape m_HitShape;
        [Tooltip("This determines the maximum number of damageables that will be hit. Leave at 0 to hit all damageables")]
        [SerializeField] private int m_PenetrationHits = 1;
        [Tooltip("This determines if the attack should start when it gets created. It automatically disables otherwise")]
        [SerializeField] public bool StartAttackOnStart = true;
        [Tooltip("This determines if this attack can hit the same damageable after a successfull hit")]
        [SerializeField] private bool m_CanReHitDamageable;
        
        public UnityEvent<AttackInfo> OnImpact;

        private DamageableSorting m_DamageableSort = new DamageableSorting();
        private float m_CurrentDuration;
        private float m_Time;
        private int m_Hits;
        private List<Damageable> m_Damageables = new List<Damageable>();

        

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            if (m_HitShape == null)
                m_HitShape = GetComponent<HitShape>();
        }

        // --------------------------------------------------------------------

        private void Start()
        {
            if (!StartAttackOnStart)
            {
                enabled = false;
            }
            else
            {
                StartAttack();
            }
        }

        // --------------------------------------------------------------------

        protected override void OnEnable()
        {
            base.OnEnable();

            m_Hits = 0;
            m_DamageableSort.Clear();
            m_CurrentDuration = 0f;
            m_Time = 0;

            Hit();
        }

        // --------------------------------------------------------------------

        public void FixedUpdate()
        {
            m_CurrentDuration += Time.deltaTime;
            m_Time += Time.deltaTime;
            if (m_Time >= DamageRate)
            {
                Hit();
            }

            if (Duration >= 0 && m_CurrentDuration >= Duration)
            {
                enabled = false;
                return;
            }
        }

        // --------------------------------------------------------------------

        private void Hit()
        {
            if (!m_Attack)
            {
                Debug.LogError("Attack has not been set but Hit has been called", gameObject);
                return;
            }

            if (m_CanReHitDamageable)
            {
               m_DamageableSort.Clear();
            }
			m_HitShape.GetOverlappingDamageables(m_Damageables, k_DebugCategory);
            m_DamageableSort.SortAndGetImpacted(ref m_Damageables, m_Attack);

            foreach (Damageable damageable in m_Damageables)
            {
                Vector3 hitShapePos = m_HitShape.transform.position;
                Vector3 fakeHitPoint = (damageable.transform.position + hitShapePos) * 0.5f;
                Vector3 impactDir = (damageable.transform.position - hitShapePos).normalized;
                var attackInfo = new AttackInfo()
                {
                    Attack = this,
                    Damageable = damageable,
                    ImpactDir = impactDir,
                    ImpactPoint = fakeHitPoint
                };
                Process(attackInfo);
                OnImpact?.Invoke(attackInfo);

                ++m_Hits;
                if (m_PenetrationHits > 0 && m_Hits >= m_PenetrationHits)
                    break;
            }

            m_Time = 0f;
        }

        // --------------------------------------------------------------------

        public override void StartAttack()
        {
            enabled = true;

            base.StartAttack();
        }

        // --------------------------------------------------------------------

        public override void StopAttack()
        {
            enabled = false;

            base.StopAttack();
        }

    }
}