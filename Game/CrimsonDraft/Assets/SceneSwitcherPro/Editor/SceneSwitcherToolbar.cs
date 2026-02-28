#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using System.Reflection;
using System.Linq;
using System.IO;

[InitializeOnLoad]
public static class SceneSwitcherToolbar
{
    private static string[] sceneNames = new string[0];
    private static int selectedIndex = 0;
    private static string lastActiveScene = "";
    private static VisualElement toolbarUI;

    private static float positionOffset = 180f; // Move closer to Play button
    private static float dropdownBoxHeight = 20f; // Dropdown button height

    private static bool fetchAllScenes
    {
        get => EditorPrefs.GetBool("SceneSwitcher_FetchAllScenes", false);
        set => EditorPrefs.SetBool("SceneSwitcher_FetchAllScenes", value);
    }

    static SceneSwitcherToolbar()
    {
        RefreshSceneList();
        SelectCurrentScene();

        // Listen for when EditorBuildSettings changes (build scenes updated)
        EditorBuildSettings.sceneListChanged += RefreshSceneList;

        // Listen for project asset changes (added/removed .unity files)
        EditorApplication.projectChanged += RefreshSceneList;

        EditorSceneManager.activeSceneChangedInEditMode += (prev, current) => UpdateSceneSelection();
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        EditorApplication.delayCall += AddToolbarUI;
    }


    static void AddToolbarUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
        if (toolbarType == null) return;

        var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
        if (toolbars.Length == 0) return;

        var toolbar = toolbars[0];
        var rootField = toolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
        if (rootField == null) return;

        var root = rootField.GetValue(toolbar) as VisualElement;
        if (root == null) return;

        var leftContainer = root.Q("ToolbarZoneLeftAlign");
        if (leftContainer == null) return;

        // Remove old UI if it exists to prevent duplication
        if (toolbarUI != null)
        {
            leftContainer.Remove(toolbarUI);
        }

        toolbarUI = new IMGUIContainer(OnGUI);
        toolbarUI.style.marginLeft = positionOffset;

        leftContainer.Add(toolbarUI);
    }

    static void OnGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        CheckAndRefreshScenes();

        if (selectedIndex >= sceneNames.Length)
            selectedIndex = 0;

        bool isPlaying = EditorApplication.isPlaying; // Check if in Play Mode

        GUILayout.BeginHorizontal();

        // Fetch all scenes toggle button (Disabled in Play Mode)
        EditorGUI.BeginDisabledGroup(isPlaying);
        bool newFetchAllScenes = GUILayout.Toggle(fetchAllScenes, "All Scenes", "Button", GUILayout.Height(dropdownBoxHeight));
        if (newFetchAllScenes != fetchAllScenes)
        {
            fetchAllScenes = newFetchAllScenes;
            RefreshSceneList();
            SelectCurrentScene();
        }
        EditorGUI.EndDisabledGroup();

        // Scene dropdown with the currently selected scene displayed (Disabled in Play Mode)
        EditorGUI.BeginDisabledGroup(isPlaying);
        GUIStyle popupStyle = new GUIStyle(EditorStyles.popup)
        {
            fixedHeight = dropdownBoxHeight
        };

        int newIndex = EditorGUILayout.Popup(selectedIndex, sceneNames, popupStyle, GUILayout.Width(150), GUILayout.Height(dropdownBoxHeight));

        if (newIndex != selectedIndex)
        {
            selectedIndex = newIndex;
            LoadScene(sceneNames[selectedIndex]);
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.EndHorizontal();
    }

    static void RefreshSceneList()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (fetchAllScenes)
        {
            // Fetch all scenes from the Assets folder
            sceneNames = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories)
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .ToArray();
        }
        else
        {
            // Only include scenes that exist and are enabled in Build Settings
            var validScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && File.Exists(scene.path))
                .Select(scene => Path.GetFileNameWithoutExtension(scene.path))
                .ToArray();

            // Detect deleted scenes still listed in Build Settings
            var missingScenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !File.Exists(s.path))
                .Select(s => Path.GetFileNameWithoutExtension(s.path))
                .ToArray();

            if (missingScenes.Length > 0)
            {
                Debug.LogWarning(
                    $"<color=orange>Scene Switcher:</color> Ignored {missingScenes.Length} missing scene(s) still listed in Build Settings:\n" +
                    string.Join(", ", missingScenes)
                );
            }

            sceneNames = validScenes;
        }

        SelectCurrentScene();

        // Refresh toolbar UI
        if (toolbarUI != null)
            toolbarUI.MarkDirtyRepaint();
    }


    static void CheckAndRefreshScenes()
    {
        // Avoid refreshing if playing or switching play mode
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // Compute a hash of all scene names to detect change efficiently
        string[] currentScenes = fetchAllScenes
            ? Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories)
                .Select(Path.GetFileNameWithoutExtension)
                .ToArray()
            : EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => Path.GetFileNameWithoutExtension(scene.path))
                .ToArray();

        // Generate a simple checksum (string join)
        string currentHash = string.Join(",", currentScenes);
        string lastHash = string.Join(",", sceneNames);

        if (currentHash != lastHash)
        {
            sceneNames = currentScenes;
            SelectCurrentScene();
        }
    }


    static void SelectCurrentScene()
    {
        string currentScene = Path.GetFileNameWithoutExtension(EditorSceneManager.GetActiveScene().path);
        int index = System.Array.IndexOf(sceneNames, currentScene);

        if (index != -1)
        {
            selectedIndex = index;
            lastActiveScene = currentScene;
        }
        else
        {
            // Append "(not in build index)" if the scene isn't listed
            string notInBuildName = currentScene + " (not in build index)";

            // Insert it at the beginning or replace first element
            sceneNames = new[] { notInBuildName }.Concat(sceneNames).ToArray();
            selectedIndex = 0;
            lastActiveScene = currentScene;
        }
    }

    static void UpdateSceneSelection()
    {
        string currentScene = Path.GetFileNameWithoutExtension(EditorSceneManager.GetActiveScene().path);
        if (currentScene != lastActiveScene)
        {
            lastActiveScene = currentScene;

            // Remove any previous "(not in build index)" label to avoid duplicates
            sceneNames = sceneNames.Where(name => !name.EndsWith(" (not in build index)")).ToArray();

            SelectCurrentScene();
        }
    }


    static void LoadScene(string sceneName)
    {
        string scenePath = null;

        if (fetchAllScenes)
        {
            scenePath = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories)
                .FirstOrDefault(path => Path.GetFileNameWithoutExtension(path) == sceneName);
        }
        else
        {
            var buildScene = EditorBuildSettings.scenes
                .FirstOrDefault(scene => scene.enabled && Path.GetFileNameWithoutExtension(scene.path) == sceneName);

            if (buildScene.path != null && File.Exists(buildScene.path))
                scenePath = buildScene.path;
        }

        // Check if scene actually exists before loading
        if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
        {
            Debug.LogWarning(
                $"<color=orange>Scene Switcher:</color> Scene \"{sceneName}\" could not be found or has been deleted.\n" +
                $"Please remove it from Build Settings or re-add the file."
            );
            return;
        }

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(scenePath);
        }
    }


    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.delayCall += () => AddToolbarUI();
        }
    }
}
#endif
