using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class EnemySensesController : SenseController
    {
        [Tooltip("This event will be triggered if the enemy detected by sight or proximity and is alive")]
        public UnityEvent OnPlayerDetected;
        [Tooltip("This event will be triggered if the enemy is no longer detected by sight or proximity or is dead")]
        public UnityEvent OnPlayerLost;
        [Tooltip("This event will be triggered if the enemy is detected AND reachable")]
        public UnityEvent OnPlayerReacheable;
        [Tooltip("This event will be triggered if the enemy is no longer reachable")]
        public UnityEvent OnPlayerUnreachable;
        
        [SerializeField] bool m_ShowDebug;

        
        public Transform PlayerTransform { get; private set; }
        public bool IsPlayerDetected { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public bool IsPlayerInSight { get; private set; }
        public bool IsPlayerInProximity { get; private set; }
        public bool IsPlayerInReach { get; private set; }
        public bool IsPlayerAlive { get; private set; }
        public bool IsEnemyDamaged { get; private set; }
        public bool IsPlayerGrabbed { get; private set; }

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            IsPlayerAlive = true; // This is assumed true by default
        }

        // --------------------------------------------------------------------

        private void OnGUI()
        {
            if (m_ShowDebug)
            {
                GUILayout.BeginArea(new Rect(10, 10, 300, Screen.height));
                GUILayout.BeginVertical();
                GUILayout.Label($"Enemy Senses:");
                GUILayout.Label("-----------------");
                GUILayout.Label($"Sight: {IsPlayerInSight}");
                GUILayout.Label($"Proximity: {IsPlayerInProximity}");
                GUILayout.Label($"Reachable: {IsPlayerInReach}");
                GUILayout.Label($"Damaged: {IsEnemyDamaged}");
                GUILayout.Label($"Grabbed: {IsPlayerGrabbed}");
                GUILayout.Label("-----------------");
                GUILayout.Label($"Detected: {IsPlayerDetected}");
                GUILayout.Label($"Last known position: {LastKnownPosition}");
                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            if (IsPlayerDetected)
            {
                if (PlayerTransform)
                    LastKnownPosition = PlayerTransform.position;
                else
                    IsPlayerDetected = false;
            }
        }

        // --------------------------------------------------------------------

        protected override void OnSenseChangedCallback(Sense sense, Transform detectedTransform)
        {
            bool wasInReach = IsPlayerInReach;

            if (detectedTransform && PlayerTransform == null) // This needs some way of sorting targets
                PlayerTransform = detectedTransform;
            else if (!detectedTransform)
                return;

            if (sense is SenseSight)
            {
                IsPlayerInSight = sense.SuccessfullySensed();
            }
            else if (sense is SenseDamage)
            {
                IsEnemyDamaged = sense.SuccessfullySensed();
            }
            else if (sense is SenseReachability)
            {
                IsPlayerInReach = sense.SuccessfullySensed();
            }
            else if (sense is SenseProximity)
            {
                IsPlayerInProximity = sense.SuccessfullySensed();
            }
            else if (sense is SenseVitality)
            {
                IsPlayerAlive = sense.SuccessfullySensed();
            }
            else if (sense is SenseGrabbed)
            {
                IsPlayerGrabbed = sense.SuccessfullySensed();
            }

            bool wasDetected = IsPlayerDetected;
            IsPlayerDetected = IsPlayerAlive && (IsPlayerInProximity || IsPlayerInSight || IsEnemyDamaged);
            if (IsPlayerDetected)
            {
                LastKnownPosition = PlayerTransform.position;
                OnPlayerDetected?.Invoke();

                if (IsPlayerInReach)
                {
                    OnPlayerReacheable?.Invoke();
                }
            }
            else if (wasDetected)
            {
                PlayerTransform = null;
                OnPlayerLost?.Invoke();
            }

            if (wasInReach && !IsPlayerInReach)
            {
                OnPlayerUnreachable?.Invoke();
            }
        }


    }
}
