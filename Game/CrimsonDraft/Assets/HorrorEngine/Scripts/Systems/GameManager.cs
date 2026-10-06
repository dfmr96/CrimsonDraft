using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace HorrorEngine
{
    [Serializable]
    public class GameSaveData
    {
        public static readonly int k_CurrentVersion = 1; // Increase this number to force player save data clearance
        
        public int Version;
        public string Date;
        public int SaveCount;
        public string SaveLocation;
        public string SceneName;
        public string CharacterName;
        public string CharacterId;
        public List<CharacterStateSaveData> CharacterStates;
        public ContainerSaveData SharedStorageBox;
        public GameAttributesSavable Attributes;
    }

    public class GameStartMessage : BaseMessage { }
    public class GameOverMessage : BaseMessage { }

   
    public class GameManager : SingletonBehaviour<GameManager>, ISavable<GameSaveData>
    {
        public static readonly string k_StartCharacterPlayerPrefs = "StartCharacter";

        [HideInInspector]
        public PlayerActor Player;
        public List<CharacterData> Characters;
        public InventorySetup InventorySetup;
        [Tooltip("The storage box items will be shared betwen characters")]
        public bool ShareStorageBox;
        [ShowIf("ShareStorageBox")]
        public ContainerData SharedStorageBox;
        public GameAttributeValueEntry[] InitialAttributes;

        [Header("Databases")]
        public RegisterDatabase[] Databases;

        [HideInInspector]
        public UnityEvent<PlayerActor> OnPlayerRegistered;

        [SerializeField] private SceneTransition m_QuitTransition;

        private bool m_IsPlaying;
        private List<CharacterState> m_CharacterStates = new List<CharacterState>();
        private CharacterState m_SelectedCharacterState;
        private ContainerData m_CurrentSharedStorageBox;
        private int m_SaveCount;
        private GameAttributes m_Attributes = new();
        private Transform m_GlobalObjects; // Transform used for objects that shouldn't be scene-specific (like active companions)

        public int SaveCount => m_SaveCount;

        public CharacterState CharacterState => m_SelectedCharacterState;
        public CharacterData Character => m_SelectedCharacterState.Data;
        public Inventory Inventory => m_SelectedCharacterState.Inventory;
        public ContainerData StorageBox => ShareStorageBox ? m_CurrentSharedStorageBox : m_SelectedCharacterState.StorageBox;

        public Transform GlobalObjects => m_GlobalObjects;

        // --------------------------------------------------------------------

        public bool IsPlaying
        { 
            get
            {
                return !PauseController.Instance.IsPaused && m_IsPlaying && !Player.IsDisabled;
            }
            set
            {
                m_IsPlaying = value;
            }
        }

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            InitializeRegisters();
            InitializeCharacters();

            if (ShareStorageBox)
            {
                m_CurrentSharedStorageBox = new ContainerData();
                m_CurrentSharedStorageBox.Copy(SharedStorageBox);
                m_CurrentSharedStorageBox.FillCapacityWithEmptyEntries();
            }

            m_Attributes.Init(InitialAttributes);

            MessageBuffer<SceneTransitionPreMessage>.Subscribe(OnSceneTransitionPre);
            MessageBuffer<SceneTransitionPostMessage>.Subscribe(OnSceneTransitionPost);
            MessageBuffer<DoorTransitionMidWayMessage>.Subscribe(OnDoorTransitionMidway);

            m_GlobalObjects = new GameObject("GlobalObjects").transform;
            m_GlobalObjects.SetParent(transform);

            StartGame();
        }

        // --------------------------------------------------------------------

        private void InitializeCharacters()
        {
            Debug.Assert(Characters.Count > 0, "The character list is empty");

            // Get all spawn points in the scene
            PlayerSpawnPoint[] spawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);

            // Default spawn point in case no matching is found
            PlayerSpawnPoint defaultSpawnPoint = spawnPoints.Length > 0 ? spawnPoints[0] : null;

            // Get the last selected character ID, if it exists
            string startCharacterId = PlayerPrefs.GetString(k_StartCharacterPlayerPrefs, "");

            // Check if we have defined spawn points
            if (spawnPoints.Length == 0)
            {
                Debug.LogWarning("No spawn points found in the scene!");
                return;
            }

            // Initialize states for all characters
            CharacterData spawningCharacter = null;
            foreach (var characterData in Characters)
            {
                CharacterState characterState = new CharacterState(characterData);
                m_CharacterStates.Add(characterState);

                // Cache the character with the expected Id
                if (!spawningCharacter && (characterData.UniqueId == startCharacterId || string.IsNullOrEmpty(startCharacterId)))
                {
                    spawningCharacter = characterData;
                }
            }


            if (spawningCharacter)
            {
                bool spawned = false;

                // Check each spawn point for a match with the spawning character
                foreach (var spawnPoint in spawnPoints)
                {
                    // Check if the spawn point has an assigned character and if it matches the current character
                    if (spawnPoint.Character == spawningCharacter)
                    {
                        SpawnCharacter(spawningCharacter, spawnPoint.transform, true);
                        spawned = true;
                        break;
                    }
                }

                // If the character has not spawned yet, no suitable spawn point was found, spawn at the default one now
                if (!spawned)
                {
                    SpawnCharacter(spawningCharacter, defaultSpawnPoint.transform, true);
                    spawned = true;
                }
            }
            else
            {
                Debug.LogError($"Character to spawn couldn't be found in character list. Expected character Id {startCharacterId}. Spawning first character at default spawn point");
                SpawnCharacter(Characters[0], defaultSpawnPoint.transform, true);
            }

            PlayerPrefs.DeleteKey(k_StartCharacterPlayerPrefs);
        }

        // --------------------------------------------------------------------

        private CharacterData GetCharacterById(string id)
        {
            foreach (var character in Characters)
            {
                if (id == character.UniqueId)
                {
                    return character;
                }
            }

            Debug.LogError("Character Id could not be found");
            return null;
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            MessageBuffer<SceneTransitionPreMessage>.Unsubscribe(OnSceneTransitionPre);
            MessageBuffer<SceneTransitionPostMessage>.Unsubscribe(OnSceneTransitionPost);
            MessageBuffer<DoorTransitionMidWayMessage>.Unsubscribe(OnDoorTransitionMidway);
        }

        // --------------------------------------------------------------------

        private void SpawnCharacter(CharacterData character, Transform spawnPoint, bool initialState)
        {
            Debug.Log($"Spawning character {character}");
            var playerPoolable = GameObjectPool.Instance.GetFromPool(character.Prefab, transform.parent);
            PlayerActor player = playerPoolable.GetComponent<PlayerActor>();
            player.Character = character;
            
            SetCurrentPlayer(player);
            
            if (initialState)
                m_SelectedCharacterState.SetupInitialEquipment(player);
            
            if (spawnPoint)
            {
                player.PlaceAt(spawnPoint);
                //player.GetComponent<GameObjectReset>().ResetComponents(); // This is to reset the animSpeedSetter, but might be overkill
            }

            player.gameObject.SetActive(true);
        }

        // --------------------------------------------------------------------

        public void SetCurrentPlayer(PlayerActor player)
        {
            if (Player)
            {
                Player.GetComponent<Health>().OnDeath.RemoveListener(OnPlayerDeath);
            }

            Player = player;
            Player.GetComponent<Health>().OnDeath.AddListener(OnPlayerDeath);

            SetSelectCharacterState(player.Character);

            OnPlayerRegistered?.Invoke(player);
        }

        // --------------------------------------------------------------------

        private void SetSelectCharacterState(CharacterData selected)
        {
            foreach (var characterState in m_CharacterStates) 
            {
                if (characterState.Data == selected) 
                {
                    m_SelectedCharacterState = characterState;
                    return;
                }
            }

            Debug.LogError("Selected character couldn't be found in the character list");
        }

        // --------------------------------------------------------------------

        public void InitializeRegisters()
        {
            foreach(var db in Databases)
            {
                db.HashRegisters();
            }
        }

        // --------------------------------------------------------------------

        public T GetDatabase<T>() where T : RegisterDatabase
        {
            foreach (var db in Databases)
            {
                if (db is T)
                    return db as T;
            }

            Debug.LogError($"Database of type {typeof(T)} could not be found in the GameManager");

            return null;
        }

        // --------------------------------------------------------------------

        void OnDoorTransitionMidway(DoorTransitionMidWayMessage msg)
        {
            ObjectStateManager.Instance.CaptureStates();
        }

        // --------------------------------------------------------------------

        void OnSceneTransitionPre(SceneTransitionPreMessage msg)
        {
            ObjectStateManager.Instance.CaptureStates();
        }

        // --------------------------------------------------------------------

        void OnSceneTransitionPost(SceneTransitionPostMessage msg)
        {
            SpawnableSavableDatabase spawnableDatabase = GetDatabase<SpawnableSavableDatabase>();
            if (spawnableDatabase)
            {
                ObjectStateManager.Instance.InstantiateSpawned(SceneManager.GetActiveScene(), spawnableDatabase);
                ObjectStateManager.Instance.ApplyStates();
            }
        }

        // --------------------------------------------------------------------

        void OnPlayerDeath(Health health)
        {
            GameOver();  
        }

        // --------------------------------------------------------------------

        public void GameOver()
        {
            MessageBuffer<GameOverMessage>.Dispatch();
            Player.Disable(this);
            IsPlaying = false;
        }

        // --------------------------------------------------------------------

        public void IncreaseSaveCount()
        {
            ++m_SaveCount;
        }

        // --------------------------------------------------------------------

        public void SwitchCharacter(CharacterData character, Transform spawnPoint)
        {
            Debug.Log($"Switching character {character}");

            if (character == null)
            {
                character = m_SelectedCharacterState.Data;
            }

            if (character != m_SelectedCharacterState.Data)
            {
                if (Player)
                {
                    var pooled = Player.GetComponent<PooledGameObject>();
                    Debug.Assert(pooled, "Player should have a PooledGameObject component");
                    pooled?.ReturnToPool();
                }

                SpawnCharacter(character, spawnPoint, false);
            }
            else
            {
                if (spawnPoint)
                    Player.PlaceAt(spawnPoint);
            }
        }

        // --------------------------------------------------------------------

        public void Clear()
        {
            foreach(var character in m_CharacterStates)
            {
                character.Clear();
            }

            if (ShareStorageBox)
                m_CurrentSharedStorageBox.Clear();

            m_GlobalObjects.DestroyAllChildren();
        }

        // --------------------------------------------------------------------

        public void StartGame()
        {
            IsPlaying = true;
            Player.Enable(this);

            MessageBuffer<GameStartMessage>.Dispatch();
        }

        // --------------------------------------------------------------------

        public void QuitGame()
        {
            m_QuitTransition.TriggerTransition();
        }

        // --------------------------------------------------------------------

        public GameAttributes GetAttributes(GameAttributeDomain domain)
        {
            if (domain == GameAttributeDomain.Global)
            {
                return m_Attributes;
            }
            else
            {
                return CharacterState.Attributes;
            }
        }


        //------------------------------------------------------
        // ISavable implementation
        //------------------------------------------------------

        public GameSaveData GetSavableData()
        {
            GameSaveData savedData = new GameSaveData();
            savedData.Version = GameSaveData.k_CurrentVersion;
            savedData.Date = DateTime.Now.ToString();
            savedData.SaveCount = m_SaveCount;
            savedData.SceneName = SceneManager.GetActiveScene().name;
            savedData.CharacterStates = new List<CharacterStateSaveData>();
            savedData.CharacterName = m_SelectedCharacterState.Data.CodeName;
            savedData.CharacterId = m_SelectedCharacterState.Data.UniqueId;

            foreach (var characterState in m_CharacterStates)
            {
                savedData.CharacterStates.Add(characterState.GetSavableData());
            }

            if (ShareStorageBox)
                savedData.SharedStorageBox = m_CurrentSharedStorageBox.GetSavableData();

            savedData.Attributes = m_Attributes.GetSavableData();

            return savedData;
        }

        public void SetFromSavedData(GameSaveData savedData)
        {
            m_SaveCount = savedData.SaveCount;

            var character = GetCharacterById(savedData.CharacterId);
            SwitchCharacter(character, null);
            
            foreach (var savedCharacterState in savedData.CharacterStates)
            {
                foreach (var characterState in m_CharacterStates)
                {
                    if (savedCharacterState.HandleId == characterState.Data.UniqueId)
                    {
                        characterState.SetFromSavedData(savedCharacterState);
                        break;
                    }
                }
            }

            if (ShareStorageBox)
                m_CurrentSharedStorageBox.SetFromSavedData(savedData.SharedStorageBox);

            m_Attributes.SetFromSavedData(savedData.Attributes);
        }

    }
}