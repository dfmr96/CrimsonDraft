using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

namespace HorrorEngine
{
    public class CinematicPlayer : MonoBehaviour
    {
        [SerializeField] bool m_FadeOut = false;
        [SerializeField] bool m_FadeIn = true;

        public UnityEvent OnCutsceneStart;
        public UnityEvent OnCutsceneEnd;

        private void Start()
        {
            UIManager.Get<UICinematicPlayer>().OnCutsceneEnd.AddListener(OnCutsceneEndCallback);
        }

        private void OnDestroy()
        {
            if (UIManager.Exists) 
            {
                UIManager.Get<UICinematicPlayer>().OnCutsceneEnd.RemoveListener(OnCutsceneEndCallback);
            }
        }

        private void OnCutsceneEndCallback()
        {
            OnCutsceneEnd?.Invoke();
        }

        public void Play(VideoClip clip)
        {
            OnCutsceneStart?.Invoke();
            UIManager.Get<UICinematicPlayer>().Show(clip, m_FadeOut, m_FadeIn);
        }
    }
}