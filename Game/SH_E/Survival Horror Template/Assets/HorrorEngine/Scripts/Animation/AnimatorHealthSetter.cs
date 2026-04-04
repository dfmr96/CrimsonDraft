using UnityEngine;

namespace HorrorEngine
{
    public class AnimatorHealthSetter : AnimatorFloatSetter
    {
        [SerializeField] private bool m_Normalized;

        private Health m_Health;

        private float Value => m_Health ? (m_Normalized ? m_Health.Normalized : m_Health.Value) : 0f;

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_Health = GetComponentInParent<Health>();
        }


        // --------------------------------------------------------------------

        protected override void OnEnable()
        {
            Set(Value, true);
        }

        // --------------------------------------------------------------------

        public override void OnReset()
        {
            base.OnReset();
            Set(Value, true);
        }

        // --------------------------------------------------------------------

        void Update()
        {
            Set(Value);
        }
    }
}