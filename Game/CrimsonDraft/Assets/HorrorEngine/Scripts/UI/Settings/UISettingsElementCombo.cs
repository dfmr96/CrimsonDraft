using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UISettingsElementCombo : UISettingsElement
    {
        public TMPro.TextMeshProUGUI SelectedText;
        private int m_CurrentIndex;

        // ---------------------------------------------------------

        public void SetSelected(int index)
        {
            m_CurrentIndex = index;
            SettingsElementComboContent comboContent = m_Content as SettingsElementComboContent;
            SelectedText.text = comboContent.GetItemName(index);
        }

        // ---------------------------------------------------------

        public void MoveNext()
        {
            Move(1);
        }

        // ---------------------------------------------------------

        public void MovePrev()
        {
            Move(-1);
        }

        // ---------------------------------------------------------

        private void Move(int offset)
        {
            m_CurrentIndex += offset;

            SettingsElementComboContent comboContent = m_Content as SettingsElementComboContent;

            if (m_CurrentIndex < 0)
            {
                m_CurrentIndex = comboContent.GetItemCount() - 1;
            }
            else if (m_CurrentIndex >= comboContent.GetItemCount())
            {
                m_CurrentIndex = 0;
            }

            SetSelected(m_CurrentIndex);

            OnValueChanged?.Invoke(comboContent.GetItemName(m_CurrentIndex));
        }

        // ---------------------------------------------------------

        public override void Fill(SettingsElementContent content)
        {
            base.Fill(content);

            string outVal;
            if (!SettingsManager.Instance.Get(content, out outVal))
            {
                outVal = content.GetDefaultValue();
            }

            SettingsElementComboContent comboContent = content as SettingsElementComboContent;
            int index = comboContent.GetItemIndex(outVal);
            
            if (index >= 0)
                SetSelected(index);
            else
                SetSelected(0);
        }
    }
}