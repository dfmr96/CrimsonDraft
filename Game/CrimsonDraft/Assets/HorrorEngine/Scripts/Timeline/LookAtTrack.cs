using UnityEngine.Timeline;

namespace HorrorEngine
{
    [TrackColor(0.2f, 0.855f, 0.2f)]
    [TrackClipType(typeof(LookAtClip))]
    [TrackBindingType(typeof(ActorHandle))]
    public class LookAtTrack : TrackAsset
    {
        protected override void OnCreateClip(TimelineClip clip)
        {
            base.OnCreateClip(clip);
            clip.duration = 1;
        }
        
    }
}