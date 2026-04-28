using System;
using UnityEngine;

namespace HorrorEngine
{
    [DisallowMultipleComponent]
    public class PooledGameObject : MonoBehaviour
    {
        [HideInInspector]
        public bool IsInPool;

        public GameObjectPool Owner;
        public Action OnReturnedToPool;


        public void ReturnToPool(bool attachToPool = true)
        {
            if (!IsInPool && Owner)
            {
                Owner.ReturnToPool(this, attachToPool);
                OnReturnedToPool?.Invoke();
            }
            else
            {
                Debug.LogError("Can't return to pool " + (Owner == null ? "(No owner)" : "") + (IsInPool ? "(Already in pool)" : ""), gameObject);
            }
        }
    }
}