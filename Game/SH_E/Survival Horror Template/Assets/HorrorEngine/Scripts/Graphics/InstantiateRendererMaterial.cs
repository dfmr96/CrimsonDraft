using UnityEngine;

namespace HorrorEngine
{
    public class InstantiateRendererMaterial : MonoBehaviour
    {
        [SerializeField] bool m_InstantiateOnAwake;
        [SerializeField] int m_MaterialIndex;

        private void Awake()
        {
            if (m_InstantiateOnAwake) 
            {
                InstanceMaterialAtIndex(m_MaterialIndex);
            }
        }

        public void InstanceMaterialAtIndex(int index)
        {
            var renderer = GetComponent<Renderer>();
            var material = renderer.materials[index];
            SetMaterial(material, index);
        }

        public void InstanceMaterial(AnimationEvent e)
        {
            
            Material material = (Material)e.objectReferenceParameter;
            if (material)
            {
                SetMaterial(material, e.intParameter);
            }
        }

        public void InstanceMaterial(Material material)
        {
            if (material)
            {
                SetMaterial(material, m_MaterialIndex);
            }
        }

        public void SetMaterial(Material material, int index)
        {
            var renderer = GetComponent<Renderer>();

            var newMat = new Material(material);
            var materials = renderer.materials;
            materials[index] = newMat;
            renderer.materials = materials;
        }
    }
}