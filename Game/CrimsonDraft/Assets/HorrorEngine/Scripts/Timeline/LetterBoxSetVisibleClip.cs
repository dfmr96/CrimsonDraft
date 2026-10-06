using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HorrorEngine
{
    [System.Serializable]
    public class LetterBoxSetVisibleClip : PlayableAsset, ITimelineClipAsset
    {
        public bool Visible = true;
        public LetterBoxBehaviour m_Template = new LetterBoxBehaviour();

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<LetterBoxBehaviour>.Create(graph, m_Template);
            playable.GetBehaviour().Visible = Visible;
            return playable;
        }
    }

    public class LetterBoxBehaviour : PlayableBehaviour
    {
        public bool Visible;
        private float m_InitValue;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (Application.isPlaying)
            {
                m_InitValue = Visible ? 0 : 1;
                UIManager.Get<UILetterBox>().SetProgress(m_InitValue, Visible);
            }
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (Application.isPlaying)
            {
                base.ProcessFrame(playable, info, playerData);

                var duration = playable.GetDuration();
                var time = playable.GetTime();

                UIManager.Get<UILetterBox>().SetProgress(Mathf.Lerp(m_InitValue, Visible ? 1 : 0, (float)(time / duration)), Visible);
            }
        }
    }
}