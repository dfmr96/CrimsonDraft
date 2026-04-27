#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CrimsonDraft.Editor
{
    public static class AnimatorIKPassEnabler
    {
        [MenuItem("Tools/CrimsonDraft/Enable IK Pass - PlayerAnimator Base Layer")]
        public static void EnablePlayerAnimatorIKPass()
        {
            const string path = "Assets/Animations/Player/PlayerAnimator.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                Debug.LogError($"[AnimatorIKPassEnabler] Controller not found at {path}");
                return;
            }

            var layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[AnimatorIKPassEnabler] IK Pass enabled on Base Layer.");
        }
    }
}
#endif
