using System;
using System.Collections.Generic;

namespace HorrorEngine
{
    [Serializable]
    public class GameAttributeValueEntry
    {
        public GameAttribute Attribute;
        public string Value;
    }

    public class GameAttributeChangedMessage : BaseMessage
    {
        public string AttributeId;
    }

    [Serializable]
    public class GameAttributeSaveDataEntry
    {
        public string Id;
        public string Value;
    }

    [Serializable]
    public class GameAttributesSavable
    {
        public List<GameAttributeSaveDataEntry> Entries;
    }

    public class GameAttributes : ISavable<GameAttributesSavable>
    {
        public Dictionary<string, string> m_Attributes = new Dictionary<string, string>();
        private GameAttributeChangedMessage m_ValueChangedMessage = new GameAttributeChangedMessage();

        // --------------------------------------------------------------------

        public void Init(GameAttributeValueEntry[] initialAttributes)
        {
            if (initialAttributes == null)
                return;

            foreach(var att in initialAttributes)
            {
                Set(att.Attribute, att.Value);
            }
        }

        // --------------------------------------------------------------------

        public void Clear()
        {
            m_Attributes.Clear();
        }

        // --------------------------------------------------------------------

        public bool IsDefined(GameAttribute attribute)
        {
            return m_Attributes.ContainsKey(attribute.UniqueId);
        }

        // --------------------------------------------------------------------

        public void Set(GameAttribute attribute, string val)
        {
            Set(attribute.UniqueId, val);
        }

        // --------------------------------------------------------------------

        public string Get(GameAttribute attribute)
        {
            return m_Attributes[attribute.UniqueId];
        }

        // --------------------------------------------------------------------

        private void Set(string id, string val)
        {
            if (!m_Attributes.ContainsKey(id))
                m_Attributes.Add(id, val);
            else
                m_Attributes[id] = val;

            m_ValueChangedMessage.AttributeId = id;
            MessageBuffer<GameAttributeChangedMessage>.Dispatch(m_ValueChangedMessage);
        }

        // --------------------------------------------------------------------
        // ISavable Implementation
        // --------------------------------------------------------------------

        public GameAttributesSavable GetSavableData()
        {
            GameAttributesSavable savedData = new GameAttributesSavable();
            savedData.Entries = new List<GameAttributeSaveDataEntry>();
            foreach (KeyValuePair<string, string> pair in m_Attributes)
            {
                savedData.Entries.Add(new GameAttributeSaveDataEntry
                {
                    Id = pair.Key,
                    Value = pair.Value
                });
            }

            return savedData;
        }

        // --------------------------------------------------------------------

        public void SetFromSavedData(GameAttributesSavable savedData)
        {
            m_Attributes.Clear();
            foreach (GameAttributeSaveDataEntry entry in savedData.Entries)
            {
                Set(entry.Id, entry.Value);
            }
        }

    }
}