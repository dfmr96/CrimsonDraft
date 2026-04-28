using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [Serializable]
    public struct GrabPreventionData
    {
        public string GrabberStateTag;
    }


    [Serializable]
    public struct GrabReleaseData
    {
        public string GrabberStateTag;
        public float Delay;
    }

    public class PlayerGrabHandler : MonoBehaviour
    {
        [Tooltip("This is the amount of time after a grab during which the player can't be grabbed")]
        [SerializeField] private float m_UngrabbableWindow = 1f;

        private ActorStateController m_StateController;
        private PlayerStateGrabbed[] m_GrabbedStates;

        public Grabber Grabber { get; private set; }
        public GrabPositioning Positioning { get; private set; }

        public UnityEvent<GrabPreventionData> OnPrevented;
        public UnityEvent<GrabReleaseData> OnRelease;

        private Health m_Health;
        private Health m_GrabberHealth;
        private UnityAction<Health> m_OnGrabberDeath;
        private float m_ReleaseTime;

        public bool CanBeGrabbed => !m_Health.IsDead && Grabber == null && (Time.time - m_ReleaseTime) > m_UngrabbableWindow;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_OnGrabberDeath = OnGrabberDeath;
            m_StateController = GetComponentInChildren<ActorStateController>();
            m_GrabbedStates = GetComponentsInChildren<PlayerStateGrabbed>();
            m_Health = GetComponent<Health>();

            m_Health.OnDeath.AddListener((health) =>
            {
                Release(new GrabReleaseData()
                {
                    Delay = 0,
                    GrabberStateTag = ""
                });
            });
        }

        // --------------------------------------------------------------------

        public void SetGrabbed(Grabber grabber, string tag, GrabPositioning positioning)
        {
            foreach(var state in m_GrabbedStates)
            {
                if (state.HasTag(tag))
                {
                    Grabber = grabber;
                    Positioning = positioning;
                    m_GrabberHealth = Grabber.GetComponent<Health>();
                    m_GrabberHealth.OnDeath.AddListener(m_OnGrabberDeath);
                    m_StateController.SetState(state);
                    return;
                }
            }
        }

        // --------------------------------------------------------------------

        private void OnGrabberDeath(Health health)
        {
            Release();
        }

        // --------------------------------------------------------------------

        public void Prevent(GrabPreventionData preventData)
        {
            OnPrevented?.Invoke(preventData);
        }

        // --------------------------------------------------------------------

        public void Release(GrabReleaseData releaseData)
        {
            m_ReleaseTime = Time.time;
            Grabber = null;
            if (m_GrabberHealth)
            {
                m_GrabberHealth.OnDeath.RemoveListener(m_OnGrabberDeath);
                m_GrabberHealth = null;
            }
            OnRelease?.Invoke(releaseData);
        }

        // --------------------------------------------------------------------

        public void Release()
        {
            if (Grabber)
                Release(new GrabReleaseData() { });
        }
    }
}