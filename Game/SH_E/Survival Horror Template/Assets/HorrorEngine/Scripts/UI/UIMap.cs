using System;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIMap : MonoBehaviour
    {
        [SerializeField] private RawImage m_MapImage;
        [SerializeField] private TMPro.TextMeshProUGUI m_MapName;
        [SerializeField] private float m_MovementSpeed = 100;
        [SerializeField] private float m_InputSnapThreshold = 0.5f;
        [SerializeField] private UIMapList m_MapList;
        [SerializeField] private Transform m_FloorsParent;
        [SerializeField] private GameObject m_FloorsPrefab;
        [SerializeField] private GameObject m_FloorsLegend;
        [SerializeField] private Color m_CurrentFloorColor;
        [SerializeField] private Color m_NotCurrentFloorColor;

        [Header("Audio")]
        [SerializeField] private AudioClip m_ShowClip;
        [SerializeField] private AudioClip m_CloseClip;
        [SerializeField] private AudioClip m_ErrorClip;

        private IUIInput m_Input;
        private MapData m_CurrentMap;
        private UIContext m_UIContext;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_UIContext = GetComponent<UIContext>();
            m_Input = GetComponentInParent<IUIInput>();
        }

        // --------------------------------------------------------------------

        private void Start()
        {
            gameObject.SetActive(false);
            m_MapList.gameObject.SetActive(false);
            m_MapList.OnMapSelected.AddListener(OnMapSelected);
        }

        // --------------------------------------------------------------------


        private void OnMapSelected(MapData map)
        {
            m_CurrentMap = map;

            MapController currentMapCtrl = MapController.GetCurrent();

            bool isCurrent = currentMapCtrl.Data == map;
            MapRenderer.Instance.GenerateAll(map);
            MapRenderer.Instance.SetPlayerVisible(isCurrent);
            if (isCurrent)
            {
                currentMapCtrl.UpdateContent();
                currentMapCtrl.GetTransformInRoom(GameManager.Instance.Player.transform, out Vector3 playerPos, out Quaternion playerRot);
                MapRenderer.Instance.SetPlayerPositionAndRotation(playerPos, playerRot);
                MapRenderer.Instance.SetTargetPosition(playerPos);
            }
            
            m_MapList.gameObject.SetActive(false);
            m_MapName.text = map.Name;

            UpdateFloors();

            UIManager.Get<UIAudio>().Play(m_ShowClip);
        }

        // --------------------------------------------------------------------

        private void UpdateFloors()
        {
            int setIndex = -1;

            MapDataSet set = m_CurrentMap.MapSet;
            if (set)
            {
                setIndex = Array.IndexOf(set.Maps, m_CurrentMap);
                for (int i = m_FloorsParent.childCount; i < set.Maps.Length; ++i)
                {
                    Instantiate(m_FloorsPrefab, m_FloorsParent);
                }
            }

            int activeCount = 0;
            Transform child = null;
            for (int i = 0; i < m_FloorsParent.childCount; ++i)
            {
                child = m_FloorsParent.GetChild(i);
                Image image = child.GetComponentInChildren<Image>();
                MapData map = set ? set.Maps[i] : null;

                bool isActive = set && i < set.Maps.Length && map.IsKnownByPlayer();
                child.gameObject.SetActive(isActive);

                if (isActive)
                {
                    image.color = (setIndex == i) ? m_CurrentFloorColor : m_NotCurrentFloorColor;
                    TMPro.TextMeshProUGUI text = child.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    text.text = set.Maps[i].Abbreviation;
                    ++activeCount;
                }
            }

            if (child)
                child.gameObject.SetActive(activeCount > 1);

            m_FloorsLegend.gameObject.SetActive(activeCount > 1);
        }

        // ---------------------- ---------------------------------------------

        private void Update()
        {
            if (m_Input.IsToggleMapListDown())
            {
                ShowMapList();
            }

            if (m_MapList.gameObject.activeSelf)
            {
                if (m_Input.IsCancelDown())
                {
                    m_MapList.gameObject.SetActive(false);
                    CursorController.Instance.SetInUI(false);
                }

                return;
            }
            else
            {
                if (m_CurrentMap && m_CurrentMap.MapSet)
                    UpdateFloorChange();
            }

            if (m_Input.IsCancelDown() || m_Input.IsToggleMapDown())
            {
                Hide();
            }

            Vector2 move = m_Input.GetPrimaryAxis();
            move.x = Mathf.Abs(move.x) > m_InputSnapThreshold ? move.x : 0f;
            move.y = Mathf.Abs(move.y) > m_InputSnapThreshold ? move.y : 0f;

            if (MapRenderer.Exists && m_CurrentMap)
                MapRenderer.Instance.MoveTarget(move* Time.unscaledDeltaTime * m_MovementSpeed);
        }

        // --------------------------------------------------------------------

        private void ShowMapList()
        {
            m_MapList.gameObject.SetActive(!m_MapList.gameObject.activeSelf);
            CursorController.Instance.SetInUI(m_MapList.gameObject.activeSelf);
        }

        // --------------------------------------------------------------------

        private void UpdateFloorChange()
        {
            if (m_Input.IsPrevSubmapDown())
            {
                ShowPrevSubmap();
            }

            if (m_Input.IsNextSubmapDown())
            {
                ShowNextSubmap();
            }
        }

        // --------------------------------------------------------------------

        private  void ShowNextSubmap()
        {
            int currentIndex = Array.IndexOf(m_CurrentMap.MapSet.Maps, m_CurrentMap);

            int prevIndex = currentIndex;
            for (int i = 0; i < m_CurrentMap.MapSet.Maps.Length; ++i)
            {
                ++currentIndex;
                if (currentIndex >= m_CurrentMap.MapSet.Maps.Length)
                    currentIndex = 0;

                if (m_CurrentMap.MapSet.Maps[currentIndex].IsKnownByPlayer())
                    break;
            }

            if (currentIndex != prevIndex)
                OnMapSelected(m_CurrentMap.MapSet.Maps[currentIndex]);
            else
                UIManager.Get<UIAudio>().Play(m_ErrorClip);
        }

        // --------------------------------------------------------------------

        private  void ShowPrevSubmap()
        {
            int currentIndex = Array.IndexOf(m_CurrentMap.MapSet.Maps, m_CurrentMap);

            int prevIndex = currentIndex;
            for (int i = 0; i < m_CurrentMap.MapSet.Maps.Length; ++i)
            {
                --currentIndex;
                if (currentIndex < 0)
                    currentIndex = m_CurrentMap.MapSet.Maps.Length - 1;

                if (m_CurrentMap.MapSet.Maps[currentIndex].IsKnownByPlayer())
                    break;
            }

            if (currentIndex != prevIndex)
                OnMapSelected(m_CurrentMap.MapSet.Maps[currentIndex]);
            else
                UIManager.Get<UIAudio>().Play(m_ErrorClip);
        }

        // --------------------------------------------------------------------

        public void Show(MapData map = null)
        {
            PauseController.Instance.Pause(this);

            gameObject.SetActive(true);

            if (MapController.Exists)
            {
                MapController mapCtrl = MapController.GetCurrent();
                if (map == null)
                {
                    map = mapCtrl.Data;
                }
            }
            else
            {
                Debug.LogWarning("There isn't a MapController in the scene. Map won't be visible in the map screen");
            }

            if (MapRenderer.Exists)
            {
                m_MapImage.texture = MapRenderer.Instance.RenderTexture;
                m_MapImage.gameObject.SetActive(true);

                MapRenderer.Instance.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning("There isn't a MapRenderer in the scene. Map won't be visible in the map screen");
            }

            m_MapList.Fill();

            if (map)
                OnMapSelected(map);

            m_UIContext.Activate();
        }

        // --------------------------------------------------------------------

        private void Hide()
        {
            PauseController.Instance.Resume(this);
            gameObject.SetActive(false);

            if (MapRenderer.Exists)
                MapRenderer.Instance.gameObject.SetActive(false);

            UIManager.Get<UIAudio>().Play(m_CloseClip);


            m_UIContext.Deactivate();

            UIManager.PopAction();
        }

        // --------------------------------------------------------------------

        public bool CanBeShown()
        {
            return !m_UIContext.IsBlocked();
        }
    }
}