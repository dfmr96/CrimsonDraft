using UnityEngine;

namespace HorrorEngine
{
    public abstract class Sense : MonoBehaviour
    {
        public SenseChangedEvent OnChanged = new SenseChangedEvent();

        public float TickFrequency = 0.5f;
        
        protected SenseController m_Controller;

        public virtual void Init(SenseController controller)
        {
            m_Controller = GetComponentInParent<SenseController>();
        }

        public abstract void Tick();

        public abstract bool SuccessfullySensed();
    }
}