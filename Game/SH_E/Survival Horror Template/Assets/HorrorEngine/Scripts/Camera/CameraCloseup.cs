using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


#if UNITY_6000_0_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

namespace HorrorEngine
{
    public abstract class Closeup : MonoBehaviour
    {
        [SerializeField] protected bool m_PauseGame = true;
        [SerializeField] float m_FadeOutDuration = 0.5f;
        [SerializeField] float m_FadeInDuration = 0.5f;
        

        private UIFade m_UIFade;
        
        private static Coroutine m_Coroutine;
        private static Queue<CloseupRoutineEntry> m_Routines = new Queue<CloseupRoutineEntry>();

        public UnityEvent OnActivated;
        public UnityEvent OnDeactivated;

        private UIContext m_UIContext;

        private struct CloseupRoutineEntry
        {
            public Func<IEnumerator> Routine;
            public bool Pause;
            public bool Unpause;
        }

        protected virtual void Awake()
        {
            m_UIContext = GetComponent<UIContext>();
        }


        // --------------------------------------------------------------------

        protected virtual void Start()
        {
            m_UIFade = UIManager.Get<UIFade>();
            enabled = false;
        }


        // --------------------------------------------------------------------

        public void Activate()
        {
            m_UIContext?.Activate();
            Activate_Internal(m_FadeOutDuration, m_FadeInDuration);
        } 
        
        // --------------------------------------------------------------------

        public void ActivateWithoutFade()
        {
            Activate_Internal(0f, 0f);
        }

        // --------------------------------------------------------------------

        private void Activate_Internal(float fadeOutTime, float fadeInTime)
        {
            m_Routines.Enqueue(new CloseupRoutineEntry()
            {
                Routine = ActivationRoutine,
                Pause = m_PauseGame,
                Unpause = false
            });

            if (m_Coroutine == null)
            {
                m_Coroutine = StartCoroutine(CloseupTransitionRoutine(0, fadeOutTime, fadeInTime));
            }
        }

        // --------------------------------------------------------------------

        public void Deactivate()
        {
            Deactivate(0);
        }

        // --------------------------------------------------------------------

        public void DeactivateWithoutFade(float delay)
        {
            Deactivate_Internal(delay, 0f, 0f);
        }


        // --------------------------------------------------------------------

        public void Deactivate(float delay) // Called from UnityEvents (keep it public)
        {
            Deactivate_Internal(delay, m_FadeOutDuration, m_FadeInDuration);
        }

        // --------------------------------------------------------------------

        private void Deactivate_Internal(float delay, float fadeOutTime, float fadeInTime)
        {
            m_Routines.Enqueue(new CloseupRoutineEntry()
            {
                Routine = DeactivationRoutine,
                Pause = false,
                Unpause = m_PauseGame
            });

            if (m_Coroutine == null)
            {
                m_Coroutine = StartCoroutine(CloseupTransitionRoutine(delay, fadeOutTime, fadeInTime));
            }
        }


        // --------------------------------------------------------------------

        private IEnumerator CloseupTransitionRoutine(float delay, float fadeOutTime, float fadeInTime)
        {
            if (delay > 0)
                yield return Yielders.UnscaledTime(delay);

            if (m_Routines.Peek().Pause)
                PauseController.Instance.Pause(CameraSystem.Instance);

            // Fade Out
            yield return m_UIFade.Fade(0f, 1f, fadeOutTime);

            bool unpause = false;
            while (m_Routines.Count > 0)
            {
                var routineEntry = m_Routines.Dequeue();
                unpause = routineEntry.Unpause;
                yield return StartCoroutine(routineEntry.Routine?.Invoke());
            }

            if (unpause)
                PauseController.Instance.Resume(CameraSystem.Instance);

            // Fade In
            yield return m_UIFade.Fade(1f, 0f, fadeInTime);

            m_Coroutine = null;

            // Check for any routines added during fadeout
            if (m_Routines.Count > 0)
            {
                m_Coroutine = StartCoroutine(CloseupTransitionRoutine(delay, fadeOutTime, fadeInTime));
            }
        }


        // --------------------------------------------------------------------

        public virtual IEnumerator ActivationRoutine() 
        {
            OnActivated?.Invoke();
            yield return null;
        }

        public virtual IEnumerator DeactivationRoutine()
        {
            OnDeactivated?.Invoke();
            m_UIContext?.Deactivate();
            yield return null;
        }


    }

    public class CameraCloseup : Closeup
    {
        [SerializeField] bool m_HidePlayer = false;
#if UNITY_6000_0_OR_NEWER
        [SerializeField] CinemachineCamera m_Camera;
#else
        [SerializeField] CinemachineVirtualCamera m_Camera;
#endif

        // --------------------------------------------------------------------

        protected override void Start()
        {
            base.Start();
            m_Camera.gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        public override IEnumerator ActivationRoutine()
        {
            yield return base.ActivationRoutine();

            if (m_HidePlayer)
            {
                GameManager.Instance.Player.SetVisible(false);
            }
            m_Camera.gameObject.SetActive(true);

            yield return null;
        }

        // --------------------------------------------------------------------

        public override IEnumerator DeactivationRoutine()
        {
            if (m_HidePlayer)
            {
                GameManager.Instance.Player.SetVisible(true);
            }

            m_Camera.gameObject.SetActive(false);

            return base.DeactivationRoutine();
        }
    }
}