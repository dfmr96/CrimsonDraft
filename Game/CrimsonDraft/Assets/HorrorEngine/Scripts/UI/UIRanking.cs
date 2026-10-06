using UnityEngine;
using TMPro;

namespace HorrorEngine
{
    public class UIRanking : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_RankText;
        [SerializeField] private TextMeshProUGUI m_ElapsedText;
        [SerializeField] private TextMeshProUGUI m_RibbonsText;
        [SerializeField] private TextMeshProUGUI m_NameText;
        [SerializeField] private AudioClip m_Completed;
        [SerializeField] private float m_FadeInTime = 3;
        [SerializeField] private string m_UnRankedText = "Unranked";
        [SerializeField] private Color m_UnRankedColor = Color.white;

        private IUIInput m_Input;
        private SceneTransition m_ExitTransition;
        private CharacterData m_ExitCharacter;
        private bool m_TransitionStarted;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Input = GetComponentInParent<IUIInput>();
        }

        // --------------------------------------------------------------------

        private void Start()
        {
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            if (m_Input != null && m_Input.IsConfirmDown())
            {
                ContinueGame();
            }
        }

        // --------------------------------------------------------------------

        public void SetRank(RankingDataSet rankingDataSet)
        {
            RankingData rank = GetRank(rankingDataSet);
            m_RankText.text = rank != null && rank.Rank != null ? rank.Rank.Name : m_UnRankedText;
            m_RankText.color = rank != null && rank.Rank != null ? rank.Rank.Color : m_UnRankedColor;
            m_ElapsedText.text = RankingUtils.GetFormattedTime(StageRankingTracker.Instance.GetElapsedTime());
            m_RibbonsText.text = StageRankingTracker.Instance.SavedGames.ToString();
            m_NameText.text = GameManager.Instance.Character.CodeName;
        }

        // --------------------------------------------------------------------

        public RankingData GetRank(RankingDataSet rankingDataSet)
        {
            float elapsedTime = StageRankingTracker.Instance.GetElapsedTime();
            int saveCount = StageRankingTracker.Instance.SavedGames;
            RankingData bestRank = null;

            foreach (var rankingData in rankingDataSet.Ranking)
            {
                if ((bestRank == null || rankingData.TimeThresholdInSeconds < bestRank.TimeThresholdInSeconds) && 
                    elapsedTime <= rankingData.TimeThresholdInSeconds && saveCount <= rankingData.MaxAllowedSaves)
                {
                    bestRank = rankingData;
                }
            }

            return bestRank;
        }

        // --------------------------------------------------------------------

        public void Show(RankingDataSet rankingDataSet, SceneTransition exitTransition, CharacterData exitCharacter)
        {
            m_ExitTransition = exitTransition;
            m_ExitCharacter = exitCharacter;

            PauseController.Instance.Pause(this);
            gameObject.SetActive(true);

            m_TransitionStarted = false;

            SetRank(rankingDataSet);

            MusicManager.Instance.Play(m_Completed, 1);
            CursorController.Instance.SetInUI(true);

            if (m_FadeInTime > 0)
                UIManager.Get<UIFade>().Fade(1f, 0f, m_FadeInTime);
        }

        // --------------------------------------------------------------------

        public void ContinueGame()
        {
            if (m_TransitionStarted)
                return;

            if (m_ExitTransition)
            {
                MessageBuffer<SceneTransitionPreMessage>.Subscribe(OnSceneTransitionEnd);
                if (m_ExitCharacter)
                    m_ExitTransition.TriggerTransitionWithNewCharacter(m_ExitCharacter);
                else
                    m_ExitTransition.TriggerTransition();

                m_TransitionStarted = true;
            }
            else
            {
                Debug.LogWarning("ExitTransition was not set");
                Hide();
            }

            StageRankingTracker.Instance.ResetTracking(true);
        }

        // --------------------------------------------------------------------

        private void OnSceneTransitionEnd(SceneTransitionPreMessage msg)
        {

            Hide();
            MessageBuffer<SceneTransitionPreMessage>.Unsubscribe(OnSceneTransitionEnd);
        }

        // --------------------------------------------------------------------

        private void Hide()
        {
            PauseController.Instance.Resume(this);
            MusicManager.Instance.Stop();
            CursorController.Instance.SetInUI(false);
            
            gameObject.SetActive(false);
        }
    }
}
