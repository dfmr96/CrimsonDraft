using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HorrorEngine
{

    public class PrefabPaletteToolWindow : EditorWindow
    {
        public Texture LevelDesignIcon;
        
        private Vector2 m_ScrollPos;
        private Dictionary<PrefabPalette, bool> m_Foldout = new Dictionary<PrefabPalette, bool>();
        private List<PrefabPalette> m_Palettes;
        private string[] m_PaletteNames;
        private static int m_PaletteIndex;
        private bool m_DrawInstantiationGizmo = true;

        // --------------------------------------------------------------------

        [MenuItem("Horror Engine/Level Design Tool")]
        public static void Init()
        {
            PrefabPaletteToolWindow window = CreateInstance<PrefabPaletteToolWindow>();
            window.titleContent = new GUIContent("HE Level Design", window.LevelDesignIcon);
            window.UpdatePalettes();
            window.Show();
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            SceneView.duringSceneGui -= this.OnSceneGUI;
            SceneView.duringSceneGui += this.OnSceneGUI;
        }

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            SceneView.duringSceneGui -= this.OnSceneGUI;
        }

        // --------------------------------------------------------------------

        protected virtual void OnGUI()
        {
            if (m_PaletteNames == null || m_PaletteNames.Length == 0)
                UpdatePalettes();

            m_PaletteIndex = EditorGUILayout.Popup(m_PaletteIndex, m_PaletteNames);
            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);
            m_DrawInstantiationGizmo = EditorGUILayout.Toggle("Draw Instantiation Gizmo", m_DrawInstantiationGizmo);
            if (m_Palettes.Count > 0 && m_Palettes[m_PaletteIndex] != null)
            {
                DrawPalette(m_Palettes[m_PaletteIndex], true);
            }
            EditorGUILayout.EndScrollView();
        }

        // --------------------------------------------------------------------

        private void UpdatePalettes()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(PrefabPalette).FullName);
            m_Palettes = new List<PrefabPalette>();
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                PrefabPalette palette = (PrefabPalette)AssetDatabase.LoadAssetAtPath(path, typeof(PrefabPalette));
                if (!palette.IsSubpalette)
                    m_Palettes.Add(palette);
            }


            m_PaletteNames = new string[m_Palettes.Count];
            for (int i = 0; i < m_Palettes.Count; ++i)
            {
                m_PaletteNames[i] = m_Palettes[i].name;
            }

            m_PaletteIndex = Mathf.Clamp(m_PaletteIndex, 0, m_PaletteNames.Length);
        }

        // --------------------------------------------------------------------

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_DrawInstantiationGizmo)
            {
                GetInstantiationPoint(out Vector3 pos, out Vector3 normal, out Transform obj);
                Handles.color = Color.white;
                Handles.DrawWireDisc(pos, normal, 0.25f);
                Handles.color = Color.cyan;
                Handles.DrawLine(pos, pos + normal);
            }
        }

        // --------------------------------------------------------------------

        private void DrawPalette(PrefabPalette palette, bool isMain)
        {
            if (palette == null)
                return;

            if (!isMain)
            {
                m_Foldout.TryAdd(palette, true);
                m_Foldout[palette] = EditorGUILayout.Foldout(m_Foldout[palette], palette.Name);
                if (!m_Foldout[palette])
                    return;
            }
            else
            {
                EditorGUILayout.Separator();

                EditorGUILayout.BeginHorizontal();

                var titleStyle = new GUIStyle();
                titleStyle.fontSize = isMain ? 18 : 12;
                titleStyle.margin = new RectOffset(5, 5, 5, 5);
                titleStyle.normal.textColor = Color.white;
                titleStyle.fontStyle = FontStyle.Bold;

                GUILayout.Label(palette.Name, titleStyle);

                if (GUILayout.Button("Edit", GUILayout.Width(32), GUILayout.Height(16)))
                    Selection.activeObject = palette;

                EditorGUILayout.EndHorizontal();
            }

            GUILine(isMain ? 2 : 1);

            EditorGUILayout.Separator();

            if (palette.Horizontal)
                EditorGUILayout.BeginHorizontal();

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.alignment = palette.Horizontal ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;

            foreach (PrefabPaletteEntry entry in palette.Prefabs)
            {
                GUIContent content = entry.Icon ? new GUIContent(entry.Prefab.name.SplitCamelCase(), entry.Icon) : new GUIContent(entry.Prefab.name);
                GUILayoutOption[] options = entry.Icon ? new GUILayoutOption[] { GUILayout.Height(40) } : null;
                
                if (palette.Horizontal)
                    EditorGUILayout.BeginVertical();
                else
                    EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(content, buttonStyle, options))
                {
                    InstantiatePrefab(entry);
                }

                if (palette.Horizontal)
                    EditorGUILayout.EndVertical();
                else
                    EditorGUILayout.EndHorizontal();
            }
            if (palette.Horizontal)
                EditorGUILayout.EndHorizontal();

            foreach (PrefabPalette subpalette in palette.SubPalettes)
            {
                DrawPalette(subpalette, false);
            }
        }

        // --------------------------------------------------------------------

        private void GetInstantiationPoint(out Vector3 hitPos, out Vector3 hitNormal, out Transform hitObj)
        {
            Camera cam = SceneView.lastActiveSceneView.camera;
            hitObj = null;
            hitNormal = Vector3.up;
            hitPos = cam.transform.position + cam.transform.forward * 5;

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                Scene scene = prefabStage.scene;
                PhysicsScene physicScene = scene.GetPhysicsScene();
                if (physicScene.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 100, ~0, QueryTriggerInteraction.Ignore))
                {
                    hitPos = hit.point;
                    hitObj = hit.transform;
                    hitNormal = hit.normal;
                }
            }
            else
            {
                if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 100, ~0, QueryTriggerInteraction.Ignore))
                {
                    hitPos = hit.point;
                    hitObj = hit.transform;
                    hitNormal = hit.normal;
                }
            }
        }

        // --------------------------------------------------------------------

        protected virtual GameObject InstantiatePrefab(PrefabPaletteEntry entry)
        {
            Undo.IncrementCurrentGroup();
            GetInstantiationPoint(out Vector3 instantiationPos, out Vector3 normal, out Transform instantiationParent);

            GameObject layer = entry.Layer ? FindOrCreateLayer(entry.Layer) : null;
            Transform parent = layer.transform;

            GameObject instance = PrefabUtility.InstantiatePrefab(entry.Prefab, parent) as GameObject;
            instance.transform.position = instantiationPos;
            Selection.activeGameObject = instance;

            ObjectUniqueId objUniqueId = instance.GetComponent<ObjectUniqueId>();
            objUniqueId?.RegenerateId();

            Undo.RegisterCreatedObjectUndo(instance, "Instantiate prefab");
            Undo.SetCurrentGroupName("Instantiate Palette Prefab");

            return instance;
        }

        // --------------------------------------------------------------------

        private GameObject FindOrCreateLayer(SceneLayer layer)
        {
            SceneLayerObject[] layerObjs = FindObjectsByType<SceneLayerObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (SceneLayerObject layerObj in layerObjs)
                if (layerObj.Layer == layer) 
                    return layerObj.gameObject;

            var go = GameObject.Find(layer.Name);
            if (!go || go.transform.parent != null)
            {
                go = new GameObject(layer.Name);
                Undo.RegisterCreatedObjectUndo(go, "Create Design Layer");
            }

            var sceneLayer = go.AddComponent<SceneLayerObject>();
            sceneLayer.Layer = layer;
            EditorUtility.SetDirty(go);

            return go;
        }

        // --------------------------------------------------------------------

        void GUILine(int height = 1)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            rect.height = height;

            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        }

    }
}