using UnityEngine;

namespace HorrorEngine
{
    public class SceneMusic : MonoBehaviour
    {
        [SerializeField] AudioClip m_Music;
        [SerializeField] float m_Volume = 1f;
        [SerializeField] bool m_PlayOnStart;

        private float m_Time = 0;

        // --------------------------------------------------------------------

        private void Start()
        {
            if (MusicManager.Exists && m_PlayOnStart)
            {
                if (m_Music)
                    Play();
                else
                    Stop();
            }
        }

        // --------------------------------------------------------------------

        public void Play()
        {
            MusicManager.Instance.Play(m_Music, m_Volume);
        }

        // --------------------------------------------------------------------

        public void Stop()
        {
            MusicManager.Instance.Stop();
        }

        // --------------------------------------------------------------------

        public void FadeIn(float duration = 1f)
        {
            MusicManager.Instance.Play(m_Music, m_Volume);
            MusicManager.Instance.FadeIn(duration);
        }

        // --------------------------------------------------------------------

        public void FadeOut(float duration = 1f)
        {
            MusicManager.Instance.FadeOut(duration);
        }

        // --------------------------------------------------------------------

        public void Push(float duration = 1f)
        {
            MusicManager.Instance.Push(m_Music, m_Volume, duration, m_Time);
        }

        // --------------------------------------------------------------------

        public void Pop(float duration = 1f)
        {
            MusicManager.Instance.Pop(m_Music, duration, out m_Time);
        }

      
    }
}