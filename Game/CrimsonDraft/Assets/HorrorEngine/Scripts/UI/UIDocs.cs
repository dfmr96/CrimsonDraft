using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIDocs : MonoBehaviour
    {
        [SerializeField] private TMPro.TextMeshProUGUI m_DocumentName;
        [SerializeField] private GameObject m_DocumentsPage;
        [SerializeField] private Transform m_PageParent;
        [SerializeField] private int m_PageCount = 3;
        [SerializeField] private float m_AnimationTime = 1f;
        [SerializeField] private Transform m_PageLeftHook;
        [SerializeField] private Transform m_PageCenterHook;
        [SerializeField] private Transform m_PageRightHook;
        [SerializeField] private UISelectableCallbacks m_RightArrow;
        [SerializeField] private UISelectableCallbacks m_LeftArrow;

        [Header("Audio")]
        [SerializeField] private AudioClip m_NavigateClip;
        [SerializeField] private AudioClip m_CloseClip;

        private Transform[] m_Pages;
        private List<UIDocumentEntry> m_Entries = new List<UIDocumentEntry>();
        private int m_CurrentPageIndex;
        private int m_Animating;

        private IUIInput m_Input;
        private UIDocumentEntry m_SelectedEntry;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Input = GetComponentInParent<IUIInput>();

            m_Pages = new Transform[m_PageCount];
            m_Pages[0] = m_DocumentsPage.transform;
            for (int i = 1; i < m_PageCount; ++i)
            {
                m_Pages[i] = Instantiate(m_DocumentsPage).transform;
                m_Pages[i].position = new Vector3(10000, 10000, 0); // Move offscreen;
                m_Pages[i].SetParent(m_PageParent);
                m_Pages[i].localScale = m_DocumentsPage.transform.localScale;
            }

            foreach(var page in m_Pages)
            {
                var pageEntries = page.GetComponentsInChildren<UIDocumentEntry>();
                m_Entries.AddRange(pageEntries);
            }

            foreach (var entry in m_Entries)
            {
                entry.GetComponent<Button>().onClick.AddListener(OnSubmit);
                entry.GetComponent<UISelectableCallbacks>().OnSelected.AddListener(OnSelected);
            }


            m_LeftArrow.OnSelected.AddListener((go) => {
                if (m_CurrentPageIndex > 0)
                {
                    MovePageToRight(m_CurrentPageIndex);
                    SetActivePage(--m_CurrentPageIndex);
                    MovePageFromLeft(m_CurrentPageIndex);
                }
            });

            m_RightArrow.OnSelected.AddListener((go)=>{
                if (m_CurrentPageIndex < m_PageCount - 1)
                {
                    MovePageToLeft(m_CurrentPageIndex);
                    SetActivePage(++m_CurrentPageIndex);
                    MovePageFromRight(m_CurrentPageIndex);
                }
            });
        }

        // --------------------------------------------------------------------

        private void OnSelected(GameObject go)
        {
            m_SelectedEntry = go.GetComponent<UIDocumentEntry>();
            m_DocumentName.text = m_SelectedEntry && m_SelectedEntry.Data ? m_SelectedEntry.Data.Name : "";
            if (m_SelectedEntry && m_NavigateClip)
                UIManager.Get<UIAudio>().Play(m_NavigateClip);
        }

        // --------------------------------------------------------------------

        private void SetActivePage(int index)
        {
            m_CurrentPageIndex = index;

            m_LeftArrow.gameObject.SetActive(m_CurrentPageIndex > 0);
            m_RightArrow.gameObject.SetActive(m_CurrentPageIndex < m_PageCount-1);
        }

        // --------------------------------------------------------------------

        private void Start()
        {
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        public void Show()
        {
            PauseController.Instance.Pause(this);
            CursorController.Instance.SetInUI(true);

            foreach (var page in m_Pages)
            {
                page.gameObject.SetActive(false);
            }

            FillPages();

            m_Animating = 0;
            SetActivePage(0);
            
            gameObject.SetActive(true);
            MovePageFromRight(m_CurrentPageIndex);
        }

        // --------------------------------------------------------------------

        private void Hide()
        {
            PauseController.Instance.Resume(this);
            CursorController.Instance.SetInUI(false);
            gameObject.SetActive(false);

            UIManager.PopAction();
        }

        // --------------------------------------------------------------------

        private void FillPages()
        {
            var docs = GameManager.Instance.Inventory.Documents;
            int index = 0;

            foreach(var doc in docs)
            {
                m_Entries[index].Fill(doc);
                ++index;
            }

            for (int i = index; i < m_Entries.Count; ++i)
            {
                m_Entries[i].Fill(null);
            }
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            if (m_Input.IsCancelDown())
            {
                OnCancel();
            }

            if (m_Animating == 0)
                EventSystemUtils.SelectDefaultOnLostFocus(m_Pages[m_CurrentPageIndex].GetComponentInChildren<UISelectableCallbacks>().gameObject);
            else if (m_Animating > 0)
                EventSystem.current.SetSelectedGameObject(null);
        }

        // --------------------------------------------------------------------

        private void OnCancel()
        {
            if (m_CloseClip)
                UIManager.Get<UIAudio>().Play(m_CloseClip);

            Hide();
        }

        // --------------------------------------------------------------------

        public void OnSubmit()
        {
            if (!m_SelectedEntry || !m_SelectedEntry.Data)
                return;

            Hide();
            UIManager.Get<UIDocument>().Show(m_SelectedEntry.Data);
        }

        // --------------------------------------------------------------------


        private void MovePageToLeft(int index)
        {
            StartCoroutine(MovePageCoroutine(index, m_PageCenterHook, m_PageLeftHook, false));
        }

        // --------------------------------------------------------------------

        private void MovePageFromRight(int index)
        {
            StartCoroutine(MovePageCoroutine(index, m_PageRightHook, m_PageCenterHook, true));
        }

        // --------------------------------------------------------------------

        private void MovePageFromLeft(int index)
        {
            StartCoroutine(MovePageCoroutine(index, m_PageLeftHook, m_PageCenterHook, true));
        }

        // --------------------------------------------------------------------

        private void MovePageToRight(int index)
        {
            StartCoroutine(MovePageCoroutine(index, m_PageCenterHook, m_PageRightHook, false));
        }

        // --------------------------------------------------------------------

        IEnumerator MovePageCoroutine(int index, Transform fromT, Transform toT, bool endActive)
        {
            ++m_Animating;
            Vector3 from = fromT.position;
            Vector3 to = toT.position;

            m_Pages[index].position = from;

            float t = 0;
            float transitionTime = m_AnimationTime;
            float distance = Vector3.Distance(from, to);
            m_Pages[index].gameObject.SetActive(true);
            while (t < transitionTime)
            {
                yield return Yielders.EndOfFrame;
                float delta = (Time.unscaledDeltaTime / transitionTime) * distance;
                m_Pages[index].position = Vector3.MoveTowards(m_Pages[index].position, to, delta);
                t += Time.unscaledDeltaTime;
            }

            m_Pages[index].position = to;
            m_Pages[index].gameObject.SetActive(endActive);

            --m_Animating;
        }

        // --------------------------------------------------------------------

        public void OnItemsCategory()
        {
            Hide();
            m_Input.Flush();
            UIManager.Get<UIInventory>().Show();
        }

        // --------------------------------------------------------------------

        public void OnMapCategory()
        {
            Hide();
            m_Input.Flush();
            UIManager.Get<UIMap>().Show();
        }
    }
}