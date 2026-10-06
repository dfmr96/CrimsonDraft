using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class Actor : MonoBehaviour
    {
        public ActorHandle Handle;
        public Animator MainAnimator;
        public ActorStateController StateController { get; private set; }

        public UnityEvent OnTeleported;

        private HashSet<Object> m_DisableContext = new HashSet<Object>();

        private IDeactivateWithActor[] m_DeactivableCompontents;

        public bool IsDisabled => m_DisableContext.Count > 0;

        // --------------------------------------------------------------------

        protected void Awake()
        {
            StateController = GetComponent<ActorStateController>();

            m_DeactivableCompontents = GetComponentsInChildren<IDeactivateWithActor>();
        }

        // --------------------------------------------------------------------

        public void Disable(Object context)
        {
            bool wasDisabled = IsDisabled;
            if (!m_DisableContext.Contains(context))
                m_DisableContext.Add(context);

            if (!wasDisabled && IsDisabled)
            {
                foreach (IDeactivateWithActor deactivable in m_DeactivableCompontents)
                {
                    ((MonoBehaviour)deactivable).enabled = false;
                }
            }
        }

        // --------------------------------------------------------------------

        public void Enable(Object context)
        {
            bool wasDisabled = IsDisabled;
            m_DisableContext.Remove(context);

            if (wasDisabled && !IsDisabled)
            {
                foreach (IDeactivateWithActor deactivable in m_DeactivableCompontents)
                {
                    ((MonoBehaviour)deactivable).enabled = true;
                }
            }
        }

        // --------------------------------------------------------------------

        public void PlaceAt(Transform point)
        {
            PlaceAt(point.position, point.rotation);
        }

        // --------------------------------------------------------------------

        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            transform.SetLocalPositionAndRotation(position, rotation);
            OnTeleported?.Invoke();
        }

    }
}