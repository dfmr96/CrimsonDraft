using UnityEngine;
using UnityEngine.UI;

namespace HorrorEngine
{

    public class UIStatusLine : MonoBehaviour
    {
        private static readonly int k_UnescaledTimeID = Shader.PropertyToID("_UnescaledTime");
        
        private Material m_MatInstance;

        private void Awake()
        {
            var image = GetComponent<Image>();
            m_MatInstance = new Material(image.material);
            image.material = m_MatInstance;
        }

        private void Update()
        {
            m_MatInstance.SetFloat(k_UnescaledTimeID, Time.unscaledTime);
        }


        public virtual void SetStatus(UIPlayerStatusEntry status)
        {
            if (m_MatInstance)
            {
                m_MatInstance.SetColor("_Color", status.Color);
                m_MatInstance.SetFloat("_Interval", status.Interval);
                m_MatInstance.SetFloat("_Tiling", status.Tiling);
                m_MatInstance.SetFloat("_SpeedMultiplier", status.Speed);
            }
        }
    }
}