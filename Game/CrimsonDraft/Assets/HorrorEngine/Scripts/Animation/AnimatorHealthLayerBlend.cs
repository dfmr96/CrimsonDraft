using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace HorrorEngine
{
    [Serializable]
    public class StateHealthLayerBlend
    {
        public ActorState State;
        public float Scalar;
    }

    public class AnimatorHealthLayerBlend : AnimatorLayerBlend
    {
        [SerializeField] private AnimationCurve m_NormalizedHealthOverWeightCurve;
        [SerializeField] private StateHealthLayerBlend[] m_StateScalars;
        [SerializeField] private float m_DefaultScalar = 0;

        private Dictionary<IActorState, float> m_StateScaleHash = new Dictionary<IActorState, float>();

        private ActorStateController m_StateController;
        private Health m_Health;
        private AnimatorOverrider m_AnimatorOverrider;
        
        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            foreach (var stateEntry in m_StateScalars)
            {
                m_StateScaleHash.Add(stateEntry.State, stateEntry.Scalar);
            }

            m_StateController = GetComponentInParent<ActorStateController>();
            m_Health = GetComponentInParent<Health>();

            m_StateController.OnStateChanged.AddListener(OnStateChanged);
            m_Health.OnHealthAltered.AddListener(OnHealthAltered);
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            m_StateController.OnStateChanged.RemoveListener(OnStateChanged);
            m_Health.OnHealthAltered.RemoveListener(OnHealthAltered);
        }

        // --------------------------------------------------------------------

        private void OnStateChanged(IActorState fromState, IActorState toState)
        {
            Trigger();
        }

        // --------------------------------------------------------------------

        private void OnHealthAltered(float prevHealth, float newHealth)
        {
            Trigger();
        }

        // --------------------------------------------------------------------

        public override void Trigger()
        {
            var state = m_StateController.CurrentState;
            float scale = m_StateScaleHash.ContainsKey(state) ? m_StateScaleHash[state] : m_DefaultScalar;
            m_ToWeight = m_NormalizedHealthOverWeightCurve.Evaluate(m_Health.Normalized) * scale;

            base.Trigger();
        }
    }
}