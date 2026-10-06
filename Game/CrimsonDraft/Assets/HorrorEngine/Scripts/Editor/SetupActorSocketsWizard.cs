using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

namespace HorrorEngine
{
    public class SetupActorSocketsWizard : EditorWindow
    {
        public struct SocketWizardEntry
        {
            public string Label;
            public SocketHandle Handle;
            public GameObject Object;
            public int ControlID;
            public string Filter;
        }

        // Default references to the player sockets
        [SerializeField] private SocketHandle m_RightHandHandle;
        [SerializeField] private SocketHandle m_LeftHandHandle;
        [SerializeField] private SocketHandle m_RightFootHandle;
        [SerializeField] private SocketHandle m_LeftFootHandle;
        [SerializeField] private SocketHandle m_FlashlightHandle;

        private List<SocketWizardEntry> Sockets;

        // --------------------------------------------------------------------

        [MenuItem("Horror Engine/Wizards/Setup Player Sockets")]
        static void InitForPlayer()
        {
            SetupActorSocketsWizard window = (SetupActorSocketsWizard)EditorWindow.GetWindow(typeof(SetupActorSocketsWizard));

            List<SocketWizardEntry> playerSockets = new List<SocketWizardEntry>();
            playerSockets.Add(new SocketWizardEntry() { Label = "Right Hand", Filter = "Right Hand", Handle = window.m_RightHandHandle, Object = null, ControlID = 0 });
            playerSockets.Add(new SocketWizardEntry() { Label = "Left Hand", Filter = "Left Hand", Handle = window.m_LeftHandHandle, Object = null, ControlID = 0 });
            playerSockets.Add(new SocketWizardEntry() { Label = "Right Foot", Filter = "Right Foot", Handle = window.m_RightFootHandle, Object = null, ControlID = 0 });
            playerSockets.Add(new SocketWizardEntry() { Label = "Left Foot", Filter = "Left Foot", Handle = window.m_LeftFootHandle, Object = null, ControlID = 0 });
            playerSockets.Add(new SocketWizardEntry() { Label = "Flashlight", Filter = "Flashlight", Handle = window.m_FlashlightHandle, Object = null, ControlID = 0 });

            window.Sockets = playerSockets;
            window.titleContent = new GUIContent("Character Socket Setup");
            window.Show();
        }


        // --------------------------------------------------------------------

        [MenuItem("Horror Engine/Wizards/Setup Actor Sockets")]
        static void InitForCurrentPrefab()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (!prefabStage)
            {
                EditorUtility.DisplayDialog("No prefab stage", "Actor Socket Setup can only be performed in prefab mode", "Ok");
                return;
            }

            Actor actor = FindFirstObjectByType<Actor>();
            if (!actor)
            {
                Debug.LogError("No Actor found in the prefab");
                return;
            }

            Animator animator = actor.MainAnimator;
            if (!animator)
            {
                Debug.LogError("No MainAnimator set on the actor");
                return;
            }

            var sockets = actor.MainAnimator.GetComponentsInChildren<Socket>();
            if (sockets.Length > 0)
            {
                List<SocketWizardEntry> socketEntries = GetSocketsForSetup(sockets, false);
                if (socketEntries.Count > 0)
                    InitWithSockets(socketEntries);
            }
        }

        // --------------------------------------------------------------------

        public static void InitWithSockets(List<SocketWizardEntry> sockets)
        {
            SetupActorSocketsWizard window = (SetupActorSocketsWizard)EditorWindow.GetWindow(typeof(SetupActorSocketsWizard));
            window.Sockets = sockets;
            window.titleContent = new GUIContent("Actor Socket Setup");
            window.Show();
        }

        // --------------------------------------------------------------------

        public static List<SocketWizardEntry> GetSocketsForSetup(Socket[] sockets, bool clearObject) 
        {
            List<SocketWizardEntry> socketEntries = new List<SocketWizardEntry>();
            foreach (var socket in sockets)
            {
                if (socket.Handle != null)
                {
                    socketEntries.Add(new SetupActorSocketsWizard.SocketWizardEntry()
                    {
                        Label = socket.Handle.name,
                        Handle = socket.Handle,
                        Filter = "",
                        Object = clearObject ? null : socket.gameObject,
                        ControlID = 0
                    });
                }
            }

            return socketEntries;
        }

        // --------------------------------------------------------------------

        private void OnGUI()
        {
            int idOffset = 101;
            for (int i = 0; i < Sockets.Count; ++i)
            {
                SocketWizardEntry entry = Sockets[i];
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(entry.Label, EditorStyles.boldLabel, GUILayout.Width(100));
                if (entry.Object == null)
                {
                    EditorGUILayout.LabelField("None");
                }
                else if (GUILayout.Button(entry.Object.name))
                {
                    EditorGUIUtility.PingObject(entry.Object);
                }
                //EditorGUILayout.ObjectField(obj, typeof(GameObject), obj);
                if (GUILayout.Button("Pick", GUILayout.Width(50)))
                {
                    entry.ControlID = EditorGUIUtility.GetControlID(FocusType.Passive) + idOffset;
                    EditorGUIUtility.ShowObjectPicker<GameObject>(entry.Object, true, entry.Filter, entry.ControlID);
                    Sockets[i] = entry;
                }
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    entry.Object = null;
                    Sockets[i] = entry;
                }
                GUILayout.EndHorizontal();

                ++idOffset;
            }
            
            if (Event.current.commandName == "ObjectSelectorUpdated")
            {
                int controlId = EditorGUIUtility.GetObjectPickerControlID();
                GameObject selectedGO = EditorGUIUtility.GetObjectPickerObject() as GameObject;

                for (int i = 0; i < Sockets.Count; ++i)
                {
                    SocketWizardEntry entry = Sockets[i];
                    if (controlId == entry.ControlID)
                    {   
                        entry.Object = selectedGO;
                        Sockets[i] = entry;
                    }
                }
            }

            if (GUILayout.Button("Apply Sockets"))
            {
                foreach (var entry in Sockets)
                {
                    AddSocket(entry);
                    Debug.Log($"Socket {entry.Label} added to gameObject {entry.Object}", entry.Object);
                }

                Debug.Log("Sockets applied successfully!");
                Close();
            }
        }

        // --------------------------------------------------------------------

        private void AddSocket(SocketWizardEntry entry)
        {
            if (!entry.Object)
                return;

            Socket socket;
            if (!entry.Object.TryGetComponent<Socket>(out socket))
            {
                socket = entry.Object.AddComponent<Socket>();
            }

            socket.Handle = entry.Handle;
            EditorUtility.SetDirty(entry.Object);
        }

    }
}