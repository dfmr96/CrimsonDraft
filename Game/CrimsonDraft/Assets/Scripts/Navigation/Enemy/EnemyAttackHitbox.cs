#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Enemy
{
    /// <summary>
    /// Caja de agarre frente al pecho del enemigo (equivalente al HitBox del GrabFrontAttack del
    /// Survival Horror Template). EnemyNavAgent la consulta mientras dura la ventana de agarre.
    /// Es un overlap por consulta, no un trigger: no depende de que un collider se habilite justo
    /// en el frame correcto ni de la matriz de colisiones entre capas.
    /// </summary>
    public sealed class EnemyAttackHitbox : MonoBehaviour
    {
        [Tooltip("Centro de la caja en espacio local del enemigo.")]
        [SerializeField] private Vector3 center = new(0f, 1.3f, 0.5f);
        [Tooltip("Tamaño de la caja en espacio local del enemigo.")]
        [SerializeField] private Vector3 size = new(0.45f, 0.9f, 0.45f);
        [SerializeField] private LayerMask mask = ~0;

        private readonly Collider[] hits = new Collider[8];

        public bool OverlapsPlayer()
        {
            var halfExtents = Vector3.Scale(this.size, transform.lossyScale) * 0.5f;
            var count = Physics.OverlapBoxNonAlloc(
                transform.TransformPoint(this.center), halfExtents, this.hits, transform.rotation,
                this.mask, QueryTriggerInteraction.Ignore);

            for (var i = 0; i < count; i++)
            {
                var hit = this.hits[i];
                if (hit.CompareTag("Player")) return true;
                if (hit.attachedRigidbody != null && hit.attachedRigidbody.CompareTag("Player")) return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color  = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(this.center, this.size);
        }
#endif
    }
}
