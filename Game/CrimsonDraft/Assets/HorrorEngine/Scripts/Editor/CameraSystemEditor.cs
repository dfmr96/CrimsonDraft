using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace HorrorEngine
{
    [CustomEditor(typeof(CameraSystem))]
    public class CameraSystemEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            List<CameraPOV> cams = CameraStack.Instance.GetCameras();
            foreach(var cam in cams)
            {
                if (GUILayout.Button(cam.name))
                {
                    EditorGUIUtility.PingObject(cam.gameObject);
                }
            }
        }
    }
}