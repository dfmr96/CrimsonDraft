using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HorrorEngine
{
    [System.Serializable]
    public class FadeOutClip : PlayableAsset, ITimelineClipAsset
    {
        public FadeBehaviour m_Template = new FadeBehaviour();

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<FadeBehaviour>.Create(graph, m_Template);
            playable.GetBehaviour().From = 0;
            playable.GetBehaviour().To = 1;
            return playable;
        }
    }
}