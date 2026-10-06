using UnityEngine;

namespace HorrorEngine
{
    
    public class ThrowTrigger : MonoBehaviour
    {
        public Vector3 VelocityInLocalSpace;

        // Usually called from AttackMontage anim events
        public void InstantiateAndThrow(Throwable throwable)
        {
            var pooledThrowable = GameObjectPool.Instance.GetFromPool(throwable.gameObject);
            pooledThrowable.transform.SetPositionAndRotation(transform.position, transform.rotation);

            Vector3 worldVelocity = transform.rotation * VelocityInLocalSpace;

            pooledThrowable.gameObject.SetActive(true);
            pooledThrowable.GetComponent<Throwable>().Throw(worldVelocity);
        }
    }
}