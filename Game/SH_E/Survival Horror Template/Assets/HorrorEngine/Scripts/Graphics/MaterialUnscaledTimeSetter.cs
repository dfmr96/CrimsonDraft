using UnityEngine;

namespace HorrorEngine
{
    public class MaterialUnscaledTimeSetter : MonoBehaviour
    {
        [SerializeField] private string m_PropertyName = "_UnscaledTime";
        [SerializeField] private MeshRenderer m_MeshRendered;
        [SerializeField] private int[] m_MaterialIndices;

        private int m_PropertyID;

        private void Awake()
        {
            m_PropertyID = Shader.PropertyToID(m_PropertyName);
        }

        void Update()
        {
            if (m_MaterialIndices.Length == 0)          
            {
                m_MeshRendered.material.SetFloat(m_PropertyID, Time.unscaledTime);
                return;
            }

            for (int i = 0; i < m_MaterialIndices.Length; i++)
            {
                m_MeshRendered.materials[m_MaterialIndices[i]].SetFloat(m_PropertyID, Time.unscaledTime);
            }
        }
    }
}