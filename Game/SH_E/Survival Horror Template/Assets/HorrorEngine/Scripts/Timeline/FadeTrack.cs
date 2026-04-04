using UnityEngine.Timeline;

namespace HorrorEngine
{
    [TrackColor(0.855f, 0.2f, 0.855f)]
    [TrackClipType(typeof(FadeInClip))]
    [TrackClipType(typeof(FadeOutClip))]
    public class FadeTrack : TrackAsset
    {
        protected override void OnCreateClip(TimelineClip clip)
        {
            base.OnCreateClip(clip);
            clip.duration = 1;
        }
    }
}