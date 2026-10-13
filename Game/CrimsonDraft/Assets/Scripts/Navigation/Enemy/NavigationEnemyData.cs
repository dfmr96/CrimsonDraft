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

        [Header("Attack - Agarre y mordisco")]
        [Tooltip("Segundos desde que arranca el agarre hasta que la hitbox empieza a chequear si el jugador está adentro.")]
        public float grabDelay = 0.15f;
        [Tooltip("Segundos que la hitbox de agarre queda abierta. Si el jugador no entra en ese tiempo, el agarre falla y vuelve a perseguir.")]
        public float grabWindow = 0.5f;
        [Tooltip("Segundos tras un agarre fallido antes de poder intentar otro.")]
        public float grabCooldown = 3f;
        [Tooltip("Segundos que sostiene al jugador antes de morder.")]
        public float grabHoldDuration = 1f;
        [Tooltip("Variación aleatoria (+/-) sobre grabHoldDuration.")]
        public float grabHoldRandomOffset = 0.25f;
        [Tooltip("Distancia horizontal a la que el enemigo se acomoda respecto del jugador mientras lo sostiene.")]
        public float grabDistance = 0.6f;
        [Tooltip("Velocidad (m/s) con la que el enemigo se acomoda a grabDistance mientras lo sostiene.")]
        public float grabMoveSpeed = 2.5f;

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