using UnityEngine;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public abstract class SceneAudio : MonoBehaviour  
    {
        [FormerlySerializedAs("m_Music")] 
        [SerializeField] AudioClip m_Clip;
        [SerializeField] float m_Volume = 1f;
        [SerializeField] bool m_PlayOnStart;

        private float m_Time = 0;

        // --------------------------------------------------------------------

        private void Start()
        {
            if (MusicManager.Exists && m_PlayOnStart)
            {
                if (m_Clip)
                    Play();
                else
                    Stop();
            }
        }

        // --------------------------------------------------------------------

        public void Play()
        {
            GetStack().Play(m_Clip, m_Volume);
        }

        // --------------------------------------------------------------------

        public void Stop()
        {
            GetStack().Stop();
        }

        // --------------------------------------------------------------------

        public void FadeIn(float duration = 1f)
        {
            GetStack().Play(m_Clip, m_Volume);
            GetStack().FadeIn(duration);
        }

        // --------------------------------------------------------------------

        public void FadeOut(float duration = 1f)
        {
            GetStack().FadeOut(duration);
        }

        // --------------------------------------------------------------------

        public void Push(float duration = 1f)
        {
            GetStack().Push(m_Clip, m_Volume, duration, m_Time);
        }

        // --------------------------------------------------------------------

        public void Pop(float duration = 1f)
        {
            GetStack().Pop(m_Clip, duration, out m_Time);
        }

        protected abstract AudioStack GetStack();
    }
    
    public class SceneMusic : SceneAudio
    {
        protected override AudioStack GetStack()
        {
            return MusicManager.Instance.GetStack();
        }
    }
}