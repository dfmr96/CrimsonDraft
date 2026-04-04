using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HorrorEngine
{
    [System.Serializable]
    public class DialogLineClip : PlayableAsset, ITimelineClipAsset
    {
        public DialogLine DialogLine;
        public bool PauseGame;
        public bool HideOnEnd = true;

        public DialogLineBehaviour m_Template = new DialogLineBehaviour();

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<DialogLineBehaviour>.Create(graph, m_Template);
            playable.GetBehaviour().Clip = this;
            return playable;
        }
    }

    public class DialogLineBehaviour : PlayableBehaviour
    {
        public DialogLineClip Clip;
        public DialogData Data = new DialogData();

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (!Application.isPlaying) 
                return;
            
            Data.PauseGame = Clip.PauseGame;
            Data.CanBeDismissed = false;

            DialogLine[] lines = { Clip.DialogLine };
            Data.SetLines(lines);

            UIManager.Get<UIDialog>().Show(Data);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (!Application.isPlaying) 
                return;

            if (this.HasFinished(playable, info) && Clip.HideOnEnd)
            {
                UIManager.Get<UIDialog>().Hide();
            }
        }

    }
}