using UnityEngine;

namespace HorrorEngine
{
    public class ActorStateRandomizer : ActorStateBase
    {
        [SerializeField] ActorState[] States;

        private ActorStateController m_StateController;

        private void Awake()
        {
            m_StateController = GetComponentInParent<ActorStateController>();
        }

        public override void StateEnter(IActorState fromState)
        {
            Debug.Assert(States.Length > 0, $"ActorStateRandom {name} had no states to randomize");
            m_StateController.SetState(States[Random.Range(0, States.Length)]);
        }

    }
}