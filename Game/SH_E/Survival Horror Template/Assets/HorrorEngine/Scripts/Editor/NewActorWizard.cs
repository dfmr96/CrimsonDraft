using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace HorrorEngine
{
    public class NewActorWizard : EditorWindow
    {
        [SerializeField] private GameObject m_ActorPrefab;
        private GameObject m_NewActorModel;
        private string m_OutputFolder = "Assets/";

        [MenuItem("Horror Engine/Wizards/New Actor")]
        static void Init()
        {
            NewActorWizard window = (NewActorWizard)EditorWindow.GetWindow(typeof(NewActorWizard));
            window.titleContent = new GUIContent("New Actor");
            window.Show();
        }

        void OnGUI()
        {
            m_ActorPrefab = EditorGUILayout.ObjectField("Actor Prefab", m_ActorPrefab, typeof(GameObject), false) as GameObject;
            m_NewActorModel = EditorGUILayout.ObjectField("New Actor Model", m_NewActorModel, typeof(GameObject), false) as GameObject;
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("New Prefab Output", m_OutputFolder);
            if (GUILayout.Button("Search", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("Select output folder", m_OutputFolder, "");
                m_OutputFolder = path.Substring(path.IndexOf("Assets/")) + "/";
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.Separator();
            if (m_ActorPrefab && m_NewActorModel && !string.IsNullOrEmpty(m_OutputFolder))
            {
                if (GUILayout.Button("Create"))
                {
                    GameObject objSource = (GameObject)PrefabUtility.InstantiatePrefab(m_ActorPrefab);

                    try
                    {
                        Actor actor = objSource.GetComponentInChildren<Actor>();
                        Animator animator = actor.MainAnimator;

#if UNITY_2022_3_OR_NEWER
                    GameObject newModel = (GameObject)PrefabUtility.InstantiatePrefab(m_NewActorModel, animator.transform.parent);
#else
                        GameObject newModel = Instantiate(m_NewActorModel, animator.transform.parent);
#endif

                        Animator newAnimator = newModel.GetComponent<Animator>();
                        if (!newAnimator)
                        {
                            newAnimator = newModel.AddComponent<Animator>();
                        }
                        newAnimator.applyRootMotion = false;
                        newAnimator.runtimeAnimatorController = animator.runtimeAnimatorController;
                        actor.MainAnimator = newAnimator;

                        var components = animator.GetComponents<Component>();
                        foreach (var cmp in components)
                        {
                            UnityEditorInternal.ComponentUtility.CopyComponent(cmp);
                            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(newModel);
                        }

                        var sockets = animator.GetComponentsInChildren<Socket>(); // Do this before deactivation or destruction of the old animator

#if UNITY_2022_3_OR_NEWER
                        DestroyImmediate(animator.gameObject);
#else
                        animator.gameObject.SetActive(false);
#endif

                        GameObject newPrefab = PrefabUtility.SaveAsPrefabAsset(objSource, m_OutputFolder + "/" + m_NewActorModel.name + "_Actor.prefab");

                        DestroyImmediate(objSource);

                        Selection.activeObject = newPrefab;
                        AssetDatabase.OpenAsset(newPrefab);

                        
                        if (sockets.Length > 0)
                        {
                            List<SetupActorSocketsWizard.SocketWizardEntry> socketEntries = SetupActorSocketsWizard.GetSocketsForSetup(sockets, true);
                            if (socketEntries.Count > 0)
                                SetupActorSocketsWizard.InitWithSockets(socketEntries);
                        }
                    }
                    catch(Exception e)
                    {
                        Debug.LogError("There was an error during the actor creation: " + e.Message);
                        DestroyImmediate(objSource);
                    }

                    Close();
                }
            }
        }
    }
}