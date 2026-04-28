using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{

    interface IShotAttackAiming
    {
        Vector3 GetDirection(Vector3 origin, float maxDistance);
    }

    public struct ShotHit
    {
        public Damageable Damageable;
        public Vector3 ImpactPoint;
        public Vector3 ImpactNormal;
        public Collider Collider;
    }

    [RequireComponent(typeof(SocketController))]
    [RequireComponent(typeof(AudioSource))]
    public class ShotAttack : WeaponAttack, IVerticalAttack
    {
        [SerializeField] private SocketHandle m_AttackOriginSocket;
        [SerializeField] private float m_NoAmmoAttackDuration;

        [Header("Attack Visuals")]
        [SerializeField] private ObjectInstantiator m_MuzzleInstantiator;
        [SerializeField] private ObjectInstantiator m_ShellKickInstantiator;
        [SerializeField] private float m_ShellKickDelay;

        [Header("Attack Size & Range")]
        [SerializeField] private float m_CastRadius = 0.5f;
        [SerializeField] private float m_MaxRange = 100f;
        [SerializeField] private float m_ForwardOffset = 0f;
        [SerializeField] private float m_Width = 0f;

        [Range(1, 10)]
        [SerializeField] private int m_PenetrationHits = 1;
        [SerializeField] private LayerMask m_LayerMask;
        [SerializeField] private LayerMask m_ObstructionLayerMask;
        [SerializeField] private float m_VerticalityFactor;

        private AudioSource m_AudioSource;
        private RaycastHit[] m_HitResults = new RaycastHit[10];
        private List<ShotHit> m_SortedHits = new List<ShotHit>();
        private HashSet<Combatant> m_HitCombatants = new HashSet<Combatant>();

        private SocketController m_SocketCtrl;
        private float m_Verticality;
        
        private IShotAttackAiming m_AttackAiming;
        private string k_DebugCategory = "ShotAttack";

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_SocketCtrl = GetComponent<SocketController>();
            m_AudioSource = GetComponent<AudioSource>();
        }

        // --------------------------------------------------------------------

        protected override void OnEnable()
        {
            base.OnEnable();

            m_AttackAiming = GetComponentInParent<IShotAttackAiming>();
        }

        // --------------------------------------------------------------------

        public void SetVerticality(float verticality)
        {
            m_Verticality = verticality;
        }

        // --------------------------------------------------------------------

        public override void StartAttack()
        {
            base.StartAttack();

            Socket originSocket = m_SocketCtrl.GetSocket(m_AttackOriginSocket);
            Vector3 originPos = originSocket.transform.position + originSocket.transform.forward * m_ForwardOffset;


            Vector3 dir = originSocket.transform.forward;

            if (m_AttackAiming == null)
            {
                dir = Vector3.Slerp(new Vector3(dir.x, 0, dir.z).normalized, dir.normalized, Mathf.Abs(m_Verticality) * m_VerticalityFactor);
                dir.Normalize();
            }
            else
            {
                dir = m_AttackAiming.GetDirection(originPos, m_MaxRange);
            }

            if (m_MuzzleInstantiator)
                m_MuzzleInstantiator.Instantiate();

            if (m_ShellKickInstantiator)
                Invoke(nameof(KickShell), m_ShellKickDelay);

            float maxRange = GetNonObstructedMaxRange(originPos, dir);

            Vector3 point1 = originPos - originSocket.transform.right * m_Width * 0.5f;
            Vector3 point2 = originPos + originSocket.transform.right * m_Width * 0.5f;
            
#if UNITY_EDITOR
            // Crude representation of the sweep covered area
            Vector3 destinyPos = originPos + (dir * (m_MaxRange - m_CastRadius));
            Vector3 point1D = destinyPos - originSocket.transform.right * m_Width * 0.5f;
            Vector3 point2D = destinyPos + originSocket.transform.right * m_Width * 0.5f;

            Vector3 vertOffset = originSocket.transform.up * m_CastRadius;
            RuntimeDebug.DrawLine(point1+ vertOffset, point2 + vertOffset, Color.red, 10f, k_DebugCategory);
            RuntimeDebug.DrawLine(point1+ vertOffset, point1D + vertOffset, Color.red, 10f, k_DebugCategory);
            RuntimeDebug.DrawLine(point2+ vertOffset, point2D + vertOffset, Color.red, 10f, k_DebugCategory);
            RuntimeDebug.DrawLine(point1D + vertOffset, point2D + vertOffset, Color.red, 10f, k_DebugCategory);

            RuntimeDebug.DrawLine(point1 - vertOffset, point2 - vertOffset, Color.red, 10f, k_DebugCategory);
            RuntimeDebug.DrawLine(point1 - vertOffset, point1D - vertOffset, Color.red, 10f, k_DebugCategory);
            RuntimeDebug.DrawLine(point2 - vertOffset, point2D - vertOffset, Color.red, 10f, k_DebugCategory);
            RuntimeDebug.DrawLine(point1D - vertOffset, point2D - vertOffset, Color.red, 10f, k_DebugCategory);
#endif

            int hits = Physics.CapsuleCastNonAlloc(point1, point2, m_CastRadius, dir, m_HitResults, maxRange, m_LayerMask, QueryTriggerInteraction.Collide);
            if (hits > 0)
            {
                GetAndSortDamageables(hits, originPos, ref m_SortedHits);

                int hitCount = 0;
                foreach (var hit in m_SortedHits)
                {
                    ShotHit hitCopy = hit;
                    if (hitCopy.Damageable == null)
                    {
                        continue; // Skip if the Damageable is null.
                    }

                    Combatant combatant = hitCopy.Damageable.GetComponentInParent<Combatant>();
                    if (combatant != null)
                    {
                        if (m_HitCombatants.Contains(combatant))
                        {
                            continue; // Skip if Combatant already processed.
                        }

                        m_HitCombatants.Add(combatant); // Add Combatant to hit set.

                        if (RuntimeDebug.IsCategoryEnabled(k_DebugCategory))
                            Debug.Log($"Combatant: {combatant.name}, Damageable: {hitCopy.Damageable.name}, Priority: {hitCopy.Damageable.Priority}");      

                    }

                    if (hitCopy.ImpactPoint == Vector3.zero) // Refine the impact point since colliders were overlapping from the start and couldn't be defined
                    {
                        RefineImpactPos(ref hitCopy, originPos, dir, maxRange);
                    }

                    Process(new AttackInfo()
                    {
                        Attack = this,
                        Damageable = hitCopy.Damageable,
                        ImpactDir = -hitCopy.ImpactNormal,
                        ImpactPoint = hitCopy.ImpactPoint
                    });

                    RuntimeDebug.DrawWireCollider(hit.Collider, Color.red, 60, k_DebugCategory);

                    ++hitCount;
                    if (hitCount >= m_PenetrationHits)
                        break;
                }

                m_HitCombatants.Clear(); // Clear the set after processing.
            }

            ReloadableWeaponData reloadable = m_WeaponData as ReloadableWeaponData;
            if (reloadable.ShotSound)
                m_AudioSource.PlayOneShot(reloadable.ShotSound);

            StopAttack();
        }

        // --------------------------------------------------------------------

        private void RefineImpactPos(ref ShotHit hit, Vector3 origin, Vector3 dir, float maxRange)
        {
            Collider collider = hit.Damageable.GetComponent<Collider>();
            if (collider)
            {
                RaycastHit refineHit;
                if (collider.Raycast(new Ray(origin, dir), out refineHit, maxRange))
                {
                    hit.ImpactPoint = refineHit.point;
                }
                else
                {
                    hit.ImpactPoint = hit.Damageable.transform.position;
                }
            }
        }

        // --------------------------------------------------------------------

        private float GetNonObstructedMaxRange(Vector3 originPos, Vector3 dir)
        {
            float maxRange = m_MaxRange;

            RaycastHit hit;
            if (Physics.Raycast(originPos, dir, out hit, m_MaxRange, m_ObstructionLayerMask)) maxRange = Mathf.Min(maxRange, hit.distance);
            if (Physics.Raycast(originPos + Vector3.up * m_CastRadius,  dir, out hit, m_MaxRange, m_ObstructionLayerMask)) maxRange = Mathf.Min(maxRange, hit.distance);
            if (Physics.Raycast(originPos + Vector3.down * m_CastRadius, dir, out hit, m_MaxRange, m_ObstructionLayerMask)) maxRange = Mathf.Min(maxRange, hit.distance);
            if (Physics.Raycast(originPos + Vector3.left * m_CastRadius, dir, out hit, m_MaxRange, m_ObstructionLayerMask)) maxRange = Mathf.Min(maxRange, hit.distance);
            if (Physics.Raycast(originPos + Vector3.right * m_CastRadius, dir, out hit, m_MaxRange, m_ObstructionLayerMask)) maxRange = Mathf.Min(maxRange, hit.distance);

            return Mathf.Min(maxRange, m_MaxRange);
        }

        // --------------------------------------------------------------------

        private void KickShell()
        {
            m_ShellKickInstantiator.Instantiate();
        }

        // --------------------------------------------------------------------

        private void GetAndSortDamageables(int count, Vector3 originPos, ref List<ShotHit> outHits)
        {
            outHits.Clear();

            for (int i = 0; i < count; ++i)
            {
                if (m_ShowDebug)
                    DebugUtils.DrawCollider(m_HitResults[i].collider, Color.yellow, 60);

                Damageable damageable = m_HitResults[i].collider.GetComponent<Damageable>();
                AttackImpact impact = damageable ? m_Attack.GetImpact(damageable.Type) : null;

                if (impact != null && impact.Damage > 0.0f)
                {
                    Health health = damageable.GetComponentInParent<Health>();
                    if (health && (health.IsDead || health.Invulnerable))
                        continue;

                    float dist = m_HitResults[i].distance;
                    ShotHit hit = new ShotHit()
                    {
                        Damageable = damageable,
                        ImpactPoint = m_HitResults[i].point,
                        ImpactNormal = m_HitResults[i].normal,
                        Collider = m_HitResults[i].collider
                    };

                    bool inserted = false;
                    for (int j = 0; j < outHits.Count; j++)
                    {
                        if (hit.Damageable.Priority > outHits[j].Damageable.Priority)
                        {
                            outHits.Insert(j, hit);
                            inserted = true;
                            break;
                        }
                        else if (hit.Damageable.Priority == outHits[j].Damageable.Priority)
                        {
                            if (dist < Vector3.Distance(originPos, outHits[j].ImpactPoint))
                            {
                                outHits.Insert(j, hit);
                                inserted = true;
                                break;
                            }
                        }
                    }

                    if (!inserted)
                    {
                        outHits.Add(hit);
                    }
                }
            }
        }

        // --------------------------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            m_SocketCtrl = GetComponent<SocketController>();
            if (m_SocketCtrl)
            {
                Gizmos.color = Color.yellow;
                Socket originSocket = m_SocketCtrl.GetSocket(m_AttackOriginSocket);

                Vector3 originPos = originSocket.transform.position + originSocket.transform.forward * m_ForwardOffset;
                Vector3 destinyPos = originPos + (originSocket.transform.forward * (m_MaxRange - m_CastRadius));
                
                Gizmos.color = Color.red;
                Vector3 point1 = originPos - originSocket.transform.right * m_Width * 0.5f;
                Vector3 point2 = originPos + originSocket.transform.right * m_Width * 0.5f;
                Gizmos.DrawWireSphere(point1, m_CastRadius);
                Gizmos.DrawWireSphere(point2, m_CastRadius);

                Vector3 point1D = destinyPos - originSocket.transform.right * m_Width * 0.5f;
                Vector3 point2D = destinyPos + originSocket.transform.right * m_Width * 0.5f;
                Gizmos.DrawWireSphere(point1D, m_CastRadius);
                Gizmos.DrawWireSphere(point2D, m_CastRadius);

                Gizmos.DrawLine(point1 + originSocket.transform.up * m_CastRadius, point2 + originSocket.transform.up * m_CastRadius);
                Gizmos.DrawLine(point1 - originSocket.transform.up * m_CastRadius, point2 - originSocket.transform.up * m_CastRadius);
                Gizmos.DrawLine(point1D + originSocket.transform.up * m_CastRadius, point2D + originSocket.transform.up * m_CastRadius);
                Gizmos.DrawLine(point1D - originSocket.transform.up * m_CastRadius, point2D - originSocket.transform.up * m_CastRadius);
                Gizmos.DrawLine(point1 - originSocket.transform.right * m_CastRadius, point1D - originSocket.transform.right * m_CastRadius);
                Gizmos.DrawLine(point2 + originSocket.transform.right * m_CastRadius, point2D + originSocket.transform.right * m_CastRadius);
                Gizmos.DrawLine(point1 + originSocket.transform.up * m_CastRadius, point1D + originSocket.transform.up * m_CastRadius);
                Gizmos.DrawLine(point2 + originSocket.transform.up * m_CastRadius, point2D + originSocket.transform.up * m_CastRadius);
                Gizmos.DrawLine(point1 - originSocket.transform.up * m_CastRadius, point1D - originSocket.transform.up * m_CastRadius);
                Gizmos.DrawLine(point2 - originSocket.transform.up * m_CastRadius, point2D - originSocket.transform.up * m_CastRadius);
                Gizmos.DrawLine(point1 - originSocket.transform.forward * m_CastRadius, point2 - originSocket.transform.forward * m_CastRadius);
                Gizmos.DrawLine(point1D + originSocket.transform.forward * m_CastRadius, point2D + originSocket.transform.forward * m_CastRadius);
            }
        }
#endif

    }
}
