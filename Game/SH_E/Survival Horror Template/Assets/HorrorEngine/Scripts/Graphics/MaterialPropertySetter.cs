using UnityEngine;

namespace HorrorEngine
{
    public class MaterialPropertySetter : MonoBehaviour
    {
        [SerializeField] int m_MaterialIndex;
        [SerializeField] string m_PropertyName;


        private int m_PropertyHash;
        private Renderer m_Renderer;

        private void Awake()
        {
            m_Renderer = GetComponent<Renderer>();
            m_PropertyHash = Shader.PropertyToID(m_PropertyName);
        }

        public void SetProperty(AnimationEvent e)
        {
            var material = m_Renderer.materials[e.intParameter];
            material.SetFloat(e.stringParameter, e.floatParameter);
        }

        public void SetPropertyValue(float val)
        {
            var materials = m_Renderer.materials;
            materials[m_MaterialIndex].SetFloat(m_PropertyHash, val);
            m_Renderer.materials = materials;
        }
    }
}