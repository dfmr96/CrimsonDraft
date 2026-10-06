using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Timeline;

namespace HorrorEngine
{
    [TrackColor(0, 0.7f, 0.855f)]
    [TrackClipType(typeof(ActorMoveClip))]
    [TrackClipType(typeof(ActorRotateClip))]
    [TrackClipType(typeof(ActorTeleportClip))]
    [TrackBindingType(typeof(ActorHandle))]
    public class ActorMotionTrack : TrackAsset
    {
        protected override void OnCreateClip(TimelineClip clip)
        {
            base.OnCreateClip(clip);
            clip.duration = 1;
        }
    }
}