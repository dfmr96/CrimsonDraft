using System;
using UnityEngine;

namespace HorrorEngine
{
    [Serializable]
    public struct StageRankingSaveData
    {
        public int SavedGames;
        public float TotalElapsedTime;
    }

    public class StageRankingTracker : SingletonBehaviour<StageRankingTracker>, ISavableObjectStateExtra
    {
        private float m_StartTime;
        private float m_PausedTime;
        private float m_TotalElapsedTime;
        private int m_SavedGames;
        private bool m_IsTracking;

        [ReadOnly]
        [SerializeField] 
        private string m_FormattedElapsedTime = "00:00:00";

        public int SavedGames { get { return m_SavedGames; } }

        // --------------------------------------------------------------------

        private void Start()
        {
            StartTracking();

            MessageBuffer<GamePausedMessage>.Subscribe(OnGamePaused);
            MessageBuffer<GameUnpausedMessage>.Subscribe(OnGameUnpaused);
            MessageBuffer<SaveDataEndedMessage>.Subscribe(OnGameSaved);
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            MessageBuffer<GamePausedMessage>.Unsubscribe(OnGamePaused);
            MessageBuffer<GameUnpausedMessage>.Unsubscribe(OnGameUnpaused);
            MessageBuffer<SaveDataEndedMessage>.Unsubscribe(OnGameSaved);
        }

        // --------------------------------------------------------------------

        public void OnGameUnpaused(GameUnpausedMessage msg)
        {
            if (!m_IsTracking)
            {
                StartTracking();
            }
            else
            {
                m_StartTime = Time.realtimeSinceStartup;
            }
        }

        // --------------------------------------------------------------------

        public void OnGamePaused(GamePausedMessage msg)
        {
            if (m_IsTracking)
            {
                StopTracking();
            }
        }

        // --------------------------------------------------------------------

        public void StartTracking()
        {
            if (m_IsTracking) return;

            m_StartTime = Time.realtimeSinceStartup;
            m_IsTracking = true;
        }

        // --------------------------------------------------------------------

        public void StopTracking()
        {
            if (!m_IsTracking) return;

            m_PausedTime = Time.realtimeSinceStartup;
            m_TotalElapsedTime += m_PausedTime - m_StartTime;
            m_IsTracking = false;

            m_FormattedElapsedTime = RankingUtils.GetFormattedTime(GetElapsedTime());
        }

        // --------------------------------------------------------------------

        public void ResetTracking(bool startImmediately = false)
        {
            m_StartTime = 0f;
            m_PausedTime = 0f;
            m_TotalElapsedTime = 0f;
            m_IsTracking = false;
            m_FormattedElapsedTime = RankingUtils.GetFormattedTime(GetElapsedTime());

            if (startImmediately)
            {
                StartTracking();
            }
        }

        // --------------------------------------------------------------------

        public float GetElapsedTime()
        {
            if (m_IsTracking)
            {
                return m_TotalElapsedTime + (Time.realtimeSinceStartup - m_StartTime);
            }
            return m_TotalElapsedTime;
        }

        private void OnGameSaved(SaveDataEndedMessage msg)
        {
            ++m_SavedGames;
        }

        //-----------------------------------------------------
        // ISavable implementation
        //-----------------------------------------------------

        public string GetSavableData()
        {
            m_FormattedElapsedTime = RankingUtils.GetFormattedTime(GetElapsedTime());

            StageRankingSaveData saveData = new StageRankingSaveData();
            saveData.TotalElapsedTime = GetElapsedTime();
            saveData.SavedGames = m_SavedGames;

            return JsonUtility.ToJson(saveData);
        }

        // --------------------------------------------------------------------

        public void SetFromSavedData(string savedData)
        {
            StageRankingSaveData saveData = JsonUtility.FromJson<StageRankingSaveData>(savedData);

            m_TotalElapsedTime = saveData.TotalElapsedTime;
            m_StartTime = Time.realtimeSinceStartup;
            m_SavedGames = saveData.SavedGames;
            m_FormattedElapsedTime = RankingUtils.GetFormattedTime(GetElapsedTime());
        }
    }
}
