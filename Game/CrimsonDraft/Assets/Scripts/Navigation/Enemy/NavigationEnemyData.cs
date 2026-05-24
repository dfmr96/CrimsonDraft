#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Enemy
{
    [CreateAssetMenu(fileName = "NavigationEnemyData", menuName = "CrimsonDraft/Navigation Enemy Data")]
    public sealed class NavigationEnemyData : ScriptableObject
    {
        [Header("Combat")]
        public string encounterId = string.Empty;

        [Header("Movement")]
        public float patrolSpeed          = 2.0f;
        public float chaseSpeed           = 3.5f;
        public float waypointStopDistance = 0.3f;
        public float catchRadius          = 0.8f;

        [Header("Proximity Detection")]
        public float detectRadius   = 1.8f;
        public float undetectRadius = 2.4f;

        [Header("Sound Detection")]
        public float playerDeadzone     = 0.1f;
        public float playerRunThreshold = 5.5f;
        public float walkSoundRadius    = 3.5f;
        public float runSoundRadius     = 9.0f;

        [Header("Visual Detection")]
        public float     visualRange     = 7.0f;
        public float     visualFov       = 110f;
        public LayerMask obstructionMask;
        public LayerMask targetMask;

        [Header("Suspicious State")]
        public bool  suspiciousEnabled  = false;
        public float suspiciousDuration = 2.0f;
    }
}
