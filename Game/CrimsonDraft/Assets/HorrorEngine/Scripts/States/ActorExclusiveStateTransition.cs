using UnityEngine;

namespace HorrorEngine
{
    public class ActorExclusiveStateTransition : MonoBehaviour
    {
        [SerializeField] private ActorStateTransition[] m_Transitions;

#if UNITY_EDITOR
        [ContextMenu("Find In Children")]
        private void FindInChildren()
        {
            m_Transitions = GetComponentsInChildren<ActorStateTransition>();
        }
#endif

        public void Trigger()
        {
            foreach(var transition in m_Transitions)
            {
                if (transition.CanTrigger())
                {
                    transition.ForceTrigger();
                    return;
                }
            }
        }
    }
}