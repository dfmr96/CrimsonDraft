using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

namespace HorrorEngine
{
    [TrackColor(0.855f, 0.2f, 0.855f)]
    [TrackClipType(typeof(LetterBoxSetVisibleClip))]
    public class LetterBoxTrack : TrackAsset
    {
        protected override void OnCreateClip(TimelineClip clip)
        {
            base.OnCreateClip(clip);
            clip.duration = 1;
        }
    }
}