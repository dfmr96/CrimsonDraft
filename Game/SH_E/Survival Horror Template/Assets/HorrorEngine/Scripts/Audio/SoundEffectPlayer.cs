using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public abstract class SFXSelector : ScriptableObject
    {
        [SerializeField] SFXSelector m_Prototype;

        public virtual SoundCue Select(SoundEffectPlayer player)
        {
            return m_Prototype ? m_Prototype.Select(player) : null;
        }
    }


    [System.Serializable]
    public class SFXEntry
    {
        public string Identifier;
        public SFXSelector Selector;
        public SoundCue[] Cues;
    }

    public class SoundEffectPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource m_Source;
        [SerializeField] private List<SFXEntry> m_SoundEffects;

        private void Awake()
        {
            Debug.Assert(m_Source, "AudioSource on the SoundEffectPlayer component has not been specified");
        }

        public void PlaySFX(AnimationEvent evt)
        {
            if (evt.animatorClipInfo.weight > 0.5f)
            {
                foreach (var sfx in m_SoundEffects)
                {
                    if (sfx.Identifier == evt.stringParameter)
                    {
                        SoundCue cue = null;
                        if (sfx.Selector)
                        {
                            cue = sfx.Selector.Select(this);
                        }
                        else
                        {
                            cue = sfx.Cues[Random.Range(0, sfx.Cues.Length)];
                        }

                        if (cue != null)
                        {
                            AudioClip clip = cue.GetClip(out float volume, out float pitch);
                            m_Source.pitch = pitch;
                            m_Source.PlayOneShot(clip, volume);
                        }
                    }
                }
            }
        }
    }
}