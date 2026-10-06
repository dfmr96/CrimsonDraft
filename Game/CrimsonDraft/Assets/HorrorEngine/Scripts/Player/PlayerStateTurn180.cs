using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public enum TurnDirection
    {
        Left,
        Right
    }
    public class PlayerStateTurn180 : ActorStateWithDuration
    {
        [SerializeField] private TurnDirection m_RotationDirection;
        private PlayerMovement m_Movement;

        protected override void Awake()
        {
            base.Awake();

            m_Movement = GetComponentInParent<PlayerMovement>();
        }

        public override void StateEnter(IActorState fromState)
        {
            base.StateEnter(fromState);

            m_Movement.enabled = false;
        }

        public override void StateFixedUpdate()
        {
            base.StateFixedUpdate();

            if (m_Duration > 0)
            {
                m_Movement.Rotate(m_RotationDirection == TurnDirection.Left ? -1 : 1, 180f / m_Duration);
            }
        }

        public override void StateExit(IActorState intoState)
        {
            m_Movement.enabled = true;

            base.StateExit(intoState);
        }
    }
}