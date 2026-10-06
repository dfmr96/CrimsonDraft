using UnityEngine;
using UnityEngine.Audio;

using System.Collections;
using System.Collections.Generic;

namespace HorrorEngine
{
    public class MusicManager : SingletonBehaviour<MusicManager>
    {
        private struct MusicEntry
        {
            public AudioClip Clip;
            public float Volume;
            public float Time;
        }

        [SerializeField] private AudioSource m_MainChannel;
        [SerializeField] private float m_FirstMusicFade = 1f;

        private Coroutine m_FadeRoutine;

        private List<MusicEntry> m_Music = new List<MusicEntry>();
        
        // --------------------------------------------------------------------

        public void Push(AudioClip clip, float volume, float duration, float startingTime = 0)
        {
            float time = startingTime;

            //Cache current time
            if (m_Music.Count > 0)
            {
                var oldEntry = m_Music[m_Music.Count - 1];
                oldEntry.Time = m_MainChannel.time;
                m_Music[m_Music.Count - 1] = oldEntry;

                if (oldEntry.Clip == clip)
                    time = oldEntry.Time;
            }

            MusicEntry newEntry = new MusicEntry()
            {
                Clip = clip,
                Volume = volume,
                Time = time
            };

            m_Music.Add(newEntry);

            if (isActiveAndEnabled)
                m_FadeRoutine = StartCoroutine(MusicTransition(newEntry, duration));
        }

        // --------------------------------------------------------------------

        public void Pop(AudioClip clip, float duration, out float time)
        {
            if (m_Music.Count == 0 || m_Music[m_Music.Count - 1].Clip != clip)
            {
                Debug.LogWarning("MusicManager: Clip did not match last entry. It couldn't be popped");
            }

            MusicEntry oldClip = m_Music[m_Music.Count - 1];
            m_Music.RemoveAt(m_Music.Count - 1);

            MusicEntry nextClip = m_Music.Count > 0 ? m_Music[m_Music.Count - 1] : new MusicEntry();
            if (oldClip.Clip == nextClip.Clip)
            {
                nextClip.Time = m_MainChannel.time;
            }

            time = m_MainChannel.time;

            if (isActiveAndEnabled)
                m_FadeRoutine = StartCoroutine(MusicTransition(nextClip, duration));
        }

        // --------------------------------------------------------------------

        private IEnumerator MusicTransition(MusicEntry entry, float duration)
        {
            if (m_MainChannel.clip != entry.Clip)
            {
                if (m_MainChannel.isPlaying)
                    yield return Fade(m_MainChannel.volume, 0f, duration * 0.5f);
            }

            if (entry.Clip)
            {
                if (m_MainChannel.clip != entry.Clip)
                {
                    m_MainChannel.clip = entry.Clip;
                    m_MainChannel.time = entry.Time;
                    m_MainChannel.Play();
                }

                yield return Fade(m_MainChannel.volume, entry.Volume, duration * 0.5f);
            }
        }

        // --------------------------------------------------------------------

        public void Play(AudioClip clip, float volume)
        {
            if (m_FadeRoutine != null)
                StopCoroutine(m_FadeRoutine);

            m_Music.Clear();
            if (!m_MainChannel.isPlaying)
                m_MainChannel.volume = 0;

            Push(clip, volume, m_FirstMusicFade);
        }

        // --------------------------------------------------------------------

        public void Stop()
        {
            m_MainChannel.Stop();
            m_MainChannel.clip = null;
            m_MainChannel.volume = 0;
            m_Music.Clear();
        }

        // --------------------------------------------------------------------

        public void FadeOut(float duration = 1f)
        {
            FadeOutTo(0f, duration);
        }

        // --------------------------------------------------------------------

        public void FadeOutTo(float to, float duration = 1f)
        {
            float volume = m_MainChannel.volume;
            m_FadeRoutine = StartCoroutine(Fade(volume, to, duration));
        }

        // --------------------------------------------------------------------

        public void FadeIn(float toVolume, float duration = 1f)
        {
            float volume = m_MainChannel.volume;
            FadeInFrom(volume, toVolume, duration);
        }

        // ----------------------------- ---------------------------------------

        public void FadeInFrom(float from, float volume, float duration = 1f)
        {
            m_FadeRoutine = StartCoroutine(Fade(from, volume, duration));
        }

        // --------------------------------------------------------------------

        public IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0;
            while (t < duration)
            {
                t = t + Time.deltaTime;
                yield return Yielders.EndOfFrame;

                m_MainChannel.volume = t > 0 ? Mathf.Lerp(from, to, t / duration) : from;
            }

            m_MainChannel.volume = to;
        }
    }
}