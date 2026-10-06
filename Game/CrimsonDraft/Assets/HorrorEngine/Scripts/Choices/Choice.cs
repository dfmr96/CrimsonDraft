using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    [Serializable]
    public class ChoiceEntry
    {
        public LocalizableText ChoiceText;

        // TODO - Remove eventually
        [FormerlySerializedAs("Text")]
        [HideInInspector]
        public string Text_DEPRECATED;
        // ---------------


        public UnityEvent OnSelected;
    }

    [Serializable]
    public class ChoiceData
    {
        public DialogData ChoiceDialog;
        public ChoiceEntry[] Choices;

        public bool IsValid()
        {
            return Choices.Length > 0;
        }   
    }


    public class Choice: MonoBehaviour
    {
        public ChoiceData Data;
        public UnityEvent OnChoiceStart;
        public UnityEvent OnChoiceEnd;

        private UnityAction m_OnClose;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_OnClose = OnClose;
        }

        // --------------------------------------------------------------------

        public void Choose()
        {
            OnChoiceStart?.Invoke();

            UIManager.PushAction(new UIStackedAction()
            {
                Action = m_OnClose,
                Name = "Choice.Choose (OnClose)"
            });

            UIManager.Get<UIChoices>().Show(Data);
        }

        // --------------------------------------------------------------------

        private void OnClose()
        {
            OnChoiceEnd?.Invoke();
        }


        // --------------------------------------------------------------------

        public void Cancel()
        {
            OnChoiceEnd = null;
            UIChoices choices = UIManager.Get<UIChoices>();
            if (choices.isActiveAndEnabled)
                choices.Hide();
        }

#if UNITY_EDITOR
        public void EditorOnly_MigrateUnlocalizedData()
        {
            var data = Data;
            foreach (var choiceItem in data.Choices)
            {
                if (!string.IsNullOrEmpty(choiceItem.Text_DEPRECATED))
                {
                    choiceItem.ChoiceText.Unlocalized = choiceItem.Text_DEPRECATED;
                    choiceItem.Text_DEPRECATED = "";
                }
            }

            Data.ChoiceDialog.EditorOnly_MigrateUnlocalizedData();
            EditorUtility.SetDirty(this);
        }
#endif

    }
}