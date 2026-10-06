using UnityEngine;
using UnityEditor;
using System;

namespace HorrorEngine
{
    public class NewCharacterWizard : EditorWindow
    {
        [SerializeField] private GameObject m_CharacterPrefab;
        private GameObject m_NewCharacterModel;
        private string m_OutputFolder = "Assets/";

        [MenuItem("Horror Engine/Wizards/New Character")]
        static void Init()
        {
            NewCharacterWizard window = (NewCharacterWizard)EditorWindow.GetWindow(typeof(NewCharacterWizard));
            window.titleContent = new GUIContent("New Character");
            window.Show();
        }

        void OnGUI()
        {
            m_CharacterPrefab = EditorGUILayout.ObjectField("Character Prefab", m_CharacterPrefab, typeof(GameObject), false) as GameObject;
            m_NewCharacterModel = EditorGUILayout.ObjectField("New Character Model", m_NewCharacterModel, typeof(GameObject), false) as GameObject;
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("New Prefab Output", m_OutputFolder);
            if (GUILayout.Button("Search", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("Select output folder", m_OutputFolder, "");
                m_OutputFolder = path.Substring(path.IndexOf("Assets/")) + "/";
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.Separator();
            if (m_CharacterPrefab && m_NewCharacterModel && !string.IsNullOrEmpty(m_OutputFolder))
            {
                if (GUILayout.Button("Create"))
                {
                    GameObject objSource = (GameObject)PrefabUtility.InstantiatePrefab(m_CharacterPrefab);

                    try
                    {
                        Actor actor = objSource.GetComponentInChildren<Actor>();
                        Animator animator = actor.MainAnimator;

#if UNITY_2022_3_OR_NEWER
                    GameObject newModel = (GameObject)PrefabUtility.InstantiatePrefab(m_NewCharacterModel, animator.transform.parent);
#else
                        GameObject newModel = Instantiate(m_NewCharacterModel, animator.transform.parent);
#endif

                        Animator newAnimator = newModel.GetComponent<Animator>();
                        newAnimator.applyRootMotion = false;
                        newAnimator.runtimeAnimatorController = animator.runtimeAnimatorController;
                        actor.MainAnimator = newAnimator;

                        var components = animator.GetComponents<Component>();
                        foreach (var cmp in components)
                        {
                            UnityEditorInternal.ComponentUtility.CopyComponent(cmp);
                            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(newModel);
                        }

#if UNITY_2022_3_OR_NEWER
                    DestroyImmediate(animator.gameObject);
#else
                        animator.gameObject.SetActive(false);
#endif

                        GameObject newPrefab = PrefabUtility.SaveAsPrefabAsset(objSource, m_OutputFolder + "/" + m_NewCharacterModel.name + "_Character.prefab");

                        DestroyImmediate(objSource);

                        Selection.activeObject = newPrefab;
                    }
                    catch(Exception e)
                    {
                        Debug.LogError("There was an error during the character creation: " + e.Message);
                        DestroyImmediate(objSource);
                    }

                    Close();
                }
            }
        }
    }
}