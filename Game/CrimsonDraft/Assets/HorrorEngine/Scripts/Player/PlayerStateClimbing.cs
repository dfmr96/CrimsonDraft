using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class PlayerStateClimbing : ActorState
    {
        private static readonly int k_ClimbProgressHash = Animator.StringToHash("ClimbProgress");
        private static readonly int k_ClimbAttachmentHash = Animator.StringToHash("ClimbAttachment");
        private static readonly int k_ClimbExit = Animator.StringToHash("ClimbExit");
        private static readonly string k_EventClimbEnterEnd = "ClimbEnterEnd";
        private static readonly string k_EventClimbExitEnd = "ClimbExitEnd";
        private static readonly string k_EventClearSurfaceOverride = "ClearSurfaceOverride";

        [SerializeField] ActorState m_ExitState;
        [SerializeField] bool m_ShowDebug;

        public enum ClimbSubstate
        {
            Entry,
            Loop,
            Exit
        }

        private ClimbSubstate m_Substate;
        private Rigidbody m_Rigidbody;
        private Climbable m_Climbable;
        private Vector3 m_StartPos;
        private Vector3 m_EndPos;
        private Vector3 m_ExitPos;
        private Vector3 m_EntryDir;
        private Vector3 m_ExitDir;
        private float m_ExitDistance;
        private float m_Speed;
        private bool m_TopToBottom;
        private UnityAction<AnimationEvent> m_OnAnimatorEvent;
        private SurfaceDetector m_SurfaceDetector;
        private CharacterController m_CharacterCtrl;
        
        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();
            m_OnAnimatorEvent = OnAnimatorEvent;
            m_Rigidbody = GetComponentInParent<Rigidbody>();
            m_CharacterCtrl = GetComponentInParent<CharacterController>();
            m_SurfaceDetector = GetComponentInParent<SurfaceDetector>();
        }

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            // The climbable had to be set correctly from StartClimb()
            if (!m_Climbable)
            {
                SetState(m_ExitState);
                return;
            }

            Debug.Assert(m_Climbable.Setup, "The climbable setup hasn't been assigned");

            InitializeDirectionality();

            base.StateEnter(fromState);

            m_Substate = ClimbSubstate.Entry;


            m_CharacterCtrl.enabled = false;

            Actor.GetComponent<Health>().Invulnerable = true;
            Actor.MainAnimator.GetComponent<AnimatorEventHandler>().OnEvent.AddListener(m_OnAnimatorEvent);

            if (m_Climbable.Setup.Surface)
                m_SurfaceDetector.SetOverride(m_Climbable.Setup.Surface);
        }

        // --------------------------------------------------------------------

        private void InitializeDirectionality()
        {
            var top = m_Climbable.ExitTop.position;
            var bottom = m_Climbable.ExitBottom.position;
            
            float distToTop = Vector3.Distance(m_Rigidbody.position, top);
            float distToBottom = Vector3.Distance(m_Rigidbody.position, bottom);

            m_TopToBottom = distToTop < distToBottom;
            if (!m_TopToBottom)
            {
                m_StartPos = m_Climbable.ClimbBottom;
                m_EndPos = m_Climbable.ClimbTop;
                m_ExitPos = m_Climbable.ExitTop.position;
                m_ExitDir = m_Climbable.ExitTop.forward;
                m_AnimationState = m_Climbable.Setup.ClimbAnimation;
                m_Speed = m_Climbable.Setup.ClimbSpeed;
                m_ExitDistance = m_Climbable.Setup.ClimbExitDistance;
                m_EntryDir = m_Climbable.Setup.ClimbEntryDirection;
            }
            else
            {
                m_StartPos = m_Climbable.DropTop;
                m_EndPos = m_Climbable.DropBottom;
                m_ExitPos = m_Climbable.ExitBottom.position;
                m_ExitDir = m_Climbable.ExitBottom.forward;
                m_AnimationState = m_Climbable.Setup.DropAnimation;
                m_Speed = m_Climbable.Setup.DropSpeed;
                m_ExitDistance = m_Climbable.Setup.DropExitDistance;
                m_EntryDir = m_Climbable.Setup.DropEntryDirection;
                
            }

            m_EntryDir = m_Climbable.transform.TransformDirection(m_EntryDir);
        }

        // --------------------------------------------------------------------

        public override void StateFixedUpdate()
        {
            base.StateFixedUpdate();

            if (!m_Climbable)
                return;

            switch (m_Substate)
            {
                case ClimbSubstate.Entry:
                    UpdateEntry();
                    break;

                case ClimbSubstate.Loop:
                    UpdateLoop();
                    break;

                case ClimbSubstate.Exit:
                    UpdateExit();
                    break;
            }
        }

        // --------------------------------------------------------------------

        private void UpdateEntry()
        {
            float progressT = Actor.MainAnimator.GetFloat(k_ClimbProgressHash);
            float attachmentT = Actor.MainAnimator.GetFloat(k_ClimbAttachmentHash);

            Vector3 initialPos = m_Rigidbody.position;
            Vector3 progressPos = Vector3.MoveTowards(initialPos, m_EndPos, Time.deltaTime * m_Speed * progressT);
            Vector3 startPos = m_StartPos;
            startPos.y = progressPos.y;
            Vector3 pos = Vector3.Lerp(progressPos, startPos, attachmentT);

            if (m_ShowDebug)
                DebugUtils.DrawBox(pos, Quaternion.identity, Vector3.one, Vector3.one * 0.1f, Color.yellow, 10f);

            m_Rigidbody.MovePosition(pos);

            if (m_Climbable.Setup.FaceEntryDirection)
            {
                m_Rigidbody.MoveRotation(Quaternion.Slerp(m_Rigidbody.rotation, Quaternion.LookRotation(m_EntryDir), attachmentT));
            }
        }

        // --------------------------------------------------------------------

        private void UpdateLoop()
        {
            float progressT = Actor.MainAnimator.GetFloat(k_ClimbProgressHash);
            float attachmentT = Actor.MainAnimator.GetFloat(k_ClimbAttachmentHash);

            Vector3 initialPos = m_Rigidbody.position;
            Vector3 progressPos = Vector3.MoveTowards(initialPos, m_EndPos, Time.deltaTime * m_Speed * progressT);
            Vector3 exitPos = m_ExitPos;
            exitPos.y = progressPos.y;
            
            Vector3 pos = Vector3.Lerp(progressPos, exitPos, 1f - attachmentT);
            if (m_ShowDebug)
                DebugUtils.DrawBox(pos, Quaternion.identity, Vector3.one, Vector3.one * 0.1f, Color.red, 10f);
            m_Rigidbody.MovePosition(pos);

            var endPosY = (m_EndPos.y + (m_TopToBottom ? m_ExitDistance : -m_ExitDistance));
            float distToExit = endPosY - m_Rigidbody.position.y;

            if (m_TopToBottom)
                distToExit *= -1;

            if (distToExit <= m_ExitDistance)
            {
                m_Substate = ClimbSubstate.Exit;
                Actor.MainAnimator.SetTrigger(k_ClimbExit);
            }
        }

        // --------------------------------------------------------------------

        private void UpdateExit()
        {
            float progressT = Actor.MainAnimator.GetFloat(k_ClimbProgressHash);
            float attachmentT = 1f - Actor.MainAnimator.GetFloat(k_ClimbAttachmentHash);

            Vector3 initialPos = m_Rigidbody.position;
            Vector3 exitProgressPos = Vector3.MoveTowards(initialPos, m_EndPos, Time.deltaTime * m_Speed * progressT);
            Vector3 pos = Vector3.Lerp(exitProgressPos, m_ExitPos, attachmentT);
            if (m_ShowDebug)
                DebugUtils.DrawBox(pos, Quaternion.identity, Vector3.one, Vector3.one * 0.1f, Color.blue, 10f);

            m_Rigidbody.MovePosition(pos);

            if (m_Climbable.Setup.FaceExitDirection)
            {
                m_Rigidbody.MoveRotation(Quaternion.Slerp(m_Rigidbody.rotation, Quaternion.LookRotation(m_ExitDir), attachmentT));
            }
        }

        // --------------------------------------------------------------------

        private void OnAnimatorEvent(AnimationEvent e)
        {
            if (e.stringParameter == k_EventClimbEnterEnd)
            {
                m_Substate = m_Climbable.Setup.Loop ? ClimbSubstate.Loop : ClimbSubstate.Exit;
            }
            else if (e.stringParameter == k_EventClimbExitEnd)
            {
                SetState(m_ExitState);
            }
            else if (e.stringParameter == k_EventClearSurfaceOverride)
            {
                m_SurfaceDetector.ClearOverride();
            }
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            base.StateExit(intoState);

            Actor.GetComponent<Health>().Invulnerable = false;
            Actor.MainAnimator.GetComponent<AnimatorEventHandler>().OnEvent.RemoveListener(m_OnAnimatorEvent);

            m_CharacterCtrl.enabled = true;

            m_SurfaceDetector.ClearOverride();
            m_Climbable = null;
        }

        // --------------------------------------------------------------------

        public void StartClimb(Climbable climbable)
        {
            m_Climbable = climbable;
            SetState(this);
        }
    }

}