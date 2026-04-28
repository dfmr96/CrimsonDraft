using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HorrorEngine
{
    [System.Serializable]
    public class LookAtClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] ExposedReference<Lookable> m_Target;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<ActorLookAtClipBehaviour>.Create(graph);

            ActorLookAtClipBehaviour playableBehaviour = playable.GetBehaviour();
            playableBehaviour.Target = m_Target.Resolve(graph.GetResolver());

            return playable;
        }
    }
    [System.Serializable]
    public class ActorLookAtClipBehaviour : ActorClipBehaviour
    {
        public Lookable Target;

        private PlayerLookAtLookable m_LookAt;
        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            base.OnBehaviourPlay(playable, info);

            if (!Application.isPlaying)
                return;

            m_LookAt = m_Actor.GetComponentInChildren<PlayerLookAtLookable>(true);
            Debug.Assert(m_LookAt, "PlayerLookAtLookable not found in player");
            m_LookAt.enabled = true;
            m_LookAt.SetOverride(Target);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            base.OnBehaviourPause(playable, info);

            if (!Application.isPlaying)
                return;

            if (this.HasFinished(playable, info))
            {
                m_LookAt.SetOverride(null);
            }
        }

    }
}