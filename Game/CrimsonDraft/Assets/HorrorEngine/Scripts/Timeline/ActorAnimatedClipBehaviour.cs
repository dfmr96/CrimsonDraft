
using UnityEngine.Playables;

namespace HorrorEngine
{
    public class ActorAnimatedClipBehaviour : ActorClipBehaviour
    {
        public AnimatorStateHandle AnimState;
        public float AnimFadeTime;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            base.OnBehaviourPlay(playable, info);


            if (AnimState != null)
            {
                m_Actor.MainAnimator.CrossFadeInFixedTime(AnimState.Hash, AnimFadeTime);
            }
        }
    }
}