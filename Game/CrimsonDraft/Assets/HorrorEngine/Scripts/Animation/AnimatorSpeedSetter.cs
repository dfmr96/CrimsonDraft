using UnityEngine;

namespace HorrorEngine
{
    public class AnimatorFloatSetter : MonoBehaviour, IResetable
    {
        [SerializeField] protected string m_PropertyName;
        [SerializeField] protected float m_InterpolationSpeed = 1f;

        protected Animator m_Animator;
        protected int m_PropertyHash;
        private float m_CurrentValue;

        private void ResetToInit()
        {
            m_CurrentValue = 0f;

            if (!m_Animator)
                m_Animator = GetComponent<Animator>();

            m_Animator.SetFloat(m_PropertyHash, 0f);
        }

        // --------------------------------------------------------------------

        public virtual void OnReset()
        {
            ResetToInit();
        }

        // --------------------------------------------------------------------

        protected virtual void Awake()
        {
            Debug.Assert(m_PropertyName.Length > 0, "PropertyName can not be empty in AnimatorSpeedSetter");

            m_PropertyHash = Animator.StringToHash(m_PropertyName);
            m_Animator = GetComponent<Animator>();
        }

        // --------------------------------------------------------------------

        protected virtual void OnEnable()
        {
            ResetToInit();
        }

        // --------------------------------------------------------------------
        
        protected void Set(float value, bool immediate = false)
        {
            m_CurrentValue = immediate ? value : Mathf.Lerp(m_CurrentValue, value, m_InterpolationSpeed * Time.deltaTime);

            m_Animator.SetFloat(m_PropertyHash, m_CurrentValue);
        }
    }

    public class AnimatorSpeedSetter : AnimatorFloatSetter
    {
        [SerializeField] Transform OptionalForwardReference;
        private Vector3 m_PrevPos;

        [Tooltip("If the displacement is greater than this number the speed is set to 0")]
        [SerializeField] float m_TeleportationThreshold = 1f;

        // --------------------------------------------------------------------

        protected override void OnEnable()
        {
            base.OnEnable();
            m_PrevPos = transform.position;
            Set(0, true);
        }

        // --------------------------------------------------------------------

        public override void OnReset()
        {
            base.OnReset();
            m_PrevPos = transform.position;
            Set(0, true);
        }

        // --------------------------------------------------------------------

        void FixedUpdate()
        {
            Vector3 disp = (transform.position - m_PrevPos);
            disp.Scale(new Vector3(1, 0, 1));
            Transform refTransform = OptionalForwardReference ? OptionalForwardReference : transform;
            float sign = Mathf.Sign(Vector3.Dot(disp.normalized, refTransform.forward));
            bool isTeleport = disp.magnitude > m_TeleportationThreshold;
            float speed = !isTeleport ?  disp.magnitude / Time.deltaTime * sign : 0f;
            Set(speed, isTeleport);

            m_PrevPos = transform.position;
        }
    }
}
