using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;

namespace HorrorEngine
{
    public class UICinematicPlayer : MonoBehaviour
    {
        [SerializeField] float m_FadeOutDuration = 0.5f;
        [SerializeField] float m_FadeInDuration = 1f;
        private VideoPlayer m_Player;
        private RawImage m_Image;
        private RenderTexture m_Texture;

        public UnityEvent OnCutsceneEnd;

        private void Awake()
        {
            m_Player = GetComponentInChildren<VideoPlayer>();
            m_Image = GetComponentInChildren<RawImage>();

            var audioSource = m_Player.GetComponentInChildren<AudioSource>();
            m_Player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            m_Player.SetTargetAudioSource(0, audioSource);
            m_Player.EnableAudioTrack(0, true);
            m_Player.controlledAudioTrackCount = 1;
        }

        private void Start()
        {
            gameObject.SetActive(false);
        }

        public void Show(VideoClip clip, bool fadeOut, bool fadeIn)
        {
            PauseController.Instance.Pause(this);
            gameObject.SetActive(true);

            m_Texture = RenderTexture.GetTemporary((int)clip.width, (int)clip.height, 0, RenderTextureFormat.ARGB32);
            m_Player.targetTexture = m_Texture;
            m_Image.texture = m_Texture;

            m_Player.clip = clip;

            StartCoroutine(ShowCinematicRoutine(clip, fadeOut, fadeIn));
        }

        private IEnumerator ShowCinematicRoutine(VideoClip clip, bool fadeOut, bool fadeIn)
        {
            if (fadeOut)
            {
                UIManager.Get<UIFade>().Fade(0, 1, m_FadeOutDuration);

                yield return Yielders.UnscaledTime(m_FadeOutDuration);
            }
            else
            {
                UIManager.Get<UIFade>().Set(1);  // Go to black directly
            }

            m_Player.Prepare();
            while (!m_Player.isPrepared)
            {
                yield return new WaitForEndOfFrame();
            }

            UIManager.Get<UIFade>().Set(0);
            m_Player.Play();

            yield return Yielders.UnscaledTime((float)clip.length);

            if (fadeIn)
            {
                UIManager.Get<UIFade>().Fade(1, 0, m_FadeInDuration);
            }

            OnCutsceneEnd?.Invoke();

            Hide();
        }

        private void Hide()
        {
            PauseController.Instance.Resume(this);
            gameObject.SetActive(false);

            m_Texture.Release();

            UIManager.PopAction();
        }


    }
}