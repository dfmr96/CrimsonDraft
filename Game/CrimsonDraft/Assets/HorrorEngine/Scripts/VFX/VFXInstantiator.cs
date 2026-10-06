using UnityEngine;

namespace HorrorEngine
{
    public class VFXInstantiator : MonoBehaviour
    {
        [SerializeField] GameObject VFXPrefab;

        public void InstantiateVFX()
        {
            var pooledVFX = GameObjectPool.Instance.GetFromPool(VFXPrefab, null);
            pooledVFX.transform.SetPositionAndRotation(transform.position, transform.rotation);
            pooledVFX.gameObject.SetActive(true);
        }
    }
}