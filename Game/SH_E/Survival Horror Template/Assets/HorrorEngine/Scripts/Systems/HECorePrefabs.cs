using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Core Prefabs")]
    public class HECorePrefabs : ScriptableObject
    {
        [SerializeField] private HECorePrefabs m_Prototype;

        [SerializeField] private GameObject m_GameManager;
        [SerializeField] private GameObject m_CameraSystem;

        [Tooltip("These prefabs will be instantiated too. Custom prefabs defined in the prototype HECorePrefabs will also be instantiated")]
        [SerializeField] private GameObject[] m_CustomObjects;

        [Header("UI")]
        [SerializeField] private GameObject m_Inventory;
        [SerializeField] private GameObject m_Document;
        [SerializeField] private GameObject m_DocumentList;
        [SerializeField] private GameObject m_Dialog;
        [SerializeField] private GameObject m_Choices;
        [SerializeField] private GameObject m_SaveGame;
        [SerializeField] private GameObject m_Item;
        [SerializeField] private GameObject m_ItemContainer;
        [SerializeField] private GameObject m_ExamineItem;
        [SerializeField] private GameObject m_ExamineItemRenderer;
        [SerializeField] private GameObject m_Pause;
        [SerializeField] private GameObject m_Map;
        [SerializeField] private GameObject m_MapRenderer;
        [SerializeField] private GameObject m_GameOver;
        [SerializeField] private GameObject m_CinematicPlayer;
        [SerializeField] private GameObject m_InteractionPrompt;
        [SerializeField] private GameObject m_Fade;
        [SerializeField] private GameObject m_LetterBox;
        [SerializeField] private GameObject m_Ranking;

        [Tooltip("These prefabs will be instantiated too. Custom prefabs defined in the prototype HECorePrefabs will also be instantiated")]
        [SerializeField] private GameObject[] m_CustomUI;

        private Dictionary<string, List<GameObject>> m_MappedPrefabs;

        // ------------ Getters with fallback

        private GameObject GameManager => m_GameManager ? m_GameManager : m_Prototype?.GameManager;
        private GameObject CameraSystem => m_CameraSystem ? m_CameraSystem : m_Prototype?.CameraSystem;

        private GameObject Inventory => m_Inventory ? m_Inventory : m_Prototype?.Inventory; 
        private GameObject Document => m_Document ? m_Document : m_Prototype?.Document;
        private GameObject DocumentList => m_DocumentList ? m_DocumentList : m_Prototype?.DocumentList;
        private GameObject Dialog => m_Dialog ? m_Dialog : m_Prototype?.Dialog;
        private GameObject Choices => m_Choices ? m_Choices : m_Prototype?.Choices;
        private GameObject SaveGame => m_SaveGame ? m_SaveGame : m_Prototype?.SaveGame;
        private GameObject Item => m_Item ? m_Item : m_Prototype?.Item;
        private GameObject ItemContainer => m_ItemContainer ? m_ItemContainer : m_Prototype?.ItemContainer;
        private GameObject ExamineItemRenderer => m_ExamineItemRenderer ? m_ExamineItemRenderer : m_Prototype?.ExamineItemRenderer;
        private GameObject ExamineItem => m_ExamineItem ? m_ExamineItem: m_Prototype?.ExamineItem;
        private GameObject Pause => m_Pause ? m_Pause : m_Prototype?.Pause;
        private GameObject Map => m_Map ? m_Map : m_Prototype?.Map;
        private GameObject MapRenderer => m_MapRenderer ? m_MapRenderer : m_Prototype?.MapRenderer;
        private GameObject GameOver => m_GameOver ? m_GameOver : m_Prototype?.GameOver;
        private GameObject CinematicPlayer => m_CinematicPlayer ? m_CinematicPlayer : m_Prototype?.CinematicPlayer;
        private GameObject InteractionPrompt => m_InteractionPrompt ? m_InteractionPrompt : m_Prototype?.InteractionPrompt;
        private GameObject Fade => m_Fade ? m_Fade : m_Prototype?.Fade;
        private GameObject LetterBox => m_LetterBox ? m_LetterBox : m_Prototype?.LetterBox;
        private GameObject Ranking => m_Ranking ? m_Ranking : m_Prototype?.Ranking;

        // --------------------------------------------------------------------

        public Dictionary<string, List<GameObject>> GetMappedPrefabs()
        {
            return m_MappedPrefabs;
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            m_MappedPrefabs = new Dictionary<string, List<GameObject>>();

            List<GameObject> rootGO = new List<GameObject>();
            Add(rootGO, GameManager);
            Add(rootGO, CameraSystem);

            AddCustomObjects(rootGO);

            List<GameObject> uiGO = new List<GameObject>();
            Add(uiGO, Inventory);
            Add(uiGO, Document);
            Add(uiGO, DocumentList);
            Add(uiGO, Dialog);
            Add(uiGO, Choices);
            Add(uiGO, SaveGame);
            Add(uiGO, Item);
            Add(uiGO, ItemContainer);
            Add(uiGO, ExamineItemRenderer); // This need to be instantiated before the ExamineItem
            Add(uiGO, ExamineItem);
            Add(uiGO, Pause);
            Add(uiGO, Map);
            Add(uiGO, MapRenderer);
            Add(uiGO, GameOver);
            Add(uiGO, CinematicPlayer);
            Add(uiGO, InteractionPrompt);
            Add(uiGO, Fade);
            Add(uiGO, LetterBox);
            Add(uiGO, Ranking);

            AddCustomUI(uiGO);

            m_MappedPrefabs.Add("", rootGO);
            m_MappedPrefabs.Add("UI", uiGO);
        }

        // --------------------------------------------------------------------

        private void AddCustomObjects(List<GameObject> list)
        {
            if (m_CustomObjects != null)
                AddArray(list, m_CustomObjects);

            m_Prototype?.AddCustomObjects(list);
        }

        // --------------------------------------------------------------------

        private void AddCustomUI(List<GameObject> list)
        {
            AddArray(list, m_CustomUI);
            m_Prototype?.AddCustomUI(list);
        }

        // --------------------------------------------------------------------

        private void AddArray(List<GameObject> list, GameObject[] go)
        {
            if (go != null)
            {
                foreach (var entry in go)
                {
                    Add(list, entry);
                }
            }
        }

        // --------------------------------------------------------------------

        private void Add(List<GameObject> list, GameObject gameObject)
        {
            if (gameObject)
                list.Add(gameObject);
        }
    }

}
