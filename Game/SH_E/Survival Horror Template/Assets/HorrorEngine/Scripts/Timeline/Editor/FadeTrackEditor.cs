using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace HorrorEngine
{
    [CustomTimelineEditor(typeof(FadeTrack))]
    public class FadeTrackEditor : TrackEditor
    {
        [SerializeField] Texture2D m_Icon;

        public override TrackDrawOptions GetTrackOptions(TrackAsset track, Object binding)
        {
            var options = base.GetTrackOptions(track, binding);
            options.icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HorrorEngine/Scripts/Editor/Resources/FadeIcon.png");
            return options;
        }
    }
}