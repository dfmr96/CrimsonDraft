using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "InteractionData", menuName = "Horror Engine/Interaction")]
    public class InteractionData : ScriptableObject
    {
        public LocalizableText Prompt;

        // TODO - Remove eventually
        [FormerlySerializedAs("Prompt")]
        [HideInInspector]
        public string Prompt_DEPRECATED;
        // -------------

        public Sprite Icon;
        public AnimatorStateHandle AnimState;
        [FormerlySerializedAs("InteractionTime")]
        public float InteractionDuration;
        public float InteractionDelay;
        public bool RotateToInteractive = true;

        private void OnValidate()
        {
            if (InteractionDuration < InteractionDelay)
                InteractionDuration = InteractionDelay;
        }

        
#if UNITY_EDITOR
        public void EditorOnly_MigrateUnlocalizedData()
        {
            bool dirty = false;
            if (!string.IsNullOrEmpty(Prompt_DEPRECATED))
            {
                Prompt.Unlocalized = Prompt_DEPRECATED;
                Prompt_DEPRECATED = "";
                dirty = true;
            }

            if (dirty)
            {
                var context = this;
                EditorApplication.delayCall += () => { EditorUtility.SetDirty(context); };
            }

        }
#endif
    }
}