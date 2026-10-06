using UnityEditor;
using UnityEngine;

namespace HorrorEngine
{
    [CustomPropertyDrawer(typeof(RankingData))]
    public class RankingDataEditor : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float time = property.FindPropertyRelative(nameof(RankingData.TimeThresholdInSeconds)).floatValue;
            RankingRankData rank = (RankingRankData)property.FindPropertyRelative(nameof(RankingData.Rank)).objectReferenceValue;
            string rankName = rank != null ? rank.Name : "NONE";
            label = new GUIContent($"{rankName} ({RankingUtils.GetFormattedTime(time)})");
            EditorGUI.PropertyField(position, property, label, true);

        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property);
        }
    }
}


