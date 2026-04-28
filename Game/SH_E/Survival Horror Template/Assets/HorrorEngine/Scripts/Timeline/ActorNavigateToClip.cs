using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HorrorEngine
{
   
    [System.Serializable]
    public class ActorNavigateToClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] ExposedReference<Transform> m_Transform;
        [SerializeField] float m_Speed = 3;
        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<ActorNavigateToClipBehaviour>.Create(graph);

            ActorNavigateToClipBehaviour playableBehaviour = playable.GetBehaviour();
            playableBehaviour.ToTransform = m_Transform.Resolve(graph.GetResolver());
            playableBehaviour.Speed = m_Speed;
            return playable;
        }
    }

    [System.Serializable]
    public class ActorNavigateToClipBehaviour : ActorAnimatedClipBehaviour
    {
        public Transform ToTransform;
        public float Speed;

        private NavMeshAgent m_Agent;
        private Rigidbody m_Rigidbody;
        private float m_CachedSpeed;
        private CharacterController m_CharacterCtrl;
        // --------------------------------------------------------------------

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            base.OnBehaviourPlay(playable, info);

            m_Agent = m_Actor.GetComponent<NavMeshAgent>();
            m_Rigidbody = m_Actor.GetComponent<Rigidbody>();
            m_CharacterCtrl = m_Actor.GetComponent<CharacterController>();
            if (m_Agent)
            {
                m_Agent.enabled = true;
                m_Agent.destination = ToTransform.position;
                if (m_Rigidbody)
                    m_Rigidbody.isKinematic = true;
                if (m_CharacterCtrl)
                    m_CharacterCtrl.enabled = false;
                float duration = (float)playable.GetDuration();
                if (duration > 0f)
                {
                    m_CachedSpeed = m_Agent.speed;
                    m_Agent.speed = Speed;
                }
            }
        }

        // --------------------------------------------------------------------

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            base.OnBehaviourPause(playable, info);

            if (!Application.isPlaying)
                return;

            if (this.HasFinished(playable, info))
            {
                if (m_Agent)
                {
                    m_Agent.enabled = false;
                    m_Agent.speed = m_CachedSpeed;
                    if (m_Rigidbody)
                        m_Rigidbody.isKinematic = false;
                    if (m_CharacterCtrl)
                        m_CharacterCtrl.enabled = true;
                }
            }
        }
    }
}