using UnityEngine;

namespace HorrorEngine
{
    public class ShadowBlob : MonoBehaviour
    {
        [SerializeField] GroundDetector m_GroundDetector;
        [SerializeField] float m_Bias = 0.01f;

        void Start()
        {
            if (!m_GroundDetector)
                enabled = false;
        }

        void Update()
        {
            m_GroundDetector.Detect(transform.parent.position);
            transform.position = m_GroundDetector.Position + Vector3.up* m_Bias;
            Vector3 fwd = Vector3.Cross(transform.parent.right, m_GroundDetector.Normal).normalized;
            transform.rotation = Quaternion.LookRotation(fwd, m_GroundDetector.Normal);
        }
    }
}
