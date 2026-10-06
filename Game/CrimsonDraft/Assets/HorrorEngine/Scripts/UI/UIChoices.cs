using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIChoices : MonoBehaviour
    {
        [SerializeField] private Transform m_ChoicesParent;
        [SerializeField] private GameObject m_ChoicePrefab;

        private List<GameObject> m_Choices = new List<GameObject>();
        private UIStackedAction m_ShowChoicesAction;
        private ChoiceData m_CurrentData;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_ShowChoicesAction = new UIStackedAction()
            {
                Action = () =>
                {
                    ShowChoices(m_CurrentData.Choices);
                },
                StopProcessingActions = true,
                Name = "UIChoices.Show (Show Choices)"
            };

            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        public void Show(ChoiceData data)
        {
            m_CurrentData = data;
            PauseController.Instance.Pause(this);
            CursorController.Instance.SetInUI(true);

            if (data.ChoiceDialog.IsValid())
            {
                for (int i = 0; i < m_Choices.Count; ++i)
                {
                    m_Choices[i].SetActive(false);
                }
                gameObject.SetActive(true);

                UIManager.PushAction(m_ShowChoicesAction); // Needed for showing the choices after the dialog
                UIManager.Get<UIDialog>().Show(data.ChoiceDialog, false);
                
                return;
            }
            else
            {
                ShowChoices(data.Choices);
            }
        }

        // --------------------------------------------------------------------

        private void ShowChoices(ChoiceEntry[] choices)
        {
            int diff = choices.Length - m_Choices.Count;
            if (diff > 0)
            {
                for(int i = 0; i < diff; ++i)
                {
                    m_Choices.Add(Instantiate(m_ChoicePrefab, m_ChoicesParent));
                }
            }

            for (int i = 0; i < m_Choices.Count; ++i)
            {
                bool active = i < choices.Length;
                m_Choices[i].SetActive(active);

                var button = m_Choices[i].GetComponent<Button>();
                button.onClick.RemoveAllListeners();

                if (active)
                {
                    m_Choices[i].GetComponentInChildren<TMPro.TextMeshProUGUI>().text = choices[i].ChoiceText;

                    var action = choices[i].OnSelected;
                    button.onClick.AddListener(() =>
                    {
                        Hide(); // This needs to happen first in case the action triggers a new choice
                        action?.Invoke();
                    });
                }
            }

            if (m_Choices[0].activeSelf)
                m_Choices[0].GetComponentInChildren<Selectable>().Select();

            gameObject.SetActive(true);
        }

        // --------------------------------------------------------------------

        public void Hide()
        {
            PauseController.Instance.Resume(this);
            CursorController.Instance.SetInUI(false);
            gameObject.SetActive(false);

            var dialog = UIManager.Get<UIDialog>();
            if (dialog.gameObject.activeSelf)
            {
                UIManager.SetActionProcessingEnabled(false); // Make sure there is only 1 action popping this frame

                dialog.Hide();

                UIManager.SetActionProcessingEnabled(true);
            }

            UIManager.RemoveAction(m_ShowChoicesAction);
            UIManager.PopAction();
        }

    }

}