using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [System.Serializable]
    public class StatusChangedEvent : UnityEvent<StatusData> { }

    public struct StatusSavable
    {
        public List<string> AppliedStatusesIds;
    }

    public class Status : MonoBehaviour, ISavableObjectStateExtra
    {
        [SerializeField] private StatusData m_DefaultStatus;

        private List<StatusData> m_AppliedStatuses = new List<StatusData>();
        private StatusData m_CurrentStatus;
        private Health m_Health;

        public StatusChangedEvent OnStatusChanged = new StatusChangedEvent();

        // --------------------------------------------------------------------

        private void Start()
        {
            m_Health = GetComponent<Health>();
            UpdateCurrentStatus(); 
        }

        // --------------------------------------------------------------------

        public void AddStatus(StatusData newStatus)
        {
            if (newStatus == null || m_AppliedStatuses.Contains(newStatus))
            {
                return;
            }

            m_AppliedStatuses.Add(newStatus);

            UpdateCurrentStatus();
        }

        // --------------------------------------------------------------------

        public void RemoveStatus(StatusData statusToRemove)
        {
            if (m_AppliedStatuses.Contains(statusToRemove))
            {
                m_AppliedStatuses.Remove(statusToRemove);
                UpdateCurrentStatus();
            }
        }

        // --------------------------------------------------------------------

        private void UpdateCurrentStatus()
        {
            if (m_AppliedStatuses.Count > 0)
            {
                m_CurrentStatus = m_AppliedStatuses[m_AppliedStatuses.Count - 1];
            }
            else
            {
                m_CurrentStatus = m_DefaultStatus; 
            }

            OnStatusChanged.Invoke(m_CurrentStatus);
        }

        // --------------------------------------------------------------------

        public UIPlayerStatusEntry GetCurrentStatus()
        {
            if (m_CurrentStatus == null || m_CurrentStatus.UIEntries == null || m_CurrentStatus.UIEntries.Length == 0)
                return null;

            if (m_CurrentStatus.UIEntries.Length == 1)
            {
                return m_CurrentStatus.UIEntries[0];
            }

            float currentHealth = m_Health.Value;
            UIPlayerStatusEntry selectedStatus = null;
            float maxHealthThreshold = 0;

            foreach (var statusLevel in m_CurrentStatus.UIEntries)
            {
                if (selectedStatus == null || (currentHealth >= statusLevel.FromHealth && statusLevel.FromHealth > maxHealthThreshold))
                {
                    maxHealthThreshold = statusLevel.FromHealth;
                    selectedStatus = statusLevel;
                }
            }

            return selectedStatus;
        }

        //-----------------------------------------------------
        // ISavable implementation
        //-----------------------------------------------------

        public string GetSavableData()
        {
            List<string> appliedStatusIds = new List<string>();
            foreach(var status in m_AppliedStatuses)
            {
                appliedStatusIds.Add(status.UniqueId);
            }

            StatusSavable savedData = new StatusSavable
            {
                AppliedStatusesIds = appliedStatusIds
            };

            string saveData = JsonUtility.ToJson(savedData);

            return saveData;
        }

        public void SetFromSavedData(string savedData)
        {
            StatusSavable statusData = JsonUtility.FromJson<StatusSavable>(savedData);
            var statusDB = GameManager.Instance.GetDatabase<StatusDataDatabase>();
            if (statusDB)
            {
                m_AppliedStatuses.Clear();
                foreach (var statusId in statusData.AppliedStatusesIds)
                {
                    var status = statusDB.GetRegister(statusId);
                    Debug.Assert(status, $"Status with id {statusId} could not be found in the DB");
                    if (status)
                        m_AppliedStatuses.Add(status);
                }

                if (m_AppliedStatuses.Count > 0)
                {
                    m_CurrentStatus = m_AppliedStatuses[m_AppliedStatuses.Count - 1];
                    OnStatusChanged.Invoke(m_CurrentStatus);
                }
            }
        }
    }
}
