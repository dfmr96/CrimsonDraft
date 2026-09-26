#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Enemy
{
    public sealed class EnemyAttackHitbox : MonoBehaviour
    {
        [SerializeField] private EnemyNavAgent owner = null!;
        [Tooltip("Collider trigger que representa el golpe. Si se deja vacío, se busca en este GameObject.")]
        [SerializeField] private Collider hitCollider = null!;

        private float minDistanceThisSwing = float.MaxValue;
        private Transform? playerT;
        private bool wasAttacking;
        private float attackStartTime;

        private void Awake()
        {
            if (hitCollider == null)
                hitCollider = GetComponent<Collider>();
            Debug.Log($"[EnemyAttackHitbox] Awake on '{gameObject.name}' — hitCollider={(hitCollider == null ? "NULL" : hitCollider.name)}");
            if (hitCollider != null) hitCollider.enabled = false;
        }

        // Unity solo entrega Animation Events al GameObject que tiene el Animator (la raíz),
        // nunca a hijos como este (colgado de Hand.R) — por eso no expone métodos con el
        // nombre del evento; EnemyAnimationReactor los recibe y llama a estos.
        public void Open()
        {
            if (playerT == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerT = player.transform;
            }
            var dNow = playerT != null ? Vector3.Distance(transform.position, playerT.position) : -1f;
            var sphere = hitCollider as SphereCollider;
            var worldRadius = sphere != null ? sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) : -1f;
            Debug.Log($"[EnemyAttackHitbox] Open() on '{gameObject.name}' — worldPos={transform.position}, localRadius={sphere?.radius}, worldRadius={worldRadius:F3}, distanceToPlayerNow={dNow:F3}");
            if (hitCollider != null) hitCollider.enabled = true;
        }

        public void Close()
        {
            Debug.Log($"[EnemyAttackHitbox] Close() on '{gameObject.name}' — closestDistanceToPlayerDuringSwing={minDistanceThisSwing}");
            if (hitCollider != null) hitCollider.enabled = false;
        }

        private void Update()
        {
            if (playerT == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerT = player.transform;
            }

            var isAttacking = owner != null && owner.State == CrimsonDraft.Infrastructure.Events.EnemyAlertState.Attack;

            if (isAttacking && !wasAttacking)
            {
                attackStartTime = Time.time;
                minDistanceThisSwing = float.MaxValue;
            }

            if (isAttacking && playerT != null)
            {
                var d = Vector3.Distance(transform.position, playerT.position);
                if (d < minDistanceThisSwing) minDistanceThisSwing = d;
                Debug.Log($"[EnemyAttackHitbox] t={Time.time - attackStartTime:F3}s dist={d:F3} colliderEnabled={hitCollider != null && hitCollider.enabled} handPos={transform.position}");
            }

            if (!isAttacking && wasAttacking)
                Debug.Log($"[EnemyAttackHitbox] Attack ended — closest distance reached this swing: {minDistanceThisSwing:F3}");

            wasAttacking = isAttacking;
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[EnemyAttackHitbox] OnTriggerEnter with '{other.name}' (tag={other.tag}) — enabled={hitCollider != null && hitCollider.enabled}");
            if (!other.CompareTag("Player")) return;
            Debug.Log("[EnemyAttackHitbox] HIT confirmed on Player — calling NotifyAttackHit()");
            if (hitCollider != null) hitCollider.enabled = false; // un golpe por swing
            owner.NotifyAttackHit();
        }

        // DEBUG temporal: dibuja el collider del golpe (rojo) y el del player (cian) para
        // comparar tamaño/posición a ojo mientras se diagnostica por qué no conecta.
        private void OnDrawGizmos()
        {
            if (hitCollider == null) hitCollider = GetComponent<Collider>();
            if (hitCollider != null)
            {
                Gizmos.color = hitCollider.enabled ? Color.red : new Color(1f, 0f, 0f, 0.3f);
                if (hitCollider is SphereCollider sphere)
                {
                    var worldCenter = hitCollider.transform.TransformPoint(sphere.center);
                    var worldRadius = sphere.radius * Mathf.Max(hitCollider.transform.lossyScale.x, hitCollider.transform.lossyScale.y, hitCollider.transform.lossyScale.z);
                    Gizmos.DrawWireSphere(worldCenter, worldRadius);
                }
                else
                {
                    Gizmos.DrawWireCube(hitCollider.bounds.center, hitCollider.bounds.size);
                }
            }

            if (playerT == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerT = player.transform;
            }
            if (playerT != null)
            {
                var playerCollider = playerT.GetComponentInChildren<Collider>();
                if (playerCollider != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireCube(playerCollider.bounds.center, playerCollider.bounds.size);
                }
            }
        }
    }
}