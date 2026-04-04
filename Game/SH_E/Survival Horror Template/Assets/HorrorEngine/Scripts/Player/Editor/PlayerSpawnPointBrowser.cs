using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace HorrorEngine
{
    public class PlayerSpawnPointBrowser : EditorWindow
    {
        public Texture2D WindowIcon;
        private PlayerSpawnPoint[] m_AllSpawnPoints;
        private bool m_MultipleDefaultsWarning = false;
        private Vector2 m_ScrollPos;
        private GUIStyle m_ActiveButtonStyle;
        private string m_Filter = "";
        private GameObject m_PreSavedSpawnPointActive = null;

        [MenuItem("Horror Engine/Spawn Point Browser")]
        public static void ShowWindow()
        {
            var browser = GetWindow<PlayerSpawnPointBrowser>();
            browser.titleContent = new GUIContent("Spawn Browser", browser.WindowIcon);
        }

        private void OnEnable()
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;

            // Initial setup
            FindAllSpawnPoints();
        }


        private void OnDisable()
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnHierarchyChanged()
        {
            FindAllSpawnPoints();
            Repaint();
        }

        private void OnSceneSaving(Scene scene, string path)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }


            m_AllSpawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var spawn in m_AllSpawnPoints)
            {
                if (spawn.isActiveAndEnabled)
                    m_PreSavedSpawnPointActive = spawn.gameObject;

                spawn.gameObject.SetActive(spawn.IsDefault);
            }
        }

        private void OnSceneSaved(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            m_AllSpawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var spawn in m_AllSpawnPoints)
            {
                spawn.gameObject.SetActive(spawn.gameObject == m_PreSavedSpawnPointActive);
            }
            m_PreSavedSpawnPointActive = null;
        }


        private void FindAllSpawnPoints()
        {
            m_AllSpawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            CheckForMultipleDefaults();
        }

        private void CheckForMultipleDefaults()
        {
            int defaultCount = m_AllSpawnPoints.Count(sp => sp.IsDefault);
            m_MultipleDefaultsWarning = defaultCount > 1;
        }

        private void OnGUI()
        {
            if (m_MultipleDefaultsWarning)
            {
                GUIStyle warningStyle = new GUIStyle(EditorStyles.helpBox);
                warningStyle.normal.textColor = Color.red;
                warningStyle.fontStyle = FontStyle.Bold;

                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "Multiple spawn points are marked as default. Only one should be marked 'IsDefault'.",
                    MessageType.Warning);
                EditorGUILayout.Space();
            }

            if (m_AllSpawnPoints == null || m_AllSpawnPoints.Length == 0)
            {
                EditorGUILayout.HelpBox("No PlayerSpawnPoint components found in the current scene.", MessageType.Info);
                return;
            }

            GUILayout.Label("Filter by name or tag", EditorStyles.boldLabel);
            m_Filter = EditorGUILayout.TextField(m_Filter, EditorStyles.toolbarSearchField);

            GUILayout.Space(10);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("While the game is running the buttons below will teleport the player to the selected spawn point", MessageType.Info);
            }

            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);

            foreach (PlayerSpawnPoint spawnPoint in m_AllSpawnPoints)
            {
                if (spawnPoint == null) continue;

                if (!string.IsNullOrEmpty(m_Filter))
                {
                    string tag = spawnPoint.tag;
                    if (!spawnPoint.name.ToLower().Contains(m_Filter.ToLower()) &&
                        !tag.ToLower().Contains(m_Filter.ToLower()))
                        continue;
                }

                EditorGUILayout.BeginHorizontal();

                SerializedObject serializedSpawnPoint = new SerializedObject(spawnPoint);
                SerializedProperty isDefaultProp = serializedSpawnPoint.FindProperty("IsDefault");

                Color textColor = spawnPoint.gameObject.activeInHierarchy ? Color.green : (isDefaultProp.boolValue ? Color.yellow : Color.white);
                m_ActiveButtonStyle = new GUIStyle(GUI.skin.button);
                m_ActiveButtonStyle.normal.textColor = textColor;
                m_ActiveButtonStyle.hover.textColor = textColor;
                m_ActiveButtonStyle.focused.textColor = textColor;

                string buttonText = spawnPoint.gameObject.name + (isDefaultProp.boolValue ? " (DEFAULT)" : "");

                if (GUILayout.Button(buttonText, m_ActiveButtonStyle))
                {
                    if (!Application.isPlaying)
                    {
                        SelectSpawnPoint(spawnPoint);
                    }
                    else
                    {
                        var player = GameManager.Instance.Player;
                        var charCtrl = player.GetComponent<CharacterController>();
                        charCtrl.enabled = false;
                        GameManager.Instance.Player.PlaceAt(spawnPoint.transform);
                        charCtrl.enabled = true;
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            // Ensure the warning is updated if an Inspector change affects IsDefault
            if (GUI.changed)
            {
                CheckForMultipleDefaults();
            }
        }

        // The core logic for handling a button press
        private void SelectSpawnPoint(PlayerSpawnPoint selectedSpawnPoint)
        {
            // Disable all other spawn points
            foreach (PlayerSpawnPoint sp in m_AllSpawnPoints)
            {
                // The request is to disable *all the other* spawn points GameObjects.
                if (sp != selectedSpawnPoint && sp != null)
                {
                    // This will call the scene setter and mark the scene as dirty
                    sp.gameObject.SetActive(false);
                }
            }

            selectedSpawnPoint.gameObject.SetActive(true);

            // Select the GameObject in the Hierarchy and focus the Scene View
            Selection.activeGameObject = selectedSpawnPoint.gameObject;

            // Focus the viewport on that object
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
        }
    }
}