using UnityEngine;
using UnityEditor;

namespace HorrorEngine
{
	public class SaveDataEditor : EditorWindow
	{
        public Texture WindowIcon;

        [MenuItem("Horror Engine/SaveData/Open Manager")]
		static void Init()
		{
            SaveDataEditor window = (SaveDataEditor)GetWindow(typeof(SaveDataEditor));
			window.titleContent = new GUIContent("Save Manager", window.WindowIcon);
			window.Show();
		}

		void OnGUI()
		{
			Texture saveIcon = (Texture)AssetDatabase.LoadAssetAtPath("Assets\\HorrorEngine\\Scripts\\Editor\\Resources\\Floppy_disk.png", typeof(Texture));
			Texture deleteIcon = (Texture)AssetDatabase.LoadAssetAtPath("Assets\\HorrorEngine\\Scripts\\Editor\\Resources\\DeleteIcon.png", typeof(Texture));
			Texture openIcon = (Texture)AssetDatabase.LoadAssetAtPath("Assets\\HorrorEngine\\Scripts\\Editor\\Resources\\FolderIcon.png", typeof(Texture));

			GUILayout.BeginHorizontal();
			
			GUILayout.EndHorizontal();

			int[] slots = SaveDataManager<GameSaveData>.Instance.GetExistingSlots();
			int lastSlot = 0;
			foreach (var slot in slots)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Box(saveIcon, GUILayout.Width(20), GUILayout.Height(20));
				GUILayout.Label("GameSlot : " + slot);
				if (Application.isPlaying)
				{
					if (GUILayout.Button("Save"))
					{
						GameManager.Instance.StartCoroutine(SaveDataManager<GameSaveData>.Instance.SaveSlot(slot, GameManager.Instance.GetSavableData()));
					}
					if (GUILayout.Button("Load"))
					{
						GameSaveUtils.LoadSlot(slot);
					}
				}

				if (GUILayout.Button(openIcon, GUILayout.Width(20), GUILayout.Height(20)))
				{
					string path = SaveDataManager<GameSaveData>.Instance.GetSlotPath(slot);
					Debug.Log("Opening saved file: " + path);
					EditorUtility.RevealInFinder(path);
				}

				if (GUILayout.Button(deleteIcon, GUILayout.Width(20), GUILayout.Height(20)))
				{
					SaveDataManager<GameSaveData>.Instance.ClearSlot(slot);
				}

				GUILayout.EndHorizontal();
				if (slot >= lastSlot)
					lastSlot = slot+1;
			}


			EditorGUILayout.Separator();

			if (slots.Length == 0)
            {
				EditorGUILayout.HelpBox("No saved games available, enter play mode to enable the Save button", MessageType.Info);
            }
            else if (!Application.isPlaying)
            {
				EditorGUILayout.HelpBox("Save/Load buttons will show up once you enter play mode", MessageType.Info);
			}

			if (Application.isPlaying)
			{
				if (GUILayout.Button("New Save"))
				{
					GameManager.Instance.StartCoroutine(SaveDataManager<GameSaveData>.Instance.SaveSlot(lastSlot, GameManager.Instance.GetSavableData()));
				}
			}
		}


		// --------------------------------------------------------------------


		[MenuItem("Horror Engine/SaveData/ApplyObjectState")]
		public static void ApplyState()
		{
			ObjectStateManager.Instance.ApplyStates();
		}
	}
}