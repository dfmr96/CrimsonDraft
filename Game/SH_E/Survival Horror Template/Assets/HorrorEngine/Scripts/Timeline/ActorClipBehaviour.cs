using UnityEngine;
using UnityEngine.Playables;

namespace HorrorEngine
{
    [System.Serializable]
    public class ActorClipBehaviour : PlayableBehaviour
    {
        protected Actor m_Actor;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            base.OnBehaviourPlay(playable, info);

            if (Application.isPlaying)
            {
                ActorHandle handle = info.output.GetUserData() as ActorHandle;
                Debug.Assert(handle, $"Actor handle has not been assigned on the track ({GetType()})");
                if (!handle)
                    return;

                Actor[] actors = GameObject.FindObjectsByType<Actor>(FindObjectsSortMode.None); // Todo - Ideally, all actors should be registered somewhere
                foreach (var actor in actors)
                {
                    if (actor.Handle == handle)
                    {
                        m_Actor = actor;
                        break;
                    }
                }

                Debug.Assert(m_Actor, $"Actor with handle {handle} couldn't be found by ActorMoveClip");
            }
        }
    }
}