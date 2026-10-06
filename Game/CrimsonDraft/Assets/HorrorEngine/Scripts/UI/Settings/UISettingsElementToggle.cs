using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UISettingsElementToggle : UISettingsElement
    {
        private bool m_Ticked;
        public UnityEvent OnTick;
        public UnityEvent OnUntick;

        // ---------------------------------------------------------

        public void SetTicked(bool ticked)
        {
            m_Ticked = ticked;
            if (ticked)
            {
                OnTick?.Invoke();
            }
            else
            {
                OnUntick?.Invoke();
            }
        }

        // ---------------------------------------------------------

        public void Toggle()
        {
            SetTicked(!m_Ticked);
            OnValueChanged?.Invoke(m_Ticked ? "1" : "0");
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

            SetTicked(outVal != "0");
        }
    }
}