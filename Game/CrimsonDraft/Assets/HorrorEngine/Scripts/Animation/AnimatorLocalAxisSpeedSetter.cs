using UnityEngine;

namespace HorrorEngine
{
    public class AnimatorLocalAxisSpeedSetter : AnimatorFloatSetter
    {
        [SerializeField] Vector3 m_LocalAxis;
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
            Transform refTransform = OptionalForwardReference ? OptionalForwardReference : transform;
            
            Vector3 localDisp = refTransform.InverseTransformDirection(disp);
            localDisp.x *= m_LocalAxis.x;
            localDisp.y *= m_LocalAxis.y;
            localDisp.z *= m_LocalAxis.z;

            bool isTeleport = disp.magnitude > m_TeleportationThreshold;

            float sign = Mathf.Sign(Vector3.Dot(localDisp, m_LocalAxis));
            
            if (!isTeleport)
                Set(localDisp.magnitude / Time.deltaTime * sign, isTeleport);
            else
                Set(0f, true);

            m_PrevPos = transform.position;
        }
    }
}
