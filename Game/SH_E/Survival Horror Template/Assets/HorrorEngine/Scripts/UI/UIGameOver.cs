using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIGameOver : MonoBehaviour
    {
        private static readonly int k_ActivationTimeID = Shader.PropertyToID("_ActivationTime");

        [SerializeField] private Button m_ContinueButton;
        [SerializeField] private Button m_QuitButton;
        [SerializeField] private SceneReference m_QuitScene;
        [SerializeField] private Image m_GameOverText;
        [SerializeField] private float m_ShowDelay = 3;

        private IUIInput m_Input;
        private Material m_GameOverTextMaterial;
        private bool m_SaveSlotAvailable;
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
            m_GameOverTextMaterial = new Material(m_GameOverText.material);
            m_GameOverText.material = m_GameOverTextMaterial;
            MessageBuffer<GameOverMessage>.Subscribe(OnGameOver);
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            MessageBuffer<GameOverMessage>.Unsubscribe(OnGameOver);
        }

        // --------------------------------------------------------------------

        void OnGameOver(GameOverMessage msg)
        {
            GameManager.Instance.StartCoroutine(ShowRoutine()); // running on manager cause this is disabled
        }

        // --------------------------------------------------------------------

        IEnumerator ShowRoutine()
        {
            yield return Yielders.Time(m_ShowDelay);

            Show();

            m_SaveSlotAvailable = GameSaveUtils.GetLastSavedSlot() >= 0;
            m_ContinueButton.gameObject.SetActive(m_SaveSlotAvailable);
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            EventSystemUtils.SelectDefaultOnLostFocus(m_SaveSlotAvailable ? m_ContinueButton.gameObject : m_QuitButton.gameObject);
        }

        // --------------------------------------------------------------------

        void Show()
        {
            m_GameOverTextMaterial.SetFloat(k_ActivationTimeID, Time.timeSinceLevelLoad);
            gameObject.SetActive(true);
            CursorController.Instance.SetInUI(true);
            m_UIContext.Activate();
        }

        // --------------------------------------------------------------------

        void Hide()
        {
            gameObject.SetActive(false);
            CursorController.Instance.SetInUI(false);
            m_UIContext.Deactivate();
            UIManager.PopAction();
        }

        // --------------------------------------------------------------------

        public void ContinueGame()
        {
            Hide();

            int lastSavedSlot = GameSaveUtils.GetLastSavedSlot();
            if (lastSavedSlot >= 0)
            {
                GameSaveUtils.LoadSlot(lastSavedSlot);
            }
        }

        // --------------------------------------------------------------------

        public void QuitGame()
        {
            Hide();
            GameManager.Instance.QuitGame();
        }
    }

}