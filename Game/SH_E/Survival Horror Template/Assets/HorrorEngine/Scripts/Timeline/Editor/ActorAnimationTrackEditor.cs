using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace HorrorEngine
{
    [CustomTimelineEditor(typeof(ActorAnimationTrack))]
    public class ActorAnimationTrackEditor : TrackEditor
    {
        [SerializeField] Texture2D m_Icon;

        public override TrackDrawOptions GetTrackOptions(TrackAsset track, Object binding)
        {
            var options = base.GetTrackOptions(track, binding);
            options.icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HorrorEngine/Scripts/Editor/Resources/AnimIcon.png");
            return options;
        }
    }
}