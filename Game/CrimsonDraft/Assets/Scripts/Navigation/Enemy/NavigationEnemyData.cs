#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Enemy
{
    [CreateAssetMenu(fileName = "NavigationEnemyData", menuName = "CrimsonDraft/Navigation Enemy Data")]
    public sealed class NavigationEnemyData : ScriptableObject
    {
        [Header("Alerted Movement")]
        [Tooltip("Velocidad del NavMeshAgent mientras persigue en Alerted.")]
        public float chaseSpeed = 3.5f;
        [Tooltip("Velocidad de giro en grados/segundo al encarar al jugador antes de avanzar.")]
        public float turnSpeed = 120f;
        [Tooltip("Ángulo (grados) respecto al jugador por encima del cual el enemigo se detiene a girar en el lugar en vez de avanzar.")]
        public float turnInPlaceThreshold = 35f;

        [Header("Attack")]
        [Tooltip("Distancia a la que el enemigo entra en el estado Attack.")]
        public float attackRange = 1.3f;
        [Tooltip("Margen extra sobre attackRange antes de volver a Alerted (evita flickering en el borde).")]
        public float attackRangeBuffer = 0.3f;

        [Header("Sound Detection")]
        [Tooltip("Velocidad mínima del Rigidbody del jugador para producir sonido. Por debajo de este valor se considera en reposo.")]
        public float playerDeadzone     = 0.1f;
        [Tooltip("Velocidad del Rigidbody del jugador a partir de la cual se usa runSoundRadius en lugar de walkSoundRadius.")]
        public float playerRunThreshold = 5.5f;
        [Tooltip("Radio de detección por sonido cuando el jugador camina (velocidad entre deadzone y runThreshold).")]
        public float walkSoundRadius    = 3.5f;
        [Tooltip("Radio de detección por sonido cuando el jugador corre (velocidad mayor que runThreshold).")]
        public float runSoundRadius     = 9.0f;

        [Header("Visual Detection")]
        [Tooltip("Distancia máxima del cono de visión.")]
        public float     visualRange     = 7.0f;
        [Tooltip("Ángulo total del cono de visión en grados (se divide simétricamente respecto al forward del enemigo).")]
        public float     visualFov       = 110f;
        [Tooltip("Capas que bloquean la línea de visión (paredes, obstáculos). Si está vacío, la LoS nunca se bloquea.")]
        public LayerMask obstructionMask;
        [Tooltip("Capa del jugador. El raycast de visión debe impactar en esta capa para confirmar la detección. Si está vacío, la detección visual nunca se activa.")]
        public LayerMask targetMask;
    }
}