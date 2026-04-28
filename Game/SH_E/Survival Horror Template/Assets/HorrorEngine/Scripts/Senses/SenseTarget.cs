using UnityEngine;

namespace HorrorEngine
{
    public class SenseTarget : MonoBehaviour
    {
        public virtual Transform GetTransform()
        {
            return transform;
        }
    }
}