#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Enemy
{
    [CreateAssetMenu(fileName = "NavigationEnemyData", menuName = "CrimsonDraft/Navigation Enemy Data")]
    public sealed class NavigationEnemyData : ScriptableObject
    {
        [Header("Movement")]
        [Tooltip("Velocidad del NavMeshAgent durante el estado Patrol.")]
        public float patrolSpeed          = 2.0f;
        [Tooltip("Velocidad del NavMeshAgent durante el estado Alert (persecución).")]
        public float chaseSpeed           = 3.5f;
        [Tooltip("Distancia al waypoint a la que se considera alcanzado y se avanza al siguiente.")]
        public float waypointStopDistance = 0.3f;
        [Tooltip("Distancia al jugador a la que se dispara el combate.")]
        public float catchRadius          = 0.8f;

        [Header("Proximity Detection")]
        [Tooltip("Radio omnidireccional de activación. El enemigo detecta al jugador si entra a esta distancia.")]
        public float detectRadius   = 1.8f;
        [Tooltip("Radio de desactivación (debe ser mayor que detectRadius). El enemigo deja de detectar por proximidad solo cuando el jugador supera esta distancia, evitando flickering en el borde.")]
        public float undetectRadius = 2.4f;

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

        [Header("Suspicious State")]
        [Tooltip("Si está activo, al detectar al jugador el enemigo entra en Suspicious antes de pasar a Alert. Si está inactivo, pasa directamente a Alert.")]
        public bool  suspiciousEnabled  = false;
        [Tooltip("Duración en segundos del estado Suspicious. Si el jugador no se confirma en este tiempo, el enemigo vuelve a Patrol.")]
        public float suspiciousDuration = 2.0f;
    }
}
