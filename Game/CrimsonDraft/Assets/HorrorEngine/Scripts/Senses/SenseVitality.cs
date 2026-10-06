using UnityEngine;

namespace HorrorEngine
{
    public class SenseVitality : Sense
    {
        [SerializeField] SenseTarget m_Target;

        private bool m_IsAlive;

        public override bool SuccessfullySensed()
        {
            return m_IsAlive;
        }

        public override void Tick()
        {
            var target = m_Target.GetTransform();
            bool wasAlive = m_IsAlive;

            m_IsAlive = target ? target.GetComponent<Health>().Value > 0 : false;

            if (wasAlive != m_IsAlive)
                OnChanged?.Invoke(this, target);
        }
    }
}