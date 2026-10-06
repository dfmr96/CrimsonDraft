using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace HorrorEngine
{
    public interface IActorState
    {
        void StateEnter(IActorState fromState);

        void StateExit(IActorState intoState);
        void OnExitTransitionEnd(IActorState intoState);
        void OnEnterTransitionStart();
        void OnEnterTransitionEnd();

        void StateUpdate();

        void StateFixedUpdate();

        bool HasTag(string tag);
    }

    public class ActorStateBase : MonoBehaviour, IActorState
    {
        public virtual bool HasTag(string tag){ return false; }

        public virtual void OnEnterTransitionEnd(){}

        public virtual void OnEnterTransitionStart() { }
        public virtual void OnExitTransitionEnd(IActorState intoState){}

        public virtual void StateEnter(IActorState fromState){}

        public virtual void StateExit(IActorState intoState){}

        public virtual void StateFixedUpdate(){}

        public virtual void StateUpdate(){}
    }

    [System.Serializable]
    public class StateTransition
    {
        public ActorState FromState;
        public AnimatorStateHandle AnimationState;
        public float AnimationBlendTime = 0.25f;
    }

    public class ActorState : ActorStateBase, IResetable
    {
        [SerializeField] protected AnimatorStateHandle m_AnimationState;
        [SerializeField] protected float m_AnimationBlendTime = 0.25f;

        [FormerlySerializedAs("m_StateTransitions")]
        [SerializeField] protected StateTransition[] m_EnterStateTransitions;

        [SerializeField] protected AnimatorStateHandle m_ExitAnimationState;
        [SerializeField] protected float m_ExitAnimationBlendTime = 0.25f;
        [SerializeField] protected float m_ExitAnimationDuration = 0f;

        [SerializeField] private AnimatorEventCallback[] m_AnimEventCallbacks;
        [SerializeField] private AnimatorOverrideController m_AnimationOverride;

        [Tooltip("If this is set to true the ExitAnimation of the previous state will be ignored when entering this state")]
        public bool SkipPreviousExitAnimation;

        [Header("Events")]
        public UnityEvent OnStateEnter;
        public UnityEvent OnStateExit;

        public string[] Tags;

        private HashSet<string> m_HashedTags = new HashSet<string>();

        protected Actor Actor { get; private set; }
        protected bool TransitionFinished => m_TransitionTime <= 0;
        private float m_TransitionTime;

        private Coroutine m_EnterRoutine;
        private AnimatorEventHandler m_AnimEventHandler;
        private UnityAction<AnimationEvent> m_OnAnimEvent;
        protected AnimatorOverrider m_AnimOverrider;


        // --------------------------------------------------------------------

        public bool HasExitAnimation() { return m_ExitAnimationState && m_ExitAnimationDuration > 0f; }
        public float ExitDuration => m_ExitAnimationDuration;

        // --------------------------------------------------------------------

        protected virtual void Awake()
        {
            foreach (var tag in Tags)
                m_HashedTags.Add(tag);

            Actor = GetComponentInParent<Actor>();

            m_AnimEventHandler = Actor.MainAnimator.GetComponent<AnimatorEventHandler>();
            m_AnimOverrider = Actor.MainAnimator.GetComponent<AnimatorOverrider>();
            m_OnAnimEvent = OnAnimationEvent;
        }

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            if (m_AnimEventHandler)
            {
                m_AnimEventHandler.OnEvent.AddListener(m_OnAnimEvent);
                foreach (var callback in m_AnimEventCallbacks)
                {
                    callback.Clear();
                }
            }

            bool foundStateTransition = false;
            if (m_EnterStateTransitions != null && m_EnterStateTransitions.Length > 0)
            {
                foreach (var specific in m_EnterStateTransitions)
                {
                    if (specific.FromState == (ActorState)fromState)
                    {
                        Actor.MainAnimator.CrossFadeInFixedTime(specific.AnimationState ? specific.AnimationState.Hash : m_AnimationState.Hash, specific.AnimationBlendTime);
                        foundStateTransition = true;
                        m_TransitionTime = specific.AnimationBlendTime;
                        break;
                    }
                }
            }

            if (!foundStateTransition)
            {
                if (m_AnimationState)
                {
                    Actor.MainAnimator.CrossFadeInFixedTime(m_AnimationState.Hash, m_AnimationBlendTime, m_AnimationState.Layer);
                    m_TransitionTime = m_AnimationBlendTime;
                }
            }


            OnStateEnter?.Invoke();
        }

        // --------------------------------------------------------------------

        public override void StateUpdate()
        {
            if (m_TransitionTime > 0)
            {
                m_TransitionTime -= Time.deltaTime;
                if (TransitionFinished)
                    OnEnterTransitionEnd();
            }
        }

        // --------------------------------------------------------------------

        public override void StateFixedUpdate()
        {
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            if (m_EnterRoutine != null)
            {
                StopCoroutine(m_EnterRoutine);
                m_EnterRoutine = null;
            }

            if (m_ExitAnimationState && m_ExitAnimationDuration > 0f)
            {
                Actor.MainAnimator.CrossFadeInFixedTime(m_ExitAnimationState.Hash, m_ExitAnimationBlendTime);
            }

            OnStateExit?.Invoke();
        }

        // --------------------------------------------------------------------

        public override void OnEnterTransitionStart()
        {
            if (m_AnimationOverride)
                m_AnimOverrider.AddOverride(m_AnimationOverride);

        }

        // --------------------------------------------------------------------

        public override void OnEnterTransitionEnd()
        {

        }

        // --------------------------------------------------------------------

        public override void OnExitTransitionEnd(IActorState intoState)
        {

            if (m_AnimationOverride)
                m_AnimOverrider.RemoveOverride(m_AnimationOverride);

            if (m_AnimEventHandler)
                m_AnimEventHandler.OnEvent.RemoveListener(m_OnAnimEvent);
        }

        // --------------------------------------------------------------------

        protected void SetState(IActorState state, bool immediate = false)
        {
            Debug.Assert(state != null, "State can't be null");
            Actor.StateController.SetState(state, immediate);
        }

        // --------------------------------------------------------------------

        public virtual void OnReset()
        {
        }

        // --------------------------------------------------------------------

        public override bool HasTag(string tag) { return m_HashedTags.Contains(tag); }

        // --------------------------------------------------------------------

        private void OnAnimationEvent(AnimationEvent e)
        {
            foreach(var callback in m_AnimEventCallbacks)
            {
                callback.CheckAndInvoke(e);
            }
        }

    }
}