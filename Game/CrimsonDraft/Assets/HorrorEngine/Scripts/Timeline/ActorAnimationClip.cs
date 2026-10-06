using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HorrorEngine
{
    [System.Serializable]
    public class ActorAnimationClip : PlayableAsset, ITimelineClipAsset
    {
        public AnimatorStateHandle State;
        public float StateFadeTime = 0.1f;
        public AnimatorStateHandle ExitState;
        public float ExitStateFadeTime = 0.1f;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<ActorAnimationClipBehaviour>.Create(graph);

            ActorAnimationClipBehaviour playableBehaviour = playable.GetBehaviour();
            playableBehaviour.State = State;
            playableBehaviour.StateFadeTime = StateFadeTime;
            playableBehaviour.ExitState = ExitState;
            playableBehaviour.ExitStateFadeTime = ExitStateFadeTime;

            return playable;
        }
    }

    [System.Serializable]
    public class ActorAnimationClipBehaviour : ActorClipBehaviour
    {
        public AnimatorStateHandle State;
        public float StateFadeTime;
        public AnimatorStateHandle ExitState;
        public float ExitStateFadeTime;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            base.OnBehaviourPlay(playable, info);

            if (!Application.isPlaying)
                return;

            if (State)
                m_Actor.MainAnimator.CrossFadeInFixedTime(State.Hash, StateFadeTime);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            base.OnBehaviourPause(playable, info);

            if (!Application.isPlaying)
                return;

            if (m_Actor)
            {
                if (this.HasFinished(playable, info) && ExitState)
                {
                    m_Actor.MainAnimator.CrossFadeInFixedTime(ExitState.Hash, ExitStateFadeTime);
                }
            }
        }
    }
}