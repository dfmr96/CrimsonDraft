using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIMapList : MonoBehaviour
    {
        [SerializeField] private Transform m_MapListContent;
        [SerializeField] private GameObject m_MapListItemPrefab;
        [SerializeField] private string m_UnknownMapName = "???";

        public UnityEvent<MapData> OnMapSelected;

        // --------------------------------------------------------------------

        void OnEnable()
        {
            SelectDefault();
        }

        // --------------------------------------------------------------------

        private void SelectDefault()
        {
            EventSystem.current.SetSelectedGameObject(null);
            
        }

        // --------------------------------------------------------------------

        public void Fill()
        {
            var gameData = GameManager.Instance;
            var mapDB = GameManager.Instance.GetDatabase<MapDatabase>();
            if (mapDB)
            {
                for (int i = 0; i < mapDB.Registers.Count; ++i)
                {
                    Transform item;
                    if (i >= m_MapListContent.childCount)
                    {
                        item = Instantiate(m_MapListItemPrefab).transform;
                        item.SetParent(m_MapListContent);

                        Button button = item.GetComponentInChildren<Button>();
                        button.onClick.AddListener(OnMapEntryClicked);
                    }
                    else
                    {
                        item = m_MapListContent.GetChild(i);
                    }

                    var tmPro = item.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    MapData map = mapDB.Registers[i];
                    bool playerHasMap = gameData.Inventory.Maps.Contains(map);
                    bool unveiled = playerHasMap || map.GetVisited();
                    tmPro.text = unveiled ? map.Name : m_UnknownMapName;

                    item.GetComponentInChildren<Button>().interactable = unveiled;
                }
            }
        }

        private void OnMapEntryClicked()
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            var mapDB = GameManager.Instance.GetDatabase<MapDatabase>();
            for (int i = 0; i < m_MapListContent.childCount; ++i)
            {
                var child = m_MapListContent.GetChild(i);
                if (selected == child.gameObject)
                {
                    OnMapSelected?.Invoke(mapDB.Registers[i]);
                }
            }
        }
    }
}