using UnityEngine.Timeline;

namespace HorrorEngine
{
    [TrackColor(0.855f, 0.855f, 0.2f)]
    [TrackClipType(typeof(InstantiateClip))]
    public class InstantiationTrack : TrackAsset
    {
        protected override void OnCreateClip(TimelineClip clip)
        {
            base.OnCreateClip(clip);
            clip.duration = 1;
        }
    }
}