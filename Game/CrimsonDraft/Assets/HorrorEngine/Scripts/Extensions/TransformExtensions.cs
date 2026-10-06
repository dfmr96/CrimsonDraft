using UnityEngine;

namespace HorrorEngine
{
    public static class TransformExtensions
    {
        public static void DestroyAllChildren(this Transform transform)
        {

            for (int i = transform.childCount - 1; i >= 0; --i)
            {
                GameObject child = transform.GetChild(i).gameObject;
                var poolable = child.GetComponent<PooledGameObject>();
                if (poolable)
                    poolable.ReturnToPool(true);
                else
                    GameObject.Destroy(child);
            }
        }

    }
}