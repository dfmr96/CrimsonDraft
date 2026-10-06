using UnityEngine;
using UnityEngine.Playables;

namespace HorrorEngine
{
    public static class PlayableBehaviourExtensions
    {
        public static bool HasFinished(this PlayableBehaviour behaviour, Playable playable, FrameData info)
        {
            var duration = playable.GetDuration();
            var time = playable.GetTime();
            var count = time + info.deltaTime;

            return ((info.effectivePlayState == PlayState.Paused && count > duration) || Mathf.Approximately((float)time, (float)duration));
        }
    }
}