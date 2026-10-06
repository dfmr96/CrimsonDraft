using UnityEngine;
using UnityEditor;

namespace HorrorEngine
{
    public class NewWeaponWizard : EditorWindow
    {
        [SerializeField] private Weapon m_WeaponPrefab;
        private GameObject m_NewWeaponModel;
        private string m_OutputFolder = "Assets/";

        private string m_Name = "NewWeapon";
        private bool m_CopyWeaponData = true;
        private bool m_CopyExaminationPrefab = true;

        [MenuItem("Horror Engine/Wizards/New Weapon")]
        static void Init()
        {
            NewWeaponWizard window = (NewWeaponWizard)EditorWindow.GetWindow(typeof(NewWeaponWizard));
            window.titleContent = new GUIContent("New Weapon");
            window.Show();
        }

        void OnGUI()
        {
            m_WeaponPrefab = EditorGUILayout.ObjectField("Base Weapon Prefab", m_WeaponPrefab, typeof(Weapon), false) as Weapon;
            EditorGUILayout.Separator();

            m_Name = EditorGUILayout.TextField("Name", m_Name);
            m_NewWeaponModel = EditorGUILayout.ObjectField("New Weapon Model", m_NewWeaponModel, typeof(GameObject), false) as GameObject;
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("New Prefab Output", m_OutputFolder);
            if (GUILayout.Button("Search", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("Select output folder", m_OutputFolder, "");
                m_OutputFolder = path.Substring(path.IndexOf("Assets/")) + "/";
            }
            GUILayout.EndHorizontal();

            m_CopyWeaponData = EditorGUILayout.Toggle("Copy Item Data", m_CopyWeaponData);
            m_CopyExaminationPrefab = EditorGUILayout.Toggle("Copy Examination Prefab", m_CopyExaminationPrefab);

            EditorGUILayout.Separator();
            if (m_WeaponPrefab && m_NewWeaponModel && !string.IsNullOrEmpty(m_OutputFolder))
            {
                if (GUILayout.Button("Create"))
                {
                    Weapon objSource = (Weapon)PrefabUtility.InstantiatePrefab(m_WeaponPrefab);

                    GameObject oldModel = objSource.Visuals;
#if UNITY_2022_3_OR_NEWER
                    GameObject newModel = (GameObject)PrefabUtility.InstantiatePrefab(m_NewWeaponModel, oldModel.transform.parent);
#else
                    GameObject newModel = Instantiate(m_NewWeaponModel, oldModel.transform.parent);
#endif
                    objSource.Visuals = newModel;

                    string outName = string.IsNullOrEmpty(m_Name) ? m_NewWeaponModel.name : m_Name;

                    // Copy all sockets
                    Socket[] sockets = oldModel.GetComponentsInChildren<Socket>();
                    foreach (var socket in sockets)
                    {
                        GameObject newSocket = new GameObject(socket.name);
                        UnityEditorInternal.ComponentUtility.CopyComponent(socket);
                        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(newSocket);
                        newSocket.transform.SetParent(newModel.transform);
                    }

                    // Copy & assign weapon data
                    WeaponData newData = null;
                    if (m_CopyWeaponData)
                    {
                        string oldDataPath = AssetDatabase.GetAssetPath(objSource.WeaponData);
                        string outDataPath = m_OutputFolder + "/" + outName + ".asset";
                        if (AssetDatabase.CopyAsset(oldDataPath, outDataPath))
                        {
                            newData = AssetDatabase.LoadAssetAtPath(outDataPath, typeof(WeaponData)) as WeaponData;
                            newData.GenerateId();
                            newData.Name.Unlocalized = outName;
                            objSource.WeaponData = newData;
                        }
                    }

                    if (m_CopyExaminationPrefab)
                    {
                        GameObject newExamination = Instantiate(objSource.WeaponData.ExamineModel);
                        var uiExaminUtil = newExamination.GetComponent<UIExamineItemUtility>();
                        DestroyImmediate(uiExaminUtil.Visuals);
#if UNITY_2022_3_OR_NEWER
                        GameObject newExaminationModel = (GameObject)PrefabUtility.InstantiatePrefab(m_NewWeaponModel, oldModel.transform.parent);
#else
                        GameObject newExaminationModel = Instantiate(m_NewWeaponModel, oldModel.transform.parent);
#endif
                        newExaminationModel.transform.SetParent(newExamination.transform);
                        newExaminationModel.layer = LayerMask.NameToLayer("UI");
                        uiExaminUtil.Visuals = newExaminationModel;
                        
                        string examOutPath = m_OutputFolder + "/" + outName + "_Examination.prefab";
                        GameObject examinePrefab = PrefabUtility.SaveAsPrefabAsset(newExamination, examOutPath);

                        if (newData)
                        {
                            newData.ExamineModel = examinePrefab;
                            EditorUtility.SetDirty(newData);
                            AssetDatabase.SaveAssetIfDirty(newData);
                        }

                        DestroyImmediate(newExamination);
                    }

#if UNITY_2022_3_OR_NEWER
                    DestroyImmediate(oldModel);
#else
                    oldModel.gameObject.SetActive(false);
#endif

                    string prefabOutPath = m_OutputFolder + "/" + outName + ".prefab";
                    GameObject newPrefab = PrefabUtility.SaveAsPrefabAsset(objSource.gameObject, prefabOutPath);

                    DestroyImmediate(objSource.gameObject);

                    if (newData != null)
                    {
                        newData.EquipPrefab = newPrefab;
                        EditorUtility.SetDirty(newData);
                        AssetDatabase.SaveAssetIfDirty(newData);
                    }

                    Selection.activeObject = newPrefab;
                    Close();
                }
            }
        }
    }
}