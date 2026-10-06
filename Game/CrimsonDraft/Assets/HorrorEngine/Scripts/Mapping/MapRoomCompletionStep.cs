using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class MapStepCompletedMessage : BaseMessage { }

    public class MapRoomCompletionStep : MonoBehaviour, ISavableObjectStateExtra
    {
        public bool IsCompleted { get; private set; }

        public void SetCompleted(bool completed)
        {
            IsCompleted = completed;
            if (IsCompleted)
                MessageBuffer<MapStepCompletedMessage>.Dispatch();
        }

        // ---------------------------------------------
        // ISavable implementation
        // ---------------------------------------------

        public string GetSavableData()
        {
            return IsCompleted.ToString();
        }

        public void SetFromSavedData(string savedData)
        {
            IsCompleted = Convert.ToBoolean(savedData);
        }
    }
}