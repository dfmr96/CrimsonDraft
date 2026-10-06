using UnityEngine;

namespace HorrorEngine
{
    public abstract class VFXSelector : ScriptableObject
    {
        [SerializeField] VFXSelector m_Prototype;
        public virtual GameObject Select(VisualEffectPlayer player)
        {
            return m_Prototype ? m_Prototype.Select(player) : null;
        }
    }

    [System.Serializable]
    public class VFXEntry
    {
        public string Identifier;
        public VFXSelector Selector;
        public GameObject Effect;
        public ObjectInstantiationSettings InstantiationSettings;
    }

    public class VisualEffectPlayer : MonoBehaviour
    {
        [SerializeField] private SocketController m_SocketController;
        [SerializeField] private VFXEntry[] m_VisualEffects;

        // --------------------------------------------------------------------
        
        public void PlayVFX(AnimationEvent evt)
        {
            if (evt.animatorClipInfo.weight > 0.5f)
            {
                PlayVFXByIdentifier(evt.stringParameter);
            }
        }

        // --------------------------------------------------------------------
        
        public void PlayVFXByIdentifier(string identifier)
        {
            foreach (var vfx in m_VisualEffects)
            {
                if (vfx.Identifier == identifier)
                {
                    GameObject prefab = vfx.Selector ? vfx.Selector.Select(this) : vfx.Effect;

                    if (prefab)
                    {
                        vfx.InstantiationSettings.Instantiate(prefab, m_SocketController);
                    }
                    break;
                }
            }
        }
        
        // --------------------------------------------------------------------

        public void PlayVFXByIndex(int index)
        {
            if (index < 0 || index >= m_VisualEffects.Length) return;

            var vfx = m_VisualEffects[index];
            GameObject prefab = vfx.Selector ? vfx.Selector.Select(this) : vfx.Effect;

            if (prefab)
            {
                vfx.InstantiationSettings.Instantiate(prefab, m_SocketController);
            }
        }
    }
}
