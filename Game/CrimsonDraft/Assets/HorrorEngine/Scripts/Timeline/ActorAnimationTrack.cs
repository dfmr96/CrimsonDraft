using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HorrorEngine
{
    [TrackColor(0.7f, 0.855f, 0)]
    [TrackClipType(typeof(ActorAnimationClip))]
    [TrackBindingType(typeof(ActorHandle))]
    public class ActorAnimationTrack : TrackAsset
    {
        protected override void OnCreateClip(TimelineClip clip)
        {
            base.OnCreateClip(clip);
            clip.duration = 1;
        }

        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            foreach (TimelineClip clip in m_Clips)
            {
                ActorAnimationClip animClip = clip.asset as ActorAnimationClip;

                clip.displayName = animClip.State ? animClip.State.StateName : "[None]";
            }

            return base.CreateTrackMixer(graph, go, inputCount);
        }
    }
}
