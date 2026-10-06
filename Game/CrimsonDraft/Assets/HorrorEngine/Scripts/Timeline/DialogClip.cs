using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HorrorEngine
{
    [System.Serializable]
    public class DialogClip : PlayableAsset, ITimelineClipAsset
    {
        public enum PauseType { OnStart, OnEnd };
        public PauseType Pause = PauseType.OnEnd;
        public DialogData Dialog;

        public DialogBehaviour m_Template = new DialogBehaviour();

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<DialogBehaviour>.Create(graph, m_Template);
            playable.GetBehaviour().Clip = this;
            return playable;
        }
    }

    public class DialogBehaviour : PlayableBehaviour
    {
        public DialogClip Clip;

        private bool m_Omitted;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (Application.isPlaying)
            {
                Debug.Assert(Clip.Dialog.IsValid(), "A dialog in the timeline is not valid");
                if (Clip.Dialog.IsValid())
                {
                    UIManager.PushAction(new UIStackedAction()
                    {
                        Action = () =>
                        {
                            m_Omitted = true;
                            if (this.HasFinished(playable, info) || Clip.Pause == DialogClip.PauseType.OnStart)
                            {
                                playable.GetGraph().GetRootPlayable(0).SetSpeed(1);
                            }
                        },
                        Name = "DialogBehaviour.OnBehaviourPlay (Reset Speed)"
                    });
                    UIManager.Get<UIDialog>().Show(Clip.Dialog);
                }

                if (Clip.Pause == DialogClip.PauseType.OnStart)
                    playable.GetGraph().GetRootPlayable(0).SetSpeed(0);
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (Application.isPlaying)
            {
                if (!m_Omitted && Clip.Pause == DialogClip.PauseType.OnEnd && this.HasFinished(playable, info))
                {
                    playable.GetGraph().GetRootPlayable(0).SetSpeed(0);
                }

            }
        }

    }
}