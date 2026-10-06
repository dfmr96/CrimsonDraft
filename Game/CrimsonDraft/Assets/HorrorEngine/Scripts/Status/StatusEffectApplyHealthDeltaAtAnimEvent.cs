using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "StatusEffectApplyHealthDeltaAtAnimEvent", menuName = "Horror Engine/Status/Effects/ApplyHealthDeltaAtAnimEvent")]
    public class StatusEffectApplyHealthDeltaAtAnimEvent : StatusEffectApplyHealthDelta
    {
        [SerializeField] private float m_HealthDeltaPerEvent;
        [SerializeField] private string EventName;

        private AnimatorEventHandler m_AnimEventHandler;
        private UnityAction<AnimationEvent> m_OnAnimEvent;

        // --------------------------------------------------------------------

        StatusEffectApplyHealthDeltaAtAnimEvent()
        {
            m_OnAnimEvent = OnAnimationEvent;
        }

        // --------------------------------------------------------------------

        public override void StartEffect(StatusEffectHandler handler)
        {
            base.StartEffect(handler);

            if (!HasFinished)
            {
                m_AnimEventHandler = handler.gameObject.GetComponentInChildren<AnimatorEventHandler>();
                if (!m_AnimEventHandler)
                {
                    Debug.LogError("Tried to apply StatusEffectApplyHealthDeltaAtAnimEvent on an object without a AnimatorEventHandler on the Animator gameObject");
                    EndEffect();
                    return;
                }

                m_AnimEventHandler.OnEvent.AddListener(m_OnAnimEvent);
            }
        }

        // --------------------------------------------------------------------

        public override void EndEffect()
        {
            m_AnimEventHandler.OnEvent.RemoveListener(m_OnAnimEvent);

            base.EndEffect();
        }

        // --------------------------------------------------------------------

        private void OnAnimationEvent(AnimationEvent e)
        {
            if (e.stringParameter == EventName)
            {
                ApplyHealthDelta(m_HealthDeltaPerEvent);
            }
        }

    }
}
