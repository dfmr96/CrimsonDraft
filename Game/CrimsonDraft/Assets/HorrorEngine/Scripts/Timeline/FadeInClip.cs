using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HorrorEngine
{
    [System.Serializable]
    public class FadeInClip : PlayableAsset, ITimelineClipAsset
    {
        public FadeBehaviour m_Template = new FadeBehaviour();

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<FadeBehaviour>.Create(graph, m_Template);
            playable.GetBehaviour().From = 1;
            playable.GetBehaviour().To = 0;
            return playable;
        }
    }

    public class FadeBehaviour : PlayableBehaviour
    {
        public float From;
        public float To;
        
        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (Application.isPlaying)
            {
                UIManager.Get<UIFade>().Set(From);
            }
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (Application.isPlaying)
            {
                base.ProcessFrame(playable, info, playerData);

                var duration = playable.GetDuration();
                var time = playable.GetTime();


                UIManager.Get<UIFade>().Set(Mathf.Lerp(From, To, (float)(time / duration)));
            }
        }
    }
}