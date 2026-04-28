using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HorrorEngine
{
    [System.Serializable]
    public class LetterBoxSetVisibleClip : PlayableAsset, ITimelineClipAsset
    {
        public bool Visible = true;
        public bool Immediate = false;
        public LetterBoxBehaviour m_Template = new LetterBoxBehaviour();

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<LetterBoxBehaviour>.Create(graph, m_Template);
            playable.GetBehaviour().Visible = Visible;
            playable.GetBehaviour().Immediate = Immediate;
            return playable;
        }
    }

    public class LetterBoxBehaviour : PlayableBehaviour
    {
        public bool Visible;
        public bool Immediate;
        private float m_InitValue;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (Application.isPlaying)
            {
                if (!Immediate)
                {
                    m_InitValue = Visible ? 0 : 1;
                    UIManager.Get<UILetterBox>().SetProgress(m_InitValue, Visible);
                }
                else
                {
                    m_InitValue = Visible ? 1 : 0;
                    UIManager.Get<UILetterBox>().SetProgress(m_InitValue, Visible);
                }
            }
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (Application.isPlaying && !Immediate)
            {
                base.ProcessFrame(playable, info, playerData);

                var duration = playable.GetDuration();
                var time = playable.GetTime();

                UIManager.Get<UILetterBox>().SetProgress(Mathf.Lerp(m_InitValue, Visible ? 1 : 0, (float)(time / duration)), Visible);
            }
        }
    }
}