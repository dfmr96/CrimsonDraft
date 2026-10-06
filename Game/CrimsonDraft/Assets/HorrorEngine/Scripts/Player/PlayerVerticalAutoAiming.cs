using UnityEngine;

namespace HorrorEngine
{
    public class PlayerVerticalAutoAiming : MonoBehaviour, IPlayerVerticalAiming
    {
        [SerializeField] PlayerStateAiming m_AimingState;
        [SerializeField] float m_UpAngleRef = -35;
        [SerializeField] float m_DownAngleRef = 35;
        [SerializeField] Vector3 m_Offset = new Vector3(0,1,0);
        [SerializeField] bool m_ShowDebug;
        [SerializeField] float m_AimingAngleThreshold = 20;
        [SerializeField] float m_LerpSpeed = 1;

        private float m_Verticality;
        public float Verticality 
        { 
            get => m_Verticality;
            set => m_Verticality = value;
        }

        private void Update()
        {
            if (!m_AimingState)
            {
                enabled = false;
                return;
            }

            Aimable aimable = m_AimingState.CurrentAimable;
            if (aimable)
            {
                Vector3 aimingSourceRef = transform.TransformPoint(m_Offset);
                Vector3 aimingPoint = aimable.AimingPoint;
                Vector3 neutralAimingPoint = new Vector3(aimingPoint.x, aimingSourceRef.y, aimingPoint.z);
                Vector3 dirToNeutral = (neutralAimingPoint - aimingSourceRef).normalized;
                Vector3 dirToAimingPoint = (aimingPoint - aimingSourceRef).normalized;
                float angle = Vector3.SignedAngle(dirToNeutral, dirToAimingPoint, transform.right);

                if (m_ShowDebug)
                {
                    Debug.DrawLine(aimingSourceRef, aimingSourceRef + dirToNeutral * 3, Color.red);
                    Debug.DrawLine(aimingSourceRef, aimingSourceRef + dirToAimingPoint * 3, Color.yellow);
                }

                float HAngle = Vector3.Angle(transform.forward.ToXZ().normalized, dirToAimingPoint.ToXZ().normalized);
                float newVert = 0;
                if (HAngle < m_AimingAngleThreshold)
                    newVert = MathUtils.Map(angle, m_DownAngleRef, m_UpAngleRef, -1, 1);

                m_Verticality = Mathf.MoveTowards(m_Verticality, newVert, Time.deltaTime * m_LerpSpeed);
            }
        }
    }
}