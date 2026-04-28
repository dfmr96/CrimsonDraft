using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class AudioStack : MonoBehaviour
    {
        private struct AudioEntry
        {
            public AudioClip Clip;
            public float Volume;
            public float Time;
        }

        [SerializeField] private AudioSource m_MainChannel;
        [FormerlySerializedAs("m_FirstMusicFade")]
        [SerializeField] private float m_DefaultFade = 1f;

        [SerializeField] private bool m_ShowDebug;

        private Coroutine m_FadeRoutine;

        private List<AudioEntry> m_AudioEntries = new List<AudioEntry>();
        
        // --------------------------------------------------------------------

        public void Push(AudioClip clip, float volume, float duration, float startingTime = 0)
        {
            float time = startingTime;
            bool sameClip = false;
            
            //Cache current time
            if (m_AudioEntries.Count > 0)
            {
                AudioEntry lastEntry = m_AudioEntries[m_AudioEntries.Count - 1];
                lastEntry.Time = m_MainChannel.time;
                m_AudioEntries[m_AudioEntries.Count - 1] = lastEntry;

                if (lastEntry.Clip == clip)
                {
                    sameClip = true;
                    time = lastEntry.Time;
                }
            }

            AudioEntry entry;
            if (!sameClip)
            {
                entry = new AudioEntry()
                {
                    Clip = clip,
                    Volume = volume,
                    Time = time
                };
                m_AudioEntries.Add(entry);
            }
            else
            {
                entry = m_AudioEntries[m_AudioEntries.Count - 1];
            }

            if (isActiveAndEnabled)
            {
                if (m_FadeRoutine != null)
                    StopCoroutine(m_FadeRoutine);
                m_FadeRoutine = StartCoroutine(Transition(entry, duration));
            }
        }

        // --------------------------------------------------------------------

        public void Pop(AudioClip clip, float duration, out float time)
        {
            if (m_AudioEntries.Count == 0 || m_AudioEntries[m_AudioEntries.Count - 1].Clip != clip)
            {
                for (int i = m_AudioEntries.Count - 1; i >= 0; --i)
                {
                    if (m_AudioEntries[i].Clip == clip)
                        m_AudioEntries.RemoveAt(i);
                }

                time = 0;
                return;
            }

            AudioEntry oldClip = m_AudioEntries[m_AudioEntries.Count - 1];
            m_AudioEntries.RemoveAt(m_AudioEntries.Count - 1);

            AudioEntry nextClip = m_AudioEntries.Count > 0 ? m_AudioEntries[m_AudioEntries.Count - 1] : new AudioEntry();
            if (oldClip.Clip == nextClip.Clip)
            {
                nextClip.Time = m_MainChannel.time;
            }

            time = m_MainChannel.time;

            if (isActiveAndEnabled)
            {
                if (m_FadeRoutine!=null)
                    StopCoroutine(m_FadeRoutine);
                m_FadeRoutine = StartCoroutine(Transition(nextClip, duration));
            }
        }

        // --------------------------------------------------------------------

        private IEnumerator Transition(AudioEntry entry, float duration)
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
                    m_MainChannel.time = MathUtils.Wrap(entry.Time, 0 , entry.Clip.length);
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

            m_AudioEntries.Clear();
            if (!m_MainChannel.isPlaying)
                m_MainChannel.volume = 0;

            Push(clip, volume, m_DefaultFade);
        }

        // --------------------------------------------------------------------

        public void Stop()
        {
            m_MainChannel.Stop();
            m_MainChannel.clip = null;
            m_MainChannel.volume = 0;
            m_AudioEntries.Clear();
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

        // --------------------------------------------------------------------

        public void FadeIn(float duration = 1f)
        {
            float volume = m_AudioEntries.Count > 0 ? m_AudioEntries[m_AudioEntries.Count - 1].Volume : 1f;
            FadeIn(volume, duration);
        }

        // --------------------------------------------------------------------

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
                t = t + Time.unscaledDeltaTime;
                yield return Yielders.EndOfFrame;

                m_MainChannel.volume = t > 0 ? Mathf.Lerp(from, to, t / duration) : from;
            }

            m_MainChannel.volume = to;
        }

        // --------------------------------------------------------------------

        public void Pause(float fadeOutDuration)
        {
            FadeOut(fadeOutDuration);
            enabled = false;
        }

        // --------------------------------------------------------------------

        public void Resume(float fadeInDuration)
        {
            enabled = true;
            if (m_AudioEntries.Count > 0)
                FadeIn(fadeInDuration);
        }

        // --------------------------------------------------------------------

        private void OnGUI()
        {
            if (m_ShowDebug)
            {
                GUIStyle style = new GUIStyle();
                style.fontSize = 18;
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.yellow;

                GUILayout.BeginArea(new Rect(10, 10, Screen.width, Screen.height));

                GUILayout.Label("AudioStack: " + name, style);
                GUILayout.Label("-------------------------------", style);

                foreach (var entry in m_AudioEntries)
                {
                    GUILayout.Label(entry.Clip.name + " : " + entry.Volume, style);
                }
                GUILayout.EndArea();
            }
        }
    }
}