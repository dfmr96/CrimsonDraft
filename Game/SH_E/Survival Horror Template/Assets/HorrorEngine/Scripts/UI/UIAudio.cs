using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(AudioSource))]
    public class UIAudio : MonoBehaviour
    {
        private AudioSource m_AudioSource;

        private void Awake()
        {
            m_AudioSource = GetComponent<AudioSource>();
        }

        public void Play(AudioClip clip)
        {
            m_AudioSource.PlayOneShot(clip);
        }
    }
}
