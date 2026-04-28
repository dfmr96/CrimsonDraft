using UnityEngine;
using UnityEditor;

namespace HorrorEngine
{
    [CustomEditor(typeof(Health))]
    public class HealthEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            Health health = target as Health;
            if (GUILayout.Button("Damage"))
            {
                health.TakeDamage(1);
            }
            if (GUILayout.Button("Kill"))
            {
                health.Kill();
            }
        }
    }
}