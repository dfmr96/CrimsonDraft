using UnityEngine;

namespace HorrorEngine
{
    public class Rotator : MonoBehaviour
    {
        [SerializeField] Vector3 m_RotationAxis = new Vector3(0f, 1f, 0f);
        [SerializeField] float m_Speed = 10f;
        [SerializeField] bool m_UnscaledTime;

        void Update()
        {
            transform.Rotate(m_RotationAxis, m_Speed * (m_UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime));
        }
    }

}