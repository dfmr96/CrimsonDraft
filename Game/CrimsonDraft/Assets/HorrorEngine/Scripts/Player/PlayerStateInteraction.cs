using System.Collections;
using UnityEngine;

namespace HorrorEngine
{
    public class PlayerStateInteraction : ActorState
    {
        [SerializeField] InteractionColliderDetector m_Detector;
        [SerializeField] ActorState m_ExitState;

        private Interactive m_Interactive;
        private float m_Time;
        private bool m_Interacted;
        private float m_InitialDelay;
        private float m_InteractionDuration;

        private Vector3 m_DirToInteractor;
        private float m_RotationAngle;

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            m_Interactive = m_Detector.FocusedInteractive;
            if (!m_Interactive)
            {
                SetState(m_ExitState);
                return;
            }

            m_DirToInteractor =  m_Interactive.transform.position - Actor.transform.position;
            m_DirToInteractor.y = 0;
            m_DirToInteractor.Normalize();
            m_RotationAngle = Vector3.Angle(m_DirToInteractor, Actor.transform.forward);

            m_AnimationState = m_Interactive.Data ? m_Interactive.Data.AnimState : null;

            base.StateEnter(fromState);

            m_Time = 0f;
            m_Interacted = false;

            m_InitialDelay = m_Interactive.Data ? m_Interactive.Data.InteractionDelay : 0f;
            m_InteractionDuration = m_Interactive.Data ? m_Interactive.Data.InteractionDuration : 0f;

            UIManager.Get<UIInputListener>().AddBlockingContext(this);

            Debug.Assert(m_InitialDelay <= m_InteractionDuration, $"Incorrect interaction data : m_InitialDelay has to be greater than m_InteractionDuration");
        }

        // --------------------------------------------------------------------

        public override void StateUpdate()
        {
            base.StateUpdate();

            m_Time += Time.deltaTime;

            if (m_Interactive.Data && m_Interactive.Data.RotateToInteractive)
            {
                Actor.transform.rotation = Quaternion.RotateTowards(Actor.transform.rotation, Quaternion.LookRotation(m_DirToInteractor), m_RotationAngle * (Time.deltaTime / m_InitialDelay));
            }

            if (!m_Interacted && m_Time > m_InitialDelay)
            {
                Interact();
            }
            // Intentionally let 1 frame pass between interaction and state change
            // to avoid the activation of the interactor and the immediate re-interaction
            else if (m_Time > m_InteractionDuration) 
            {
                SetState(m_ExitState);
            }
        }

        // --------------------------------------------------------------------

        private void Interact()
        {
            m_Interactive.Interact(GetComponentInParent<PlayerInteractor>());
            m_Interacted = true;
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            UIManager.Get<UIInputListener>().RemoveBlockingContext(this);

            base.StateExit(intoState);

            m_Interactive?.Finish();
        }
    }
}