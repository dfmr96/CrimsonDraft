using UnityEngine;

namespace HorrorEngine
{
    public class SenseGrabbed : Sense
    {
        [SerializeField] SenseTarget m_Target;
        
        private bool m_IsGrabbed;

        public override bool SuccessfullySensed()
        {
            return m_IsGrabbed;
        }

        public override void Tick()
        {
            var target = m_Target.GetTransform();
            bool wasGrabbed = m_IsGrabbed;

            m_IsGrabbed = target ? !target.GetComponent<PlayerGrabHandler>().CanBeGrabbed : false;

            if (wasGrabbed != m_IsGrabbed)
                OnChanged?.Invoke(this, target);
        }
    }
}