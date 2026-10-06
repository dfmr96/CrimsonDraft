using UnityEngine;

namespace HorrorEngine
{
    public class ColliderObserverObjectFilter : ColliderObserverFilter
    {
        [SerializeField] GameObject[] m_Objects;

        public override bool Passes(Collider other)
        {
            foreach (var obj in m_Objects)
            {
                if (obj == other.gameObject)
                    return true;
            }

            return false;
        }
    }
}