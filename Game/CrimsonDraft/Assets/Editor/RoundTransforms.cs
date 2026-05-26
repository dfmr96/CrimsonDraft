using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class RoundTransforms
{
    [MenuItem("Tools/Round All Transforms to 2 Decimals")]
    static void RoundAll()
    {
        var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var modified = new List<Transform>();

        Undo.SetCurrentGroupName("Round Transforms to 2 Decimals");
        int group = Undo.GetCurrentGroup();

        foreach (var t in transforms)
        {
            Vector3 pos   = Round(t.localPosition);
            Vector3 rot   = Round(t.localEulerAngles);
            Vector3 scale = Round(t.localScale);

            if (pos != t.localPosition || rot != t.localEulerAngles || scale != t.localScale)
            {
                Undo.RecordObject(t, "Round Transform");
                t.localPosition    = pos;
                t.localEulerAngles = rot;
                t.localScale       = scale;
                modified.Add(t);
            }
        }

        Undo.CollapseUndoOperations(group);
        Debug.Log($"[RoundTransforms] Rounded {modified.Count} transforms.");
    }

    static Vector3 Round(Vector3 v) =>
        new Vector3(
            Mathf.Round(v.x * 100f) / 100f,
            Mathf.Round(v.y * 100f) / 100f,
            Mathf.Round(v.z * 100f) / 100f
        );
}
