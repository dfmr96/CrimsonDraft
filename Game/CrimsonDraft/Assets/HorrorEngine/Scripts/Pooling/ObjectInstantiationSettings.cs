using UnityEngine;
using System;

namespace HorrorEngine
{
    [Serializable]
    public class ObjectInstantiationSettings
    {
        public Transform Parent;
        public SocketHandle Socket;
        public Vector3 Position;
        public Vector3 Rotation;
        public float Scale = 1f;
        public bool IsLocal = true;
        public bool InheritsRotation;
        public bool DetachFromParent;

        public GameObject Instantiate(GameObject prefab, SocketController socketCtrl = null)
        {
            if (!prefab)
                return null;

            Debug.Assert(!(Socket && Parent), "Socket and Parent properties are exclusive, Socket will be set as the new parent for the object");
            Debug.Assert(socketCtrl || !Socket, "Socket has been provided but SocketController was null");

            if (Socket)
            {
                Parent = socketCtrl.GetSocket(Socket).transform;
                Socket = null; // Clear socket to prevent future lookups
            }

            GameObject newObj;
            Transform parent = null;
            if (GameObjectPool.Exists)
            {
                newObj = GameObjectPool.Instance.GetFromPool(prefab, Parent).gameObject;
                parent = GameObjectPool.Instance.transform;
            }
            else
            {
                newObj = GameObject.Instantiate(prefab, Parent);
            }

            if (Parent)
            {
                if (IsLocal)
                {
                    newObj.transform.localPosition = Position;
                    newObj.transform.localRotation = Quaternion.Euler(Rotation);
                }
                else
                {
                    newObj.transform.position = Position;
                    newObj.transform.rotation = Quaternion.Euler(Rotation);
                }

                
                if (InheritsRotation)
                    newObj.transform.rotation = Parent.transform.rotation;

                newObj.transform.localScale = Vector3.one * Scale;
                if (DetachFromParent)
                {
                    newObj.transform.SetParent(parent);
                    newObj.transform.localScale = prefab.transform.localScale * Scale;
                }
            }
            else
            {
                newObj.transform.position = Position;
                newObj.transform.rotation = Quaternion.Euler(Rotation);
                newObj.transform.localScale = Vector3.one * Scale;
            }

            newObj.gameObject.SetActive(true);
            return newObj;
        }
    }
}