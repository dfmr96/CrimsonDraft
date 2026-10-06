using System;
using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(PooledGameObject))]
    [RequireComponent(typeof(ParticleSystem))]
    public class AutoPoolParticleSystem : MonoBehaviour
    {
        // If true, deactivate the object instead of destroying it
        public bool OnlyDeactivate;

        public float CheckInterval = 0.5f;

        private ParticleSystem mParticleSystem;
        private PooledGameObject mPoolable;
        private Action mCheckAliveAction;

        // --------------------------------------------------------------------

        private void Awake()
        {
            mCheckAliveAction = CheckIfAlive;
            mPoolable = GetComponent<PooledGameObject>();
            mParticleSystem = GetComponent<ParticleSystem>();
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            this.InvokeActionRepeating(mCheckAliveAction, CheckInterval);
        }

        // --------------------------------------------------------------------

        private void CheckIfAlive()
        {
            if (!mParticleSystem.IsAlive(true))
            {
                if (OnlyDeactivate)
                {
#if UNITY_3_5
				this.gameObject.SetActiveRecursively(false);
#else
                    this.gameObject.SetActive(false);
#endif
                }
                else if (mPoolable)
                {
                    mPoolable.ReturnToPool();
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}