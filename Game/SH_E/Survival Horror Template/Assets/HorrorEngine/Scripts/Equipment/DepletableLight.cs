using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class DepletableLight : MonoBehaviour
    {
        [FormerlySerializedAs("m_IntensityOverStatus")]
        [SerializeField] public AnimationCurve m_MultiplierOverStatus;
        [SerializeField] public float m_InterpolationSpeed = 3f;

        private Light m_Light;


        private LensFlareComponentSRP m_Flare;
        private float m_OriginalFlareIntensity;


        private float m_OriginalIntensity;
        private float m_Status;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Light = GetComponent<Light>();


            m_Flare = GetComponent<LensFlareComponentSRP>();
            if (m_Flare)
                m_OriginalFlareIntensity = m_Flare.intensity;

            m_OriginalIntensity = m_Light.intensity;
            m_Status = 1f;
        }

        // --------------------------------------------------------------------

        public void OnDeplete(float status)
        {
            m_Status = status;
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            float targetIntensity = m_OriginalIntensity * m_MultiplierOverStatus.Evaluate(m_Status);
            m_Light.intensity = Mathf.MoveTowards(m_Light.intensity, targetIntensity, Time.deltaTime * m_InterpolationSpeed);

            if (m_Flare)
            {
                float targetBrightness = m_OriginalFlareIntensity * m_MultiplierOverStatus.Evaluate(m_Status);
                m_Flare.intensity = Mathf.MoveTowards(m_Flare.intensity, targetBrightness, Time.deltaTime * m_InterpolationSpeed);
            }
        }

    }
}