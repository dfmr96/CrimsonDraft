using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;

namespace HorrorEngine
{
    public abstract class UISettingsElement : MonoBehaviour
    {
        public TMPro.TextMeshProUGUI Label;
        public UnityEvent<string> OnValueChanged;

        protected SettingsElementContent m_Content;

        // ---------------------------------------------------------

        private void OnEnable()
        {
            // Refresh the selected option on enable if this has been initialized before
            if (m_Content)
                Fill(m_Content);   
        }

        // ---------------------------------------------------------

        public virtual void Fill(SettingsElementContent content)
        {
            m_Content = content;

            if (content.Name.IsLocalized)
                Label.GetComponent<LocalizeStringEvent>().StringReference = content.Name.Localized;
            else
                Label.text = content.Name;
        }
    }
}