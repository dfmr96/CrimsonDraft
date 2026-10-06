using UnityEngine;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    [Serializable]
    public class GameObjectPoolEntry
    {
        public GameObject Prefab;
        public int Count;
    }

    public class GameObjectPool : SingletonBehaviour<GameObjectPool>
    {
        public List<GameObjectPoolEntry> PrepooledObjects = new List<GameObjectPoolEntry>();

        private static Dictionary<GameObject, List<PooledGameObject>> mPool = new Dictionary<GameObject, List<PooledGameObject>>();
        private GameObject mPoolRoot;

        public int PoolMisses { get; private set; }

        public void OwnScenePoolables()
        {
            PooledGameObject[] poolables = FindObjectsByType<PooledGameObject>(FindObjectsSortMode.None);
            foreach (PooledGameObject po in poolables)
                if (po.Owner == null)
                    po.Owner = this;
        }


        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            mPoolRoot = new GameObject("[PooledObjects]");
            mPoolRoot.transform.SetParent(transform);

            for (int i = 0; i < PrepooledObjects.Count; ++i)
            {
                for (int n = 0; n < PrepooledObjects[i].Count; ++n)
                    AddNewToPool(PrepooledObjects[i].Prefab);
            }

            OwnScenePoolables();
        }

        // --------------------------------------------------------------------

        public PooledGameObject GetFromPool(GameObject go, Transform parent = null)
        {
#if UNITY_EDITOR
            AssertIsValidPoolObject(go);
#endif

            go.gameObject.SetActive(false);
            PooledGameObject candidate = null;
            if (mPool.ContainsKey(go))
            {
                for (int i = 0; i < mPool[go].Count; ++i)
                {
                    if (mPool[go][i].IsInPool)
                    {
                        candidate = mPool[go][i];
                    }
                }
            }

            if (!candidate)
            {
                ++PoolMisses;
                candidate = AddNewToPool(go);
            }

            candidate.IsInPool = false;

            if (parent)
                candidate.transform.SetParent(parent);

            candidate.transform.rotation = go.transform.rotation;
            candidate.transform.localScale = go.transform.lossyScale;
            candidate.transform.localPosition = Vector3.zero;

            /*GameObjectReset reset = candidate.GetComponent<GameObjectReset>();
            if (reset)
            {
                reset.ResetComponents();
            }*/
            
            go.gameObject.SetActive(true);

            return candidate;
        }

        // --------------------------------------------------------------------

        private PooledGameObject AddNewToPool(GameObject go)
        {
            go.gameObject.SetActive(false);

#if UNITY_EDITOR
            AssertIsValidPoolObject(go);
#endif

            if (!mPool.ContainsKey(go))
                mPool.Add(go, new List<PooledGameObject>());

            GameObject newGO = Instantiate(go);
            PooledGameObject newPooled = newGO.GetComponent<PooledGameObject>();
            newPooled.Owner = this;
            newPooled.IsInPool = true;

            newGO.gameObject.SetActive(true);
            newGO.gameObject.SetActive(false);
            newGO.transform.SetParent(mPoolRoot.transform);

            mPool[go].Add(newPooled);

            go.gameObject.SetActive(true);

            return newPooled;
        }

        // --------------------------------------------------------------------
#if UNITY_EDITOR

        private void AssertIsValidPoolObject(GameObject go)
        {
            Debug.Assert(go, $"The GameObject you are trying to get from the pool \"{go.name}\" is null");
            PrefabAssetType type = PrefabUtility.GetPrefabAssetType(go);
            Debug.Assert(type == PrefabAssetType.Regular || type == PrefabAssetType.Variant, "GameObjectPool should only receive a prefab to instantiate from", gameObject);
            Debug.Assert(go.GetComponent<PooledGameObject>(), $"The GameObject you are trying to get from the pool \"{go.name}\" doesn't have a PooledGameObject component", gameObject);
        }

#endif
        // --------------------------------------------------------------------

        public void ReturnToPool(PooledGameObject pooled, bool attachToPool = true)
        {
            if (!pooled.IsInPool)
            {
                pooled.IsInPool = true;
                pooled.gameObject.SetActive(false);

                if (attachToPool && pooled.transform.parent != pooled.Owner.mPoolRoot.transform)
                    pooled.transform.SetParent(pooled.Owner.mPoolRoot.transform);
            }
        }
    }
}